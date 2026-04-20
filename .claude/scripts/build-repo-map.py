#!/usr/bin/env python3
"""Build repository map artifacts.

Scans the repository for C# and TypeScript source files, extracts public
symbols, builds a dependency graph, ranks files by in-degree, and writes
three artifacts to docs/repo-map/:

  - index.json : machine-readable structural index
  - map.md     : token-budgeted Markdown summary
  - README.md  : how to use and regenerate the map (idempotent -- only
                 written if missing)

Uses only the Python 3 standard library. Regex-based extraction is
reliable here because the codebase follows ABP naming conventions.

Run from the repo root:
    python .claude/scripts/build-repo-map.py

Exit codes:
    0 -- success
    1 -- parse error or unexpected filesystem state
"""

from __future__ import annotations

import json
import os
import re
import sys
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path

# -----------------------------------------------------------------------------
# Configuration
# -----------------------------------------------------------------------------

REPO_ROOT = Path(__file__).resolve().parents[2]
OUT_DIR = REPO_ROOT / "docs" / "repo-map"

CS_ROOTS = [REPO_ROOT / "src", REPO_ROOT / "test"]
TS_ROOTS = [REPO_ROOT / "angular" / "src" / "app"]
CSPROJ_GLOB = "**/*.csproj"

EXCLUDE_PARTS = {"bin", "obj", "node_modules", "dist", "proxy", ".angular"}

CS_SYMBOL_RE = re.compile(
    r"^\s*public\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*"
    r"(?P<kind>class|interface|enum|record|struct)\s+"
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)",
    re.MULTILINE,
)
CS_USING_RE = re.compile(r"^\s*using\s+([A-Za-z0-9_.]+)\s*;", re.MULTILINE)
CS_NAMESPACE_RE = re.compile(
    r"^\s*namespace\s+([A-Za-z0-9_.]+)", re.MULTILINE
)

TS_SYMBOL_RE = re.compile(
    r"^\s*export\s+(?:default\s+)?"
    r"(?P<kind>class|interface|enum|function|const|type)\s+"
    r"(?P<name>[A-Za-z_$][A-Za-z0-9_$]*)",
    re.MULTILINE,
)
TS_IMPORT_RE = re.compile(
    r"""^\s*import\s+(?:[^;]*?\s+from\s+)?['"]([^'"]+)['"]""", re.MULTILINE
)

PROJECT_REF_RE = re.compile(
    r"<ProjectReference\s+Include=\"([^\"]+)\"", re.IGNORECASE
)


# -----------------------------------------------------------------------------
# Helpers
# -----------------------------------------------------------------------------

def is_excluded(path: Path) -> bool:
    return any(part in EXCLUDE_PARTS for part in path.parts)


def rel(path: Path) -> str:
    return path.relative_to(REPO_ROOT).as_posix()


def read_text_safe(path: Path) -> str | None:
    try:
        return path.read_text(encoding="utf-8-sig")
    except (OSError, UnicodeDecodeError):
        return None


def iter_files(roots: list[Path], suffix: str):
    for root in roots:
        if not root.exists():
            continue
        for path in root.rglob(f"*{suffix}"):
            if is_excluded(path):
                continue
            yield path


# -----------------------------------------------------------------------------
# Extraction
# -----------------------------------------------------------------------------

def extract_cs(path: Path) -> dict | None:
    text = read_text_safe(path)
    if text is None:
        return None
    symbols = [
        {"kind": m.group("kind"), "name": m.group("name")}
        for m in CS_SYMBOL_RE.finditer(text)
    ]
    usings = CS_USING_RE.findall(text)
    ns_match = CS_NAMESPACE_RE.search(text)
    return {
        "file": rel(path),
        "language": "csharp",
        "namespace": ns_match.group(1) if ns_match else None,
        "symbols": symbols,
        "uses_namespaces": sorted(set(usings)),
    }


def extract_ts(path: Path) -> dict | None:
    text = read_text_safe(path)
    if text is None:
        return None
    symbols = [
        {"kind": m.group("kind"), "name": m.group("name")}
        for m in TS_SYMBOL_RE.finditer(text)
    ]
    imports = TS_IMPORT_RE.findall(text)
    return {
        "file": rel(path),
        "language": "typescript",
        "symbols": symbols,
        "imports": sorted(set(imports)),
    }


def extract_csproj_refs(path: Path) -> list[str]:
    text = read_text_safe(path) or ""
    refs = []
    for m in PROJECT_REF_RE.finditer(text):
        ref_raw = m.group(1).replace("\\", "/")
        ref_path = (path.parent / ref_raw).resolve()
        try:
            refs.append(rel(ref_path))
        except ValueError:
            refs.append(ref_raw)
    return refs


# -----------------------------------------------------------------------------
# Analysis
# -----------------------------------------------------------------------------

def build_namespace_to_files(cs_entries: list[dict]) -> dict[str, list[str]]:
    result: dict[str, list[str]] = defaultdict(list)
    for entry in cs_entries:
        ns = entry.get("namespace")
        if ns:
            result[ns].append(entry["file"])
    return result


