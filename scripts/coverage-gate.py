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
import functools
import json
import re
# The FIRST subprocess call in this file, and a deliberate narrowing of the
# purity #856 chose. Its reasoning was "discovery lives in the workflow because
# that script shells out nowhere and the Python suite imports it directly;
# keeping it pure keeps it testable" -- which is right about testability and is
# why --tracked-files stays the primary path and every test uses it.
#
# The fallback exists because the alternative was a check that disappears when
# one workflow line is deleted, printing a notice on its way out. A guard whose
# absence is announced is still absent, and #683 is specifically about a
# contamination that arrives with no signal.
import subprocess
import sys
import xml.etree.ElementTree as ET
from collections.abc import Callable
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


def load_tracked(path: Path) -> set[str]:
    """Read the repository tracked-file list, ignoring comments and blanks.

    Mirrors `load_exclusions` deliberately -- same shape, same two failure modes.
    An absent list means the step that writes it did not run; an empty one is
    more likely a mistake than a decision. Either would let this check pass
    without looking, which is the failure class it exists to remove.
    """
    if not path.is_file():
        die(f"tracked-file list not found: {path}. CI produces it with "
            "`git ls-files`; a missing list means that step did not run, and "
            "that is a failure rather than an absent constraint.")
    tracked = set()
    for line in path.read_text(encoding="utf-8").splitlines():
        stripped = line.strip()
        if stripped and not stripped.startswith("#"):
            tracked.add(stripped)
    if not tracked:
        die(f"tracked-file list {path} is empty. An empty list is more likely a "
            "mistake than a decision; omit --tracked-files instead.")
    return tracked


def discover_tracked() -> set[str]:
    """Ask git for the tracked-file list when the workflow did not supply one.

    #683 asked for contamination to fail the build rather than move the figure.
    A `--tracked-files` flag delivers that only while someone remembers to pass
    it: delete one line from ci.yml and the check is gone, having printed a
    notice. This makes the flag an OPTIMISATION rather than the mechanism --
    the workflow still passes a manifest, and without one the gate finds out
    for itself.

    A git failure EXITS. "Cannot list" is not "nothing is tracked" (which would
    fail every file) and not "everything is tracked" (which would disable the
    guard exactly when it cannot be evaluated).
    """
    result = subprocess.run(["git", "ls-files"], capture_output=True,
                            text=True, check=False)
    if result.returncode != 0:
        die("no --tracked-files was supplied and `git ls-files` failed, so the "
            f"tracked-file check cannot run: {result.stderr.strip()}. Pass "
            "--tracked-files, or run this from inside the work tree.")
    tracked = {line.strip() for line in result.stdout.splitlines() if line.strip()}
    if not tracked:
        die("`git ls-files` returned nothing, so every counted file would look "
            "untracked. Refusing to report 100% contamination, which is a tell "
            "rather than a result.")
    return tracked


@functools.cache
def workspace_prefixes() -> tuple[str, ...]:
    """The prefixes a repo-relative path may legitimately arrive behind.

    Exactly ONE thing is recognised: the checkout this gate is running in, as
    git reports it. Both the slashed and unslashed spellings are returned,
    because `normalise` does not strip a leading `/` and reports have been
    observed in both shapes.

    Deriving it rather than listing runner layouts means this keeps working on a
    self-hosted runner, a container, or a developer machine, none of which put
    the checkout at `/home/runner/work/<repo>/<repo>`.

    An empty tuple when git cannot answer is deliberate and safe HERE, unlike in
    `discover_tracked`: with no recognised prefix every absolute path fails the
    guard, which is the closed direction. `discover_tracked` has already exited
    on a git failure long before this runs.
    """
    result = subprocess.run(["git", "rev-parse", "--show-toplevel"],
                            capture_output=True, text=True, check=False)
    if result.returncode != 0:
        return ()
    root = result.stdout.strip().replace("\\", "/").rstrip("/")
    # The `if p` filter is what makes a blank answer safe, so there is no
    # separate `if not root` guard above it. Mutation testing is why: with one
    # there, disabling EITHER left the function still returning () -- each was
    # masked by the other, so neither could be seen to fail and both read as
    # load-bearing while being redundant.
    return tuple(dict.fromkeys(p for p in (root, root.lstrip("/")) if p))


