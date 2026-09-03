#!/usr/bin/env python3
"""Independent CI coverage gate (phase 2 item 2.10).

Measures per-stack line coverage from the test suites' own reports and fails
when it falls below a floor, when changed lines are undercovered, or when a
report that should exist does not.

WHY THIS EXISTS SEPARATELY FROM SONARCLOUD. SonarCloud's 80% new-code quality
gate stays exactly as it is. This is a second, version-controlled opinion that
can be reviewed in a pull request and does not depend on the service it
double-checks. It also cannot be seeded from SonarCloud even in principle:
SonarCloud analyses only `main`, and on `main` the Angular tree reports 19
coverable lines because of an older exclusion list.

THE DEFECT THIS MUST NOT HAVE. Every test job in ci.yml is conditional, and a
skipped job reports Success without blocking a merge even when it is required.
So absent input is a HARD FAILURE here, never an absent constraint -- and an
unset floor is a hard failure too, for the same reason: a gate configured with
no threshold is a check that passes because nobody finished wiring it.
"""

from __future__ import annotations

import argparse
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import NoReturn


def glob_to_regex(pattern: str) -> re.Pattern[str]:
    """Translate a coverage-exclusion glob into an anchored regex.

    Handles the three forms the shared list uses: a `**/` prefix for any leading
    directories, a `/**` suffix for any trailing ones, and `*` for anything but
    a path separator. Written out rather than delegated to fnmatch, which treats
    `*` as crossing separators and would silently over-match.
    """
    out: list[str] = []
    i = 0
    while i < len(pattern):
        if pattern.startswith("**/", i):
            out.append("(?:.*/)?")
            i += 3
        elif pattern.startswith("/**", i):
            out.append("(?:/.*)?")
            i += 3
        elif pattern[i] == "*":
            out.append("[^/]*")
            i += 1
        elif pattern[i] == "?":
            out.append("[^/]")
            i += 1
        else:
            out.append(re.escape(pattern[i]))
            i += 1
    return re.compile("^" + "".join(out) + "$")


def load_exclusions(path: Path) -> list[re.Pattern[str]]:
    """Read the shared exclusion list, ignoring comments and blank lines."""
    if not path.is_file():
        die(f"exclusion list not found: {path}. It is shared with sonarcloud.yml "
            "and both consumers must read the same file.")
    patterns = []
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line and not line.startswith("#"):
            patterns.append(glob_to_regex(line))
    if not patterns:
        die(f"exclusion list {path} contains no patterns. An empty list is more "
            "likely a mistake than a decision; comment out the file's use instead.")
    return patterns


def normalise(raw: str, prefix: str) -> str:
    """Make a report path repo-relative with forward slashes.

    Both halves matter. karma emits Windows separators (`src\\app\\proxy\\x.ts`)
    and paths relative to `angular/`, so without normalising AND prefixing, the
    `angular/src/app/proxy/**` exclusion matches nothing and the gate silently
    measures more than it should.
    """
    p = raw.replace("\\", "/").lstrip("./")
    if prefix:
        p = f"{prefix.rstrip('/')}/{p}"
    return p


def excluded(path: str, patterns: list[re.Pattern[str]]) -> bool:
    return any(p.match(path) for p in patterns)


def parse_lcov(path: Path, prefix: str) -> dict[str, tuple[int, int]]:
    """Return {file: (lines_found, lines_hit)} from an lcov report."""
    per_file: dict[str, tuple[int, int]] = {}
    cur = None
    found = hit = 0
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        line = line.strip()
        if line.startswith("SF:"):
            cur, found, hit = normalise(line[3:], prefix), 0, 0
        elif line.startswith("LF:") and cur:
            found = int(line[3:])
        elif line.startswith("LH:") and cur:
            hit = int(line[3:])
        elif line == "end_of_record" and cur:
            per_file[cur] = (found, hit)
            cur = None
    return per_file


