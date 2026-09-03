#!/usr/bin/env python3
"""Independent CI coverage gate (phase 2 item 2.10).

Measures per-stack line coverage from the test suites' own reports and fails
when it falls below a floor, when changed lines are undercovered, or when a
report that should exist does not.

The changed-lines floor is the `--changed-diff` plus `--floor-changed` pair; it
pools both stacks into one verdict, because the requirement is about the
submission rather than about a stack. Until 2026-09-03 this docstring described
that check while no such code existed -- no diff parsing, no merge base, no
flag. It was a false claim sitting in the gate built to catch false claims, and
it is called out here rather than quietly deleted because the failure mode
(documentation asserting a capability nobody re-checked) is the one this whole
phase exists to find.

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


def parse_lcov(path: Path, prefix: str) -> dict[str, dict[int, int]]:
    """Return {file: {line_number: hits}} from an lcov report.

    Per-line rather than the LF/LH aggregates, because the changed-lines floor
    needs to ask about specific line numbers. Verified equivalent on this repo's
    report before the change was made: the DA records total 2663/1796, exactly
    the LF/LH totals, so the absolute floor still reads 69.46% after exclusions.

    A line repeated across records takes the MAXIMUM hit count. Covered by one
    suite and missed by another is covered.
    """
    per_file: dict[str, dict[int, int]] = {}
    cur: str | None = None
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        line = line.strip()
        if line.startswith("SF:"):
            cur = normalise(line[3:], prefix)
            per_file.setdefault(cur, {})
        elif line.startswith("DA:") and cur:
            number, _, hits = line[3:].partition(",")
            try:
                n, h = int(number), int(hits.split(",")[0])
            except ValueError:
                continue
            lines = per_file[cur]
            lines[n] = max(lines.get(n, 0), h)
        elif line == "end_of_record":
            cur = None
    return per_file


def parse_cobertura(path: Path, prefix: str) -> dict[str, dict[int, int]]:
    """Return {file: {line_number: hits}} from a Cobertura report.

    Keying by line number also de-duplicates: one source file can appear under
    several `<class>` elements (partial classes, generic instantiations), and
    counting each `<line>` element independently would inflate the denominator
    with lines that are not distinct.
    """
    per_file: dict[str, dict[int, int]] = {}
    root = ET.parse(path).getroot()
    for cls in root.iter("class"):
        fn = cls.get("filename")
        if not fn:
            continue
        lines = per_file.setdefault(normalise(fn, prefix), {})
        for ln in cls.iter("line"):
            number = ln.get("number")
            if number is None:
                continue
            try:
                n, h = int(number), int(ln.get("hits", "0"))
            except ValueError:
                continue
            lines[n] = max(lines.get(n, 0), h)
    return per_file


def parse_changed_lines(path: Path) -> dict[str, set[int]]:
    """Return {file: {line numbers added or modified}} from a unified diff.

    Expects `git diff --unified=0`, where each hunk covers only changed lines,
    so the new-side range of every hunk header IS the changed set. Only the new
    side is read: a line the submission deletes cannot be covered by a test.
    """
    changed: dict[str, set[int]] = {}
    hunk = re.compile(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@")
    cur: set[int] | None = None
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        if line.startswith("+++ "):
            target = line[4:].strip()
            if target == "/dev/null":
                cur = None            # a deletion has no new side
                continue
            if target.startswith("b/"):
                target = target[2:]
            cur = changed.setdefault(target.replace("\\", "/"), set())
        elif line.startswith("@@") and cur is not None:
            m = hunk.match(line)
            if not m:
                continue
            start = int(m.group(1))
            count = int(m.group(2)) if m.group(2) is not None else 1
            cur.update(range(start, start + count))
    return changed


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


def summarise(per_file: dict[str, dict[int, int]],
              patterns: list[re.Pattern[str]]) -> tuple[int, int, int]:
    """Return (lines_found, lines_hit, files_counted) after exclusions."""
    found = hit = files = 0
    for path, lines in sorted(per_file.items()):
        if excluded(path, patterns):
            continue
        found += len(lines)
        hit += sum(1 for h in lines.values() if h > 0)
        files += 1
    return found, hit, files


def summarise_changed(per_file: dict[str, dict[int, int]],
                      changed: dict[str, set[int]],
                      patterns: list[re.Pattern[str]]) -> tuple[int, int, int]:
    """Return (changed_coverable, changed_hit, files_touched) after exclusions.

    Only lines that are BOTH changed by the submission and coverable by the
    suite are counted. A changed line the coverage report says nothing about --
    a comment, a blank line, an interface declaration -- is not a coverage
    failure and must not be counted against the floor, or the gate would demand
    tests for things that cannot have any.
    """
    coverable = hit = files = 0
    for path, lines in sorted(per_file.items()):
        if excluded(path, patterns):
            continue
        touched = changed.get(path)
        if not touched:
            continue
        relevant = [lines[n] for n in sorted(touched) if n in lines]
        if not relevant:
            continue
        coverable += len(relevant)
        hit += sum(1 for h in relevant if h > 0)
        files += 1
    return coverable, hit, files


def unmeasured_changed(per_file: dict[str, dict[int, int]],
                       changed: dict[str, set[int]],
                       patterns: list[re.Pattern[str]]) -> list[str]:
    """Changed source files the coverage report says NOTHING about.

    THE BLIND SPOT THIS EXISTS TO MAKE VISIBLE. karma/istanbul only instruments
    files reachable from a spec, so a source file with no spec never appears in
    the lcov at all. Its changed lines are therefore not "uncovered" -- they are
    invisible, and a changed-lines floor computed only over what the report
    mentions would pass a brand-new, wholly untested component in silence. That
    is the precise failure this phase exists to find, so the gate reports it on
    every run instead of leaving it to a document nobody re-reads.

    Scoped by the extensions the report itself uses, so it self-configures per
    stack (.ts from lcov, .cs from Cobertura) and does not flag the markdown and
    YAML in the same diff, which legitimately have no coverage.

    Measured 2026-09-03, on the lcov produced from `feat/ci-coverage-floors`
    at d2d9a0d9 (667 specs, all passing):

        find angular/src -name '*.ts' ! -name '*.spec.ts' ! -name '*.d.ts' | wc -l
        -> 492 source files
        grep -c '^SF:' angular/coverage/CaseEvaluation/lcov.info
        -> 99 files with any coverage record

    and replaying commit 216a2d04 (PR #493) through this check: 33 changed
    files, 6 with a record, 27 without.

    Reported, deliberately NOT failed: at those proportions failing would block
    essentially every submission, which is a decision for the epic to take
    explicitly, not one for this script to impose.
    """
    extensions = {Path(p).suffix for p in per_file if Path(p).suffix}
    if not extensions:
        return []
    return sorted(
        path for path in changed
        if Path(path).suffix in extensions
        and path not in per_file
        and not excluded(path, patterns)
    )


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
    ap.add_argument("--changed-diff",
                    help="unified diff from `git diff --unified=0 <base>...HEAD`; "
                         "enables the changed-lines floor")
    ap.add_argument("--floor-changed",
                    help="percentage of changed coverable lines that must be hit")
    ap.add_argument("--measure-only", action="store_true",
                    help="print the figures and skip floor enforcement; for "
                         "establishing a baseline, never for gating")
    args = ap.parse_args()

    patterns = load_exclusions(Path(args.exclusions))

    # MEASURE EVERY STACK BEFORE VALIDATING ANY FLOOR.
    #
    # These were one loop until 2026-09-03, and the ordering was a real defect.
    # require_floor() calls die(), which exits the process immediately, and its
    # message instructs the reader to "read the measured figure printed by this
    # job and set the floor to it". Measuring after that check meant the figure
    # was never printed: the instruction pointed at output that could not exist,
    # and an unset backend floor also exited before the frontend was measured at
    # all. The whole "the gate's first run fails and tells you the number" design
    # rests on the figures reaching stdout first, so they are gathered here and
    # judged below.
    measured = []
    # Pooled across stacks for the changed-lines floor below. Keys are
    # repo-relative paths from normalise(), which is what makes them comparable
    # with the diff's paths in the first place.
    coverage_by_file: dict[str, dict[int, int]] = {}
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
        pct = (hit / found * 100) if found else 0.0
        if args.measure_only:
            print(f"{label}: {pct:.2f}% ({hit}/{found} lines over {files} files) "
                  "[measure-only, not gated]")
        else:
            # "measured" distinguishes this from report()'s verdict line, which
            # repeats the figure next to the floor. The repetition is deliberate:
            # this is the line that survives an unset-floor exit, and the one the
            # error message tells the reader to copy the floor from.
            print(f"{label}: measured {pct:.2f}% ({hit}/{found} lines over {files} files)")
        measured.append((label, found, hit, files, floor_arg))
        coverage_by_file.update(per_file)

    if args.measure_only:
        return 0

    ok = True
    for label, found, hit, files, floor_arg in measured:
        floor = require_floor(floor_arg, label)
        ok = report(label, found, hit, files, floor) and ok

    # THE CHANGED-LINES FLOOR.
    #
    # Both stacks are pooled into one verdict rather than judged separately,
    # because the requirement is about the submission, not about a stack: "WHEN
    # the lines a pull request changes are covered at less than 90%". A PR
    # touching both sides gets one number, which is also what SonarCloud's
    # new-code metric reports.
    if args.changed_diff is not None:
        diff_path = Path(args.changed_diff)
        # Deliberately NOT require_report(). That helper treats an empty file as
        # a failure, which is right for a coverage report -- an empty one means
        # the suite produced nothing -- and wrong for a diff. The two absences
        # mean opposite things here:
        #   file missing -> the workflow never computed it, the gate is miswired,
        #                   and a miswired gate must fail rather than skip.
        #   file empty   -> the submission genuinely changes nothing against the
        #                   base. That is the empty case the changed-files
        #                   pattern requires to exit 0, not a defect.
        if not diff_path.is_file():
            die(f"changed-lines diff not found at {diff_path}. The workflow "
                "computes this before invoking the gate, so its absence means "
                "the gate was wired wrong. Failing rather than skipping: a "
                "skipped check is indistinguishable from a passing one.")
        try:
            changed = parse_changed_lines(diff_path)
        except Exception as exc:  # noqa: BLE001 -- a parse failure is a gate failure
            die(f"changed-lines diff at {diff_path} could not be parsed: {exc}")
        floor = require_floor(args.floor_changed, "changed-lines")

        blind = unmeasured_changed(coverage_by_file, changed, patterns)
        if blind:
            print(f"changed-lines: NOTE -- {len(blind)} changed source file(s) have "
                  "no coverage record at all and are invisible to this floor "
                  "(no spec reaches them), e.g. " + ", ".join(blind[:3]))

        coverable, hit, files = summarise_changed(coverage_by_file, changed, patterns)
        if coverable == 0:
            # Exits 0 deliberately, and this is NOT the absent-input hole the
            # rest of this gate guards against. There the report was missing;
            # here the reports were read and genuinely contain no coverable line
            # this submission changed -- a docs-only or config-only PR. Failing
            # those would make the gate impossible to satisfy honestly.
            print("changed-lines: no coverable changed lines in this submission "
                  "-> nothing to enforce")
        else:
            pct = hit / coverable * 100
            verdict = "PASS" if pct >= floor else "FAIL"
            print(f"changed-lines: {pct:.2f}% ({hit}/{coverable} changed coverable "
                  f"lines over {files} files) floor {floor:.2f}% -> {verdict}")
            ok = (pct >= floor) and ok

    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