def in_repo(path: str, tracked: set[str],
            prefixes: tuple[str, ...] | None = None) -> bool:
    """Is this report path one of the repository own files?

    ANCHORED, not a suffix search, and that distinction is the whole point
    (#864). Cobertura carries absolute checkout paths, so one of our files
    arrives as `<workspace>/src/App/Foo.cs`; comparing by equality fails for
    EVERY file -- measured once as 925 of 925 "untracked", which is a tell
    rather than a result.

    The first implementation fixed that by trying every suffix at a separator
    boundary. That re-admitted precisely what this guard exists to reject: with
    `src/App/Foo.cs` tracked, all three of these passed --

        _/src/App/Foo.cs             the SourceLink vendor root itself
        vendor/src/App/Foo.cs        a vendored copy of one of our paths
        /nix/store/abc/src/App/Foo.cs  an unrelated absolute tree

    A vendor file only had to END with a path we track. The guard was widest
    exactly where its threat model is.

    Now a prefix is removed ONLY when it is a recognised workspace root, so a
    tree at any other location stays unmatched and still fails.
    """
    if path in tracked:
        return True
    for prefix in (workspace_prefixes() if prefixes is None else prefixes):
        if path.startswith(prefix + "/") and path[len(prefix) + 1:] in tracked:
            return True
    return False


def assert_tracked(per_file: dict[str, dict[int, int]],
                   patterns: list[re.Pattern[str]],
                   tracked: set[str],
                   prefixes: tuple[str, ...] | None = None) -> None:
    """Fail if any COUNTED file is not a file of this repository.

    Its own function rather than a branch inside `summarise`: measuring was split
    from judging on 2026-09-03, and folding an assertion into the counting path
    would re-merge exactly what that change separated.

    Third-party source embedded by SourceLink has been graded as ours twice, by
    two different roots, each fixed by naming that vendor in the exclusion list.
    That list grows one vendor at a time, and the next one matches none of its
    patterns and arrives with no signal at all. This is the signal.
    """
    # Resolved once here rather than per file: `in_repo` would otherwise shell
    # out to git for every counted path, thousands of times for one report.
    prefixes = workspace_prefixes() if prefixes is None else prefixes
    strays = sorted(p for p in per_file
                    if not excluded(p, patterns)
                    and not in_repo(p, tracked, prefixes))
    if not strays:
        return
    for stray in strays[:10]:
        print(f"  untracked and counted: {stray}")
    if len(strays) > 10:
        print(f"  ... and {len(strays) - 10} more")
    die(f"{len(strays)} counted file(s) are not tracked in this repository, so a "
        "dependency source is being graded as ours. Either exclude that "
        "population in .coverage-exclusions with its reason, or fix the prefix "
        "that made our own files unrecognisable.")


def normalise(raw: str, prefix: str) -> str:
    """Make a report path repo-relative with forward slashes.

    Both halves matter. karma emits Windows separators (`src\\app\\proxy\\x.ts`)
    and paths relative to `angular/`, so without normalising AND prefixing, the
    `angular/src/app/proxy/**` exclusion matches nothing and the gate silently
    measures more than it should.
    """
    # removeprefix, NOT lstrip. `lstrip("./")` strips a CHARACTER SET, so it
    # eats the leading dot of any dot-directory: ".claude/scripts/x.py" became
    # "claude/scripts/x.py". Harmless while only lcov and the .NET Cobertura
    # were fed in -- neither emits a dot-directory -- and live the moment Python
    # coverage arrives, because `.claude/scripts/*.py` is in its denominator.
    # The damage would have been silent in both consumers at once: a
    # `.coverage-exclusions` entry for `.claude/**` would match nothing, and the
    # changed-lines floor would compare the diff's ".claude/scripts/x.py"
    # against the report's "claude/scripts/x.py" and find no record -- making
    # exactly the files this gate was extended to watch invisible to it.
    p = raw.replace("\\", "/")
    while p.startswith("./"):
        p = p[2:]
    if prefix:
        p = f"{prefix.rstrip('/')}/{p}"
    return p