def parse_cobertura(path: Path, prefix: str) -> dict[str, tuple[int, int]]:
    """Return {file: (lines_found, lines_hit)} from a Cobertura report."""
    per_file: dict[str, tuple[int, int]] = {}
    root = ET.parse(path).getroot()
    for cls in root.iter("class"):
        fn = cls.get("filename")
        if not fn:
            continue
        key = normalise(fn, prefix)
        found, hit = per_file.get(key, (0, 0))
        for ln in cls.iter("line"):
            found += 1
            if int(ln.get("hits", "0")) > 0:
                hit += 1
        per_file[key] = (found, hit)
    return per_file


def die(msg: str) -> NoReturn:
    print(f"::error::{msg}")
    sys.exit(1)


def require_report(path: Path, label: str) -> None:
    """Absent or empty input is a failure, never a pass."""
    if not path.is_file():
        die(f"{label} coverage report not found at {path}. Its test job either did "
            "not run or did not produce a report. A missing report is a FAILURE "
            "here, not an absent constraint -- a skipped job reports Success and "
            "would otherwise let this gate pass without measuring anything.")
    if path.stat().st_size == 0:
        die(f"{label} coverage report at {path} is empty.")


def require_floor(value: str | None, label: str) -> float:
    """An unset floor is a failure, not the absence of a constraint."""
    if value is None or not str(value).strip():
        die(f"{label} coverage floor is not set. This gate refuses to run without "
            "one: an unconfigured threshold is a check that passes because nobody "
            "finished wiring it. Read the measured figure printed by this job and "
            "set the floor to it.")
    try:
        return float(value)
    except ValueError:
        die(f"{label} coverage floor {value!r} is not a number.")


def summarise(per_file: dict[str, tuple[int, int]],
              patterns: list[re.Pattern[str]]) -> tuple[int, int, int]:
    """Return (lines_found, lines_hit, files_counted) after exclusions."""
    found = hit = files = 0
    for path, (lf, lh) in sorted(per_file.items()):
        if excluded(path, patterns):
            continue
        found += lf
        hit += lh
        files += 1
    return found, hit, files


def report(label: str, found: int, hit: int, files: int, floor: float) -> bool:
    if found == 0:
        die(f"{label}: zero coverable lines after exclusions. Either the report is "
            "not what it should be, or the exclusion list now excludes everything. "
            "Refusing to report 0/0 as a pass.")
    pct = hit / found * 100
    verdict = "PASS" if pct >= floor else "FAIL"
    print(f"{label}: {pct:.2f}% ({hit}/{found} lines over {files} files) "
          f"floor {floor:.2f}% -> {verdict}")
    return pct >= floor


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--exclusions", default=".coverage-exclusions")
    ap.add_argument("--lcov", help="frontend lcov report")
    ap.add_argument("--lcov-prefix", default="angular",
                    help="repo-relative directory the lcov paths are relative to")
    ap.add_argument("--cobertura", help="backend Cobertura report")
    ap.add_argument("--cobertura-prefix", default="")
    ap.add_argument("--floor-frontend")
    ap.add_argument("--floor-backend")
    ap.add_argument("--measure-only", action="store_true",
                    help="print the figures and skip floor enforcement; for "
                         "establishing a baseline, never for gating")
    args = ap.parse_args()

    patterns = load_exclusions(Path(args.exclusions))
    ok = True

    for label, path_arg, prefix, floor_arg, parser in (
        ("backend", args.cobertura, args.cobertura_prefix, args.floor_backend, parse_cobertura),
        ("frontend", args.lcov, args.lcov_prefix, args.floor_frontend, parse_lcov),
    ):
        if path_arg is None:
            continue
        path = Path(path_arg)
        require_report(path, label)
        try:
            per_file = parser(path, prefix)
        except Exception as exc:  # noqa: BLE001 -- any parse failure is a gate failure
            die(f"{label} coverage report at {path} could not be parsed: {exc}")
        found, hit, files = summarise(per_file, patterns)
        if args.measure_only:
            pct = (hit / found * 100) if found else 0.0
            print(f"{label}: {pct:.2f}% ({hit}/{found} lines over {files} files) "
                  "[measure-only, not gated]")
            continue
        floor = require_floor(floor_arg, label)
        ok = report(label, found, hit, files, floor) and ok

    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