def rank_cs_files(cs_entries: list[dict]) -> Counter:
    """Count how many files 'use' each namespace, and attribute that count
    to every file defining a symbol in that namespace.
    """
    ns_to_files = build_namespace_to_files(cs_entries)
    file_rank: Counter = Counter()
    for entry in cs_entries:
        for used_ns in entry.get("uses_namespaces", []):
            for defining_file in ns_to_files.get(used_ns, []):
                if defining_file != entry["file"]:
                    file_rank[defining_file] += 1
    return file_rank


def _resolve_relative_ts_import(src_dir, imp: str, by_file: dict) -> str | None:
    """Resolve a relative TS import to a repo-relative file path, if known."""
    resolved_base = (src_dir / imp).resolve()
    for candidate in (
        resolved_base.with_suffix(".ts"),
        resolved_base.with_suffix(".tsx"),
        resolved_base / "index.ts",
    ):
        try:
            rel_target = rel(candidate)
        except ValueError:
            continue
        if rel_target in by_file:
            return rel_target
    return None


def rank_ts_files(ts_entries: list[dict]) -> Counter:
    """Count how many files import a given path. Normalizes relative imports
    to absolute file paths where possible.
    """
    file_rank: Counter = Counter()
    by_file = {e["file"]: e for e in ts_entries}
    for entry in ts_entries:
        src_dir = (REPO_ROOT / entry["file"]).parent
        for imp in entry.get("imports", []):
            if not imp.startswith("."):
                continue  # external package
            rel_target = _resolve_relative_ts_import(src_dir, imp, by_file)
            if rel_target is not None:
                file_rank[rel_target] += 1
    return file_rank


# -----------------------------------------------------------------------------
# Output
# -----------------------------------------------------------------------------