def excluded(path: str, patterns: list[re.Pattern[str]]) -> bool:
    return any(p.match(path) for p in patterns)


def parse_lcov(path: Path, prefix: str) -> dict[str, dict[int, int]]:
    """Return {file: {line_number: hits}} from an lcov report.

    Per-line rather than the LF/LH aggregates, because the changed-lines floor
    needs to ask about specific line numbers. Verified equivalent on this repo's
    report before the change was made: the DA records totalled 2663/1796,
    exactly the LF/LH totals of the report at the time.

    Re-checked 2026-09-04 against the widened report from item 2.13, because a
    parser that agreed on 99 files is not thereby proven on 324: reading it with
    no prefix, so no exclusion applies, gives 2049/9881 -- karma's own summary
    for the same run prints 20.73% (2049/9881). The parser is measured against
    an independent count of the same file rather than trusted.

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


_HUNK = re.compile(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@")


def _diff_target(line: str) -> str | None:
    """Repo-relative path from a `+++ ` header, or None when there is no new side.

    None means the submission DELETED the file, and a deleted line cannot be
    covered by a test, so it must not join the changed set.
    """
    target = line[4:].strip()
    if target == "/dev/null":
        return None
    if target.startswith("b/"):
        target = target[2:]
    return target.replace("\\", "/")


def _hunk_lines(line: str) -> range:
    """New-side line numbers a hunk header covers; empty range if it is not one.

    Returning an empty range rather than None keeps the caller's loop flat --
    `update(range(0))` is a no-op, so there is nothing to branch on.
    """
    m = _HUNK.match(line)
    if not m:
        return range(0)
    start = int(m.group(1))
    count = int(m.group(2)) if m.group(2) is not None else 1
    return range(start, start + count)


def parse_changed_lines(path: Path) -> dict[str, set[int]]:
    """Return {file: {line numbers added or modified}} from a unified diff.

    Expects `git diff --unified=0`, where each hunk covers only changed lines,
    so the new-side range of every hunk header IS the changed set. Only the new
    side is read: a line the submission deletes cannot be covered by a test.
    """
    changed: dict[str, set[int]] = {}
    cur: set[int] | None = None
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        if line.startswith("+++ "):
            target = _diff_target(line)
            cur = None if target is None else changed.setdefault(target, set())
        elif line.startswith("@@") and cur is not None:
            cur.update(_hunk_lines(line))
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


def validated_output_path(destination: str) -> Path:
    """Resolve and check a CLI-supplied output path BEFORE anything is written.

    Two reasons, and the second is the one that made this a review finding.

    The path arrives from `--per-file`, so it is command-line input reaching a
    file write. SonarCloud grades that as path traversal, and the same rule
    already fires three times in this file on `main` -- `load_exclusions`,
    `load_tracked` and `parse_changed_lines` all read a path that came from a
    flag. That is what a CLI gate is: every path it touches is an argument. The
    difference here is that this one WRITES, so it is worth being explicit about
    what it will and will not write to rather than inheriting the read path's
    silence.

    And EVERY OTHER FAILURE IN THIS SCRIPT IS EXPLAINED. `require_report`,
    `require_floor`, `load_exclusions`, `discover_tracked` and `assert_tracked`
    all end at `die()` with a sentence saying what was wrong. A bare
    `write_text` on a missing directory would have been the one path that exits
    on a raw traceback -- the newest line in a file whose whole design is that a
    failure tells you what to do about it.
    """
    # NO try/except around resolve(), and that is a decision rather than an
    # omission. With the default strict=False, resolve() is documented to
    # resolve "as far as possible" and append the remainder WITHOUT checking
    # that it exists, so it does not raise for a path that is missing.
    #
    # Verified before the guard was deleted rather than argued from the docs:
    # a missing path, an empty string, a deep missing chain, a Windows reserved
    # name (CON), invalid characters (<>|), a 300-character segment, a `..`
    # escape above the repo root and a trailing-dot-and-space name ALL returned
    # normally. The only failure this function can actually meet is the
    # is-a-directory case, which the explicit check below handles and a test
    # covers. An except clause nothing can reach is untestable code that makes
    # the next reader think a failure mode exists.
    resolved = Path(destination).expanduser().resolve()
    if resolved.is_dir():
        die(f"--per-file was given {destination!r}, which is an existing "
            "directory. Pass the path of the JSON file to write, not the "
            "directory to write it into.")
    parent = resolved.parent
    if not parent.is_dir():
        die(f"--per-file was given {destination!r}, whose parent directory "
            f"{parent} does not exist. This gate does not create directories: "
            "the caller decides where its output belongs, and silently making "
            "one would hide a mistyped path until someone went looking for a "
            "report that was written somewhere else.")
    return resolved


def emit_per_file(args: argparse.Namespace,
                  coverage_by_file: dict[str, dict[int, int]],
                  patterns: list[re.Pattern[str]]) -> None:
    """Write the breakdown if it was asked for. A no-op otherwise.

    Its own function because `main` now calls it from TWO places -- once in the
    measure-only branch and once after the floor verdict -- and the two must not
    drift. Inlining it twice is how they would.
    """
    if args.per_file is None:
        return
    count = write_per_file(args.per_file, coverage_by_file, patterns)
    print(f"per-file: wrote {count} file(s) to {args.per_file}")


def write_per_file(destination: str,
                   per_file: dict[str, dict[int, int]],
                   patterns: list[re.Pattern[str]]) -> int:
    """Write a per-file coverage breakdown as JSON. Returns the file count.

    THE POINT IS THE DENOMINATOR, not the convenience. Ranking which files to
    test next was being done from raw lcov by throwaway scripts that applied
    their own idea of what counts -- one of them dropped the ABP proxies by a
    substring match and nothing else, which happened to agree here and would not
    on a report with different contamination. A ranking that disagrees with the
    gate about what is counted sends work at files the gate does not grade.

    So this deliberately sits beside `summarise` and walks the same map through
    the same `excluded` call. The set emitted here is exactly the set summarise
    counts; if one ever changes, both must.

    Two properties are inherited rather than reimplemented, which is the other
    half of the point: paths have already been through `normalise` (so
    `--lcov-prefix` is applied), and a line repeated across records has already
    been resolved to its MAXIMUM hit count by the parser. A separate reader of
    the same report would have to get both right again.

    Sorted by uncovered DESCENDING with ties broken by path, so the output is a
    ranking AND is byte-identical across two runs over one report. An unstable
    order would make every regeneration a noisy diff.
    """
    rows = []
    for source, lines in per_file.items():
        if excluded(source, patterns):
            continue
        found = len(lines)
        hit = sum(1 for h in lines.values() if h > 0)
        rows.append({"path": source, "found": found, "hit": hit,
                     "uncovered": found - hit})
    rows.sort(key=lambda r: (-r["uncovered"], r["path"]))
    path = validated_output_path(destination)
    try:
        path.write_text(json.dumps(rows, indent=2) + "\n", encoding="utf-8")
    except OSError as exc:
        die(f"could not write the per-file breakdown to {path}: {exc}")
    return len(rows)


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

    THE DECISION THIS ASKED FOR HAS BEEN TAKEN. Until 2026-09-04 this docstring
    said the proportions above made failing impossible, and that whether to fail
    was "a decision for the epic to take explicitly, not one for this script to
    impose". Adrian took it on 2026-09-04, in item 2.13, and the answer was to
    remove the proportions rather than to keep excusing them: the Angular blind
    spot was closed at its source by widening the karma target's `include` and
    `tsconfig.spec.json`, so the 184 previously invisible sources now carry
    records and their changed lines are subject to the changed-lines floor like
    any others. His reasoning, kept because a successor will need it:

        A rule that only applies to files which already have tests creates an
        incentive never to write the first one -- the worst files stay the
        safest to touch. One uniform rule is also what someone inheriting this
        should learn: we cover what we touch.

    So this check is no longer a standing excuse for a whole stack. It remains
    because it is still the honest report for anything a coverage run genuinely
    cannot see -- a file the build excludes, a stack whose report failed to
    generate, a language with no instrumentation -- and because a count that
    creeps back up is the first symptom of the blind spot reopening.
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


def build_parser() -> argparse.ArgumentParser:
    ap = argparse.ArgumentParser()
    ap.add_argument("--exclusions", default=".coverage-exclusions")
    ap.add_argument("--tracked-files",
                    help="file listing the repo tracked paths (`git ls-files`). "
                         "Counted files outside it fail the gate (#683)")
    ap.add_argument("--lcov", help="frontend lcov report")
    ap.add_argument("--lcov-prefix", default="angular",
                    help="repo-relative directory the lcov paths are relative to")
    ap.add_argument("--cobertura", help="backend Cobertura report")
    ap.add_argument("--cobertura-prefix", default="")
    ap.add_argument("--python-cobertura",
                    help="Python coverage.xml (coverage.py's Cobertura output)")
    # No --python-prefix: coverage.py emits repo-relative filenames already,
    # unlike karma's lcov which is relative to angular/. Verified against a real
    # coverage.xml rather than assumed.
    ap.add_argument("--floor-frontend")
    ap.add_argument("--floor-backend")
    ap.add_argument("--floor-python")
    ap.add_argument("--changed-diff",
                    help="unified diff from `git diff --unified=0 <base>...HEAD`; "
                         "enables the changed-lines floor")
    ap.add_argument("--floor-changed",
                    help="percentage of changed coverable lines that must be hit")
    ap.add_argument("--measure-only", action="store_true",
                    help="print the figures and skip floor enforcement; for "
                         "establishing a baseline, never for gating")
    # Writes to a PATH rather than to stdout. The frontend stack alone counts
    # 272 files, and stdout here is gate-output.txt, which a human reads on
    # every run; a 272-line dump would change that artefact for everyone to
    # serve the one caller that wants to parse it.
    # The help text says what the code does, including the awkward half. It
    # previously ended "does not affect any figure or exit code", which stopped
    # being true the moment a failed write could die() -- and a help string
    # contradicting its own implementation is the stale-comment defect this
    # programme has spent the day removing, not a rounding error.
    ap.add_argument("--per-file",
                    help="write a JSON per-file coverage breakdown to this path "
                         "(one object per counted file, ranked by uncovered "
                         "lines). Affects no measured figure, and is written "
                         "AFTER the floor verdict so it cannot turn a PASS into "
                         "a FAIL -- but a path that cannot be written fails the "
                         "run rather than being skipped silently")
    return ap


def measure_stack(label: str, path_arg: str, prefix: str, parser: Callable[[Path, str], dict[str, dict[int, int]]],
                  patterns: list[re.Pattern[str]],
                  measure_only: bool) -> tuple[int, int, int, dict[str, dict[int, int]]]:
    """Read one stack's report and PRINT its figure. Returns (found, hit, files, per_file)."""
    path = Path(path_arg)
    require_report(path, label)
    try:
        per_file = parser(path, prefix)
    except Exception as exc:  # noqa: BLE001 -- any parse failure is a gate failure
        die(f"{label} coverage report at {path} could not be parsed: {exc}")
    found, hit, files = summarise(per_file, patterns)
    pct = (hit / found * 100) if found else 0.0
    if measure_only:
        print(f"{label}: {pct:.2f}% ({hit}/{found} lines over {files} files) "
              "[measure-only, not gated]")
    else:
        # "measured" distinguishes this from report()'s verdict line, which
        # repeats the figure next to the floor. The repetition is deliberate:
        # this is the line that survives an unset-floor exit, and the one the
        # error message tells the reader to copy the floor from.
        print(f"{label}: measured {pct:.2f}% ({hit}/{found} lines over {files} files)")
    return found, hit, files, per_file


