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

# Code is stripped before link matching. Without this, any "](" sequence inside a code
# span or fence is read as a Markdown link. It is not hypothetical: the slug regex
# ^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$ documented in architecture/OFFICES-AND-HOSTING.md
# contains "](?:" and was reported as an unresolved link on 2026-09-28.
#
# This matters more than a single false positive. CI runs this script as a BLOCKING step,
# and a checker that cries wolf is exactly why a gate ends up disabled.
#
# Both strippers are plain scanners rather than regexes. The regexes they replace
# (FENCE_RE with a lazy DOTALL body and a back-reference, INLINE_CODE_RE with adjacent
# backtick quantifiers) were flagged by Sonar python:S8786 as super-linear. The scanners
# reproduce the regexes' behaviour exactly, including two quirks worth knowing:
#   - an opening fence of N backticks or tildes first looks for a closer of all N, then
#     N-1, down to 3 (a regex back-reference backtracking over a greedy run);
#   - a run of two or more backticks with no closing run on its line is removed on its
#     own, while a single unmatched backtick is kept.
_FENCE_CHARS = ("`", "~")
_MIN_FENCE = 3


def _fence_close(lines: list[str], start: int) -> int | None:
    """Index of the line that closes a fence opened at lines[start], or None.

    An opener is a run of at least three backticks or tildes at column 0. A closer is a later
    line holding exactly that fence, plus optional trailing spaces or tabs.
    """
    char = lines[start][:1]
    if char not in _FENCE_CHARS:
        return None
    run = len(lines[start]) - len(lines[start].lstrip(char))
    for length in range(run, _MIN_FENCE - 1, -1):
        fence = char * length
        for index in range(start + 1, len(lines)):
            if lines[index].rstrip(" \t") == fence:
                return index
    return None


def _blank_fences(text: str) -> str:
    """Empty every line of each closed fenced block, keeping the line count."""
    lines = text.split("\n")
    index = 0
    while index < len(lines):
        close = _fence_close(lines, index)
        if close is None:
            index += 1
            continue
        lines[index : close + 1] = [""] * (close + 1 - index)
        index = close + 1
    return "\n".join(lines)


def _run_end(text: str, index: int) -> int:
    """Index just past the run of backticks starting at text[index]."""
    while index < len(text) and text[index] == "`":
        index += 1
    return index


def _strip_inline_code(text: str) -> str:
    """Remove inline code spans: a backtick run, non-backtick text on one line, a backtick run."""
    kept: list[str] = []
    index = 0
    while (start := text.find("`", index)) >= 0:
        kept.append(text[index:start])
        run_end = _run_end(text, start)
        stop = run_end
        while stop < len(text) and text[stop] not in "`\n":
            stop += 1
        if stop < len(text) and text[stop] == "`":
            index = _run_end(text, stop)
        elif run_end - start >= 2:
            index = run_end
        else:
            kept.append("`")
            index = start + 1
    kept.append(text[index:])
    return "".join(kept)


def strip_code(text: str) -> str:
    """Blank out fenced blocks and inline code spans, preserving line count.

    Fenced blocks collapse to their newlines so any line-based reporting added later
    still points at the right place; inline spans are removed outright.
    """
    return _strip_inline_code(_blank_fences(text))


def is_excluded(path: Path) -> bool:
    return any(part in EXCLUDE_PARTS for part in path.parts)


def _md_files_under(root: Path) -> list[Path]:
    """Markdown files under one root, minus the excluded paths.

    Split out of gather_md_files purely to flatten nesting: the four-deep
    for/if/for/if scored 16 on cognitive complexity against a limit of 15
    (Sonar python:S3776). Behaviour is unchanged.
    """
    if not root.is_dir():
        return []
    return [p for p in root.rglob("*.md") if not is_excluded(p)]


def gather_md_files() -> list[Path]:
    files: set[Path] = set()
    for root in SCAN_DIRS:
        files.update(_md_files_under(root))
    for pattern in CLAUDE_GLOBS:
        files.update(p for p in REPO_ROOT.glob(pattern) if not is_excluded(p))
    return sorted(files)


def _skip_reason(target: str) -> tuple[bool, str] | None:
    """Return (True, detail) when the link doesn't need filesystem resolution."""
    if not target:
        return True, "empty"
    if re.match(r"^[a-z][a-z0-9+.-]*:", target):
        return True, "external"
    if target.startswith("#"):
        return True, "anchor"
    if "{" in target and "}" in target:
        return True, "template"
    return None


def validate_link(source_file: Path, target: str) -> tuple[bool, str]:
    """Return (is_valid, detail)."""
    target = target.strip()
    skip = _skip_reason(target)
    if skip is not None:
        return skip
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
        for m in LINK_RE.finditer(strip_code(text)):
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
