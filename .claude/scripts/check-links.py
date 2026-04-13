#!/usr/bin/env python3
"""Validate relative Markdown links under docs/ and CLAUDE.md files.

Scans every Markdown file for inline links of the form [text](target). For
any relative target (no scheme), resolves it from the containing file and
reports a FAIL if the target file does not exist.

External links (http://, https://, mailto:) and anchor-only references (#id)
are skipped -- anchor validation would require parsing heading text.

Output is report-only (prints FAIL lines). Exits 1 if any link fails.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]

SCAN_DIRS = [
    REPO_ROOT / "docs",
    REPO_ROOT / ".claude",
]

# Also scan all tracked CLAUDE.md files via glob below.
CLAUDE_GLOBS = ["CLAUDE.md", "**/CLAUDE.md"]

EXCLUDE_PARTS = {"bin", "obj", "node_modules", "dist", ".angular"}

LINK_RE = re.compile(r"\[(?P<text>[^\]]+)\]\((?P<target>[^)]+)\)")


def is_excluded(path: Path) -> bool:
    return any(part in EXCLUDE_PARTS for part in path.parts)


def gather_md_files() -> list[Path]:
    files: set[Path] = set()
    for root in SCAN_DIRS:
        if root.is_dir():
            for p in root.rglob("*.md"):
                if not is_excluded(p):
                    files.add(p)
    for pattern in CLAUDE_GLOBS:
        for p in REPO_ROOT.glob(pattern):
            if not is_excluded(p):
                files.add(p)
    return sorted(files)


def validate_link(source_file: Path, target: str) -> tuple[bool, str]:
    """Return (is_valid, detail)."""
    target = target.strip()
    if not target:
        return True, "empty"
    # Skip external / scheme links
    if re.match(r"^[a-z][a-z0-9+.-]*:", target):
        return True, "external"
    # Skip anchor-only
    if target.startswith("#"):
        return True, "anchor"
    # Skip template placeholders (e.g. {feature-kebab}, {target})
    if "{" in target and "}" in target:
        return True, "template"
    # Strip anchor fragment
    path_part = target.split("#", 1)[0]
    if not path_part:
        return True, "anchor-only"
    # Workspace-root-relative paths (VSCode convention: leading slash)
    if path_part.startswith("/"):
        resolved = (REPO_ROOT / path_part.lstrip("/")).resolve()
        if resolved.exists():
            return True, "ok-from-root"
        return False, f"unresolved (workspace-relative): {path_part}"
    resolved = (source_file.parent / path_part).resolve()
    if resolved.exists():
        return True, "ok"
    # Try relative to repo root (sometimes docs use that form)
    alt = (REPO_ROOT / path_part).resolve()
    if alt.exists():
        return True, "ok-from-root"
    return False, f"unresolved: {path_part}"


def main() -> int:
    md_files = gather_md_files()
    failures = 0
    checked = 0

    for path in md_files:
        try:
            text = path.read_text(encoding="utf-8")
        except (OSError, UnicodeDecodeError) as exc:
            print(f"WARN unreadable: {path} -- {exc}")
            continue
        for m in LINK_RE.finditer(text):
            target = m.group("target")
            checked += 1
            ok, detail = validate_link(path, target)
            if not ok:
                rel_path = path.relative_to(REPO_ROOT).as_posix()
                print(f"FAIL {rel_path}: {target} ({detail})")
                failures += 1

    print()
    print(f"check-links: scanned {len(md_files)} files, "
          f"validated {checked} links, {failures} failures")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