def measure_all(args: argparse.Namespace,
                patterns: list[re.Pattern[str]]) -> tuple[list[tuple], dict[str, dict[int, int]]]:
    """MEASURE EVERY STACK BEFORE VALIDATING ANY FLOOR.

    Measuring and judging were one loop until 2026-09-03, and the ordering was a
    real defect. require_floor() calls die(), which exits the process
    immediately, and its message instructs the reader to "read the measured
    figure printed by this job and set the floor to it". Measuring after that
    check meant the figure was never printed: the instruction pointed at output
    that could not exist, and an unset backend floor also exited before the
    frontend was measured at all. The whole "the gate's first run fails and tells
    you the number" design rests on the figures reaching stdout first, so they
    are gathered here and judged by the caller.

    The returned coverage map is POOLED across stacks for the changed-lines
    floor. Keys are repo-relative paths from normalise(), which is what makes
    them comparable with the diff's paths in the first place.
    """
    measured: list[tuple] = []
    coverage_by_file: dict[str, dict[int, int]] = {}
    for label, path_arg, prefix, floor_arg, parser in (
        ("backend", args.cobertura, args.cobertura_prefix, args.floor_backend, parse_cobertura),
        ("frontend", args.lcov, args.lcov_prefix, args.floor_frontend, parse_lcov),
        # coverage.py writes Cobertura, so the existing parser reads it
        # unchanged, and its paths are repo-relative so the prefix is empty.
        ("python", args.python_cobertura, "", args.floor_python, parse_cobertura),
    ):
        if path_arg is None:
            continue
        found, hit, files, per_file = measure_stack(
            label, path_arg, prefix, parser, patterns, args.measure_only)
        measured.append((label, found, hit, files, floor_arg))
        coverage_by_file.update(per_file)
    return measured, coverage_by_file