def write_index_json(data: dict) -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    (OUT_DIR / "index.json").write_text(
        json.dumps(data, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def top_n(counter: Counter, n: int) -> list[tuple[str, int]]:
    return counter.most_common(n)


def render_map_md(data: dict) -> str:
    lines: list[str] = []
    lines.append("[Home](../INDEX.md) > Repository Map")
    lines.append("")
    lines.append("# Repository Map")
    lines.append("")
    lines.append(
        "> Auto-generated by `.claude/scripts/build-repo-map.py`. "
        "Regenerate after significant structural changes. "
        "For instructions, see [README](README.md)."
    )
    lines.append("")
    lines.append(f"**Generated:** {data['generated_at']}")
    lines.append("")

    # Stacks / services
    lines.append("## Stacks Detected")
    lines.append("")
    for stack in data["stacks"]:
        lines.append(f"- **{stack['name']}** ({stack['files']} files) -- {stack['note']}")
    lines.append("")

    # Projects
    lines.append("## .NET Projects")
    lines.append("")
    lines.append("| Project | References |")
    lines.append("|---|---|")
    for proj in data["projects"]:
        refs = ", ".join(Path(r).stem for r in proj["references"]) or "(none)"
        lines.append(f"| `{proj['name']}` | {refs} |")
    lines.append("")

    # Dependency diagram
    lines.append("## Project Dependency Graph")
    lines.append("")
    lines.append("```mermaid")
    lines.append("flowchart LR")
    for proj in data["projects"]:
        src = Path(proj["name"]).stem.replace(".", "_")
        for ref in proj["references"]:
            tgt = Path(ref).stem.replace(".", "_")
            lines.append(f"    {src} --> {tgt}")
    lines.append("```")
    lines.append("")

    # Top-ranked C# files
    lines.append("## Top 15 Most-Referenced C# Files")
    lines.append("")
    lines.append("Files with the highest in-degree -- these are the most "
                 "load-bearing pieces of the codebase.")
    lines.append("")
    lines.append("| Rank | In-degree | File |")
    lines.append("|---|---|---|")
    for i, (path, score) in enumerate(data["top_cs_files"], start=1):
        lines.append(f"| {i} | {score} | `{path}` |")
    lines.append("")

    # Top-ranked TS files
    lines.append("## Top 15 Most-Imported Angular Files")
    lines.append("")
    lines.append("| Rank | In-degree | File |")
    lines.append("|---|---|---|")
    for i, (path, score) in enumerate(data["top_ts_files"], start=1):
        lines.append(f"| {i} | {score} | `{path}` |")
    lines.append("")

    # Summary stats
    lines.append("## Summary Statistics")
    lines.append("")
    lines.append(f"- C# files scanned: **{data['stats']['cs_files']}**")
    lines.append(f"- C# public symbols: **{data['stats']['cs_symbols']}**")
    lines.append(f"- TypeScript files scanned: **{data['stats']['ts_files']}**")
    lines.append(f"- TypeScript exported symbols: **{data['stats']['ts_symbols']}**")
    lines.append(f"- .NET projects: **{data['stats']['projects']}**")
    lines.append("")

    # Commands
    lines.append("## Commands")
    lines.append("")
    for name, cmd in data["commands"].items():
        lines.append(f"- **{name}:** `{cmd}`")
    lines.append("")

    return "\n".join(lines)


def write_map_md(data: dict) -> None:
    (OUT_DIR / "map.md").write_text(render_map_md(data), encoding="utf-8")


def ensure_readme() -> None:
    readme = OUT_DIR / "README.md"
    if readme.exists():
        return
    content = """[Home](../INDEX.md) > Repository Map > README

# Repository Map

Machine-readable structural index of the codebase. Regenerated on demand from
source files, never hand-edited.

## Files

| File | Purpose |
|------|---------|
| `index.json` | Full machine-readable index: files, symbols, dependencies, rankings |
| `map.md` | Human-readable summary with top-ranked files and a dependency diagram |
| `README.md` | This file |

## Regenerate

```bash
python .claude/scripts/build-repo-map.py
```

The script scans C# under `src/` and `test/`, and TypeScript under
`angular/src/app/` (excluding `proxy/`, `bin/`, `obj/`, `node_modules/`,
`dist/`, `.angular/`). It uses only the Python 3 standard library.

## How the Ranking Works

- **C# files** are ranked by how many other files declare a `using` statement
  for their namespace. A file defining heavily-used types will rank high.
- **TypeScript files** are ranked by how many other files import them via a
  relative path. External package imports (non-relative) are ignored.

Ranking is heuristic, not algorithmic PageRank. Use it for navigation, not
for refactoring decisions.

## When to Regenerate

- After adding or removing a project
- After a large refactor that moves files between projects
- Before a review / release where accurate top-N rankings matter

The `scheduled-doc-check` skill flags staleness if `index.json` is older than
30 days.

## Related

- [Verify Structure Skill](../../.claude/skills/verify-structure/SKILL.md)
- [Generate Repo Map Skill](../../.claude/skills/generate-repo-map/SKILL.md)
"""
    readme.write_text(content, encoding="utf-8")


# -----------------------------------------------------------------------------
# Orchestration
# -----------------------------------------------------------------------------

def build() -> dict:
    cs_entries = [e for p in iter_files(CS_ROOTS, ".cs") for e in [extract_cs(p)] if e]
    ts_entries = [e for p in iter_files(TS_ROOTS, ".ts") for e in [extract_ts(p)] if e]

    csproj_paths = list(REPO_ROOT.glob(CSPROJ_GLOB))
    projects = []
    for path in csproj_paths:
        if is_excluded(path):
            continue
        projects.append({
            "name": rel(path),
            "references": extract_csproj_refs(path),
        })
    projects.sort(key=lambda p: p["name"])

    cs_rank = rank_cs_files(cs_entries)
    ts_rank = rank_ts_files(ts_entries)

    stats = {
        "cs_files": len(cs_entries),
        "cs_symbols": sum(len(e["symbols"]) for e in cs_entries),
        "ts_files": len(ts_entries),
        "ts_symbols": sum(len(e["symbols"]) for e in ts_entries),
        "projects": len(projects),
    }

    stacks = [
        {"name": ".NET / ABP Framework 10.0.2", "files": stats["cs_files"],
         "note": "Detected via .csproj files"},
        {"name": "Angular 20 (standalone components)", "files": stats["ts_files"],
         "note": "Detected via angular/src/app"},
    ]

    commands = {
        "build (dotnet)": "dotnet build HealthcareSupport.CaseEvaluation.slnx",
        "test (dotnet)": "dotnet test",
        "migrate DB": "dotnet run --project src/HealthcareSupport.CaseEvaluation.DbMigrator",
        "build (angular)": "cd angular && npx ng build --configuration development",
        "serve (angular)": "cd angular && npx serve -s dist/CaseEvaluation/browser -p 4200",
    }

    return {
        "generated_at": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "stats": stats,
        "stacks": stacks,
        "projects": projects,
        "files": cs_entries + ts_entries,
        "top_cs_files": top_n(cs_rank, 15),
        "top_ts_files": top_n(ts_rank, 15),
        "commands": commands,
        "extraction_method": {
            "csharp": "regex (public class/interface/enum/record/struct + using)",
            "typescript": "regex (export class/interface/enum/function + import)",
            "projects": "regex on ProjectReference elements in .csproj",
            "ranking": "in-degree count (not PageRank; heuristic)",
        },
    }


def main() -> int:
    try:
        data = build()
    except Exception as exc:  # noqa: BLE001 -- want full failure info on CI
        print(f"build-repo-map: FAILED -- {exc}", file=sys.stderr)
        return 1

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    write_index_json(data)
    write_map_md(data)
    ensure_readme()

    print(
        f"build-repo-map: OK -- {data['stats']['cs_files']} C# files, "
        f"{data['stats']['ts_files']} TS files, "
        f"{data['stats']['projects']} projects"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