def load_changed_diff(path_arg: str) -> dict[str, set[int]]:
    """Parse the changed-lines diff, distinguishing MISSING from EMPTY.

    Deliberately NOT require_report(). That helper treats an empty file as a
    failure, which is right for a coverage report -- an empty one means the suite
    produced nothing -- and wrong for a diff. The two absences mean opposite
    things here:
      file missing -> the workflow never computed it, the gate is miswired, and a
                      miswired gate must fail rather than skip.
      file empty   -> the submission genuinely changes nothing against the base.
                      That is the empty case the changed-files pattern requires
                      to exit 0, not a defect.
    """
    diff_path = Path(path_arg)
    if not diff_path.is_file():
        die(f"changed-lines diff not found at {diff_path}. The workflow "
            "computes this before invoking the gate, so its absence means "
            "the gate was wired wrong. Failing rather than skipping: a "
            "skipped check is indistinguishable from a passing one.")
    try:
        return parse_changed_lines(diff_path)
    except Exception as exc:  # noqa: BLE001 -- a parse failure is a gate failure
        die(f"changed-lines diff at {diff_path} could not be parsed: {exc}")


def enforce_changed_lines(args: argparse.Namespace,
                          coverage_by_file: dict[str, dict[int, int]],
                          patterns: list[re.Pattern[str]]) -> bool:
    """THE CHANGED-LINES FLOOR. Returns whether it passed.

    Both stacks are pooled into one verdict rather than judged separately,
    because the requirement is about the submission, not about a stack: "WHEN the
    lines a pull request changes are covered at less than 90%". A PR touching
    both sides gets one number, which is also what SonarCloud's new-code metric
    reports.
    """
    changed = load_changed_diff(args.changed_diff)
    floor = require_floor(args.floor_changed, "changed-lines")

    blind = unmeasured_changed(coverage_by_file, changed, patterns)
    if blind:
        print(f"changed-lines: NOTE -- {len(blind)} changed source file(s) have "
              "no coverage record at all and are invisible to this floor "
              "(no spec reaches them), e.g. " + ", ".join(blind[:3]))

    coverable, hit, files = summarise_changed(coverage_by_file, changed, patterns)
    if coverable == 0:
        # Passes deliberately, and this is NOT the absent-input hole the rest of
        # this gate guards against. There the report was missing; here the
        # reports were read and genuinely contain no coverable line this
        # submission changed -- a docs-only or config-only PR. Failing those
        # would make the gate impossible to satisfy honestly.
        print("changed-lines: no coverable changed lines in this submission "
              "-> nothing to enforce")
        return True

    pct = hit / coverable * 100
    verdict = "PASS" if pct >= floor else "FAIL"
    print(f"changed-lines: {pct:.2f}% ({hit}/{coverable} changed coverable "
          f"lines over {files} files) floor {floor:.2f}% -> {verdict}")
    return pct >= floor


def main() -> int:
    args = build_parser().parse_args()
    patterns = load_exclusions(Path(args.exclusions))

    measured, coverage_by_file = measure_all(args, patterns)

    # NO STACK AT ALL IS A WIRING FAILURE, not an empty result.
    #
    # `require_report` guards a stack that IS named and `require_floor` guards a
    # stack that IS measured, so neither is reachable when nothing is named --
    # both were vacuous in precisely the case they exist for. The gate printed
    # NOTHING and exited 0, which is the silent pass this file's own docstring
    # forbids: "absent input is a HARD FAILURE here, never an absent
    # constraint". Verified before this was written, not assumed.
    #
    # Nothing reaches it today: ci.yml appends --python-cobertura outside every
    # `applicable()` branch and keeps an inverted empty-args detector of its own,
    # sonarcloud.yml passes a report explicitly, and the one test that drives
    # main() builds --cobertura into argv before any branch. This is defence in
    # depth behind an invariant the workflow already asserts -- so that deleting
    # that one workflow line fails loudly here instead of silently passing.
    #
    # FIRES IN MEASURE-ONLY MODE TOO. A baseline of nothing is not a baseline,
    # and measure-only is the mode whose figures feed the rolling comparison, so
    # a silent empty result does more damage there rather than less.
    if not measured:
        die("no coverage report was supplied, so nothing was measured and this "
            "gate would otherwise have passed by default. A gate with no input "
            "is a check that passes because nobody finished wiring it. Pass at "
            "least one of --lcov, --cobertura or --python-cobertura.")

    # Runs in measure-only mode too: this validates the INPUT, it is not a floor.
    #
    # There is no longer a skip branch. It printed its own absence, which reads
    # as diligence and is not: a stated skip and a silent one both end with the
    # check not running, and #683 is about contamination that arrives with no
    # signal. The flag is now an optimisation -- the workflow still supplies the
    # list, and without one the gate asks git.
    tracked = (load_tracked(Path(args.tracked_files))
               if args.tracked_files is not None
               else discover_tracked())
    assert_tracked(coverage_by_file, patterns, tracked)

    if args.measure_only:
        # Measure-only has no verdict for a diagnostic to pre-empt, so the
        # breakdown is emitted here and the mode still supports it.
        emit_per_file(args, coverage_by_file, patterns)
        return 0

    ok = True
    for label, found, hit, files, floor_arg in measured:
        floor = require_floor(floor_arg, label)
        ok = report(label, found, hit, files, floor) and ok

    if args.changed_diff is not None:
        ok = enforce_changed_lines(args, coverage_by_file, patterns) and ok

    # GATES EVALUATE BEFORE DIAGNOSTICS EMIT.
    #
    # This was above the floor loop until the review of #975, and the placement
    # was the real defect rather than the unhandled write everyone stopped at: a
    # mistyped --per-file path did not merely fail, it failed BEFORE the floor
    # was evaluated, so the answer the gate exists to give was lost to a
    # reporting flag. A diagnostic must not be able to pre-empt the verdict.
    #
    # `ok` is already decided here, so a failed write cannot change PASS into
    # FAIL -- it can only fail a run whose verdict has already been printed and
    # is therefore still knowable from the log. And the breakdown is written on
    # a FAILING run too, which is exactly when someone wants to know which files
    # are dragging the figure down.
    emit_per_file(args, coverage_by_file, patterns)

    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
