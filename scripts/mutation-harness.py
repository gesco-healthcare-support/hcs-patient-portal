#!/usr/bin/env python3
"""Guard mutation harness (#1005): prove that the tests which claim to guard a rule can fail.

WHAT IT DOES. `scripts/mutation-manifest.json` lists NAMED mutations: a file, an exact string,
a replacement that breaks one guard (delete a permission check, flip a comparison, skip a
refusal) and the tests that are supposed to notice. For each one the harness

  1. runs the named tests on the UNMUTATED code and requires them green and non-empty
     (otherwise a later "failure" proves nothing);
  2. saves the original OUTSIDE the repository, records its sha256 in a journal, applies the
     one mutation, runs the same tests, and restores;
  3. verifies the restore by sha256 against the saved original -- never by `git diff`, which
     cannot see a mutation that merely moved a line;
  4. classifies the outcome from the TEST RESULTS, never from the process exit code.

WHY NOT EXIT CODES. The EF suite has exited 1 with zero failures when Docker teardown threw
under load, and a build failure also exits non-zero. A harness that read `$?` would call a
mutant that did not even compile "killed". Results are parsed from the trx file (dotnet) or the
verbose report (unittest), and a mutant that does not build is BUILD_ERROR, never a kill.

WHY IT CANNOT PASS VACUOUSLY. Every route to "0 survivors" without testing anything has its own
failing status: empty baseline -> NO_TESTS, red baseline -> BASELINE_RED, `find` text not
present exactly once -> INAPPLICABLE, mutant that does not compile -> BUILD_ERROR, no results
file -> NO_RESULT, a hang -> TIMEOUT. Only a non-empty, green baseline followed by a failing
named test counts as KILLED. tests/python/test_mutation_harness.py feeds it a vacuous test and a
sound one and asserts SURVIVED and KILLED respectively.

SAFE RESTORE. The original is copied outside the repo and journalled BEFORE the file is touched.
The restore runs in `finally`, on SIGINT/SIGTERM/SIGBREAK and at exit. A hard kill (SIGKILL,
TerminateProcess, power loss) cannot run any of that, so the journal survives it: the next `run`
refuses to start while an entry is outstanding, and `recover` restores from the journal and
verifies the hash. Never restore with `git checkout -- <file>`: it has reverted real uncommitted
work on this repo twice.

Stdlib only (pytest is not installed here). ASCII only. Files are written with newline="\\n".

Usage:
    python scripts/mutation-harness.py check-manifest   # no tests run; fails on a stale `find`
    python scripts/mutation-harness.py list
    python scripts/mutation-harness.py run [--only ID ...] [--changed REF] [--max-seconds N]
    python scripts/mutation-harness.py recover
"""

from __future__ import annotations

import argparse
import atexit
import contextlib
import hashlib
import json
import os
import re
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_MANIFEST = REPO_ROOT / "scripts" / "mutation-manifest.json"

# Outcomes. Only KILLED and SURVIVED are verdicts about the tests; everything else means the
# harness could not tell, and ALWAYS fails the run.
KILLED = "KILLED"
KILLED_BY_OTHER = "KILLED_BY_OTHER"
SURVIVED = "SURVIVED"
BUILD_ERROR = "BUILD_ERROR"
NO_TESTS = "NO_TESTS"
NO_RESULT = "NO_RESULT"
BASELINE_RED = "BASELINE_RED"
INAPPLICABLE = "INAPPLICABLE"
TIMEOUT = "TIMEOUT"
ESCAPES_ROOT = "ESCAPES_ROOT"

EXIT_OK, EXIT_PROBLEM, EXIT_INVALID, EXIT_RESTORE, EXIT_JOURNAL = 0, 1, 2, 3, 4
INVALID_STATUSES = {BUILD_ERROR, NO_TESTS, NO_RESULT, BASELINE_RED, INAPPLICABLE, TIMEOUT, ESCAPES_ROOT}


class RestoreError(RuntimeError):
    """A restored file does not hash to its saved original. Never swallowed."""


class JournalError(RuntimeError):
    """A previous run left a mutation outstanding."""


def sha256_of(path: Path) -> str:
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write_bytes_durable(path: Path, data: bytes) -> None:
    with open(path, "wb") as fh:
        fh.write(data)
        fh.flush()
        os.fsync(fh.fileno())


# ----------------------------------------------------------------------------- manifest


@dataclass
class Mutant:
    id: str
    file: str
    find: str
    replace: str
    runner: str
    project: str
    filter: str
    expect: str = "killed"
    reason: str = ""
    killed_by: list[str] = field(default_factory=list)
    watch: list[str] = field(default_factory=list)

    @property
    def group(self) -> tuple[str, str, str]:
        return (self.runner, self.project, self.filter)


def load_manifest(path: Path) -> list[Mutant]:
    # main() refuses a manifest that resolves outside the repository root (within_root) before this
    # is reached, and the default is the committed manifest.
    data = json.loads(Path(path).read_text(encoding="utf-8"))  # NOSONAR python:S8707
    mutants, seen = [], set()
    for raw in data["mutants"]:
        m = Mutant(
            id=raw["id"], file=raw["file"], find=raw["find"], replace=raw["replace"],
            runner=raw["runner"], project=raw.get("project", ""), filter=raw["filter"],
            expect=raw.get("expect", "killed"), reason=raw.get("reason", ""),
            killed_by=raw.get("killed_by", []), watch=raw.get("watch", []),
        )
        if m.id in seen:
            raise ValueError(f"duplicate mutant id {m.id!r}")
        seen.add(m.id)
        if m.expect not in ("killed", "survive"):
            raise ValueError(f"{m.id}: expect must be 'killed' or 'survive'")
        if m.expect == "survive" and not m.reason:
            raise ValueError(f"{m.id}: expect=survive needs a reason (a documented gap, not a shrug)")
        if m.runner not in RUNNERS:
            raise ValueError(f"{m.id}: unknown runner {m.runner!r}")
        if m.find == m.replace:
            raise ValueError(f"{m.id}: find and replace are identical, so this mutates nothing")
        mutants.append(m)
    return mutants


def within_root(root: Path, candidate: str | Path) -> bool:
    """True when `candidate`, resolved against `root` (symlinks followed, `..` collapsed), stays
    inside it. An absolute candidate replaces the root in a join, so it is caught here too."""
    base = Path(root).resolve()
    try:
        (base / candidate).resolve().relative_to(base)
    except ValueError:
        return False
    return True


def escapes_root(root: Path, m: Mutant) -> str | None:
    """Why a mutant would touch something outside the repository, else None.

    The harness WRITES to `m.file` and journals the write, so a typo or a stray `..` in a manifest
    entry would mutate a file outside the intended set and the restore journal would faithfully
    follow it there. Refuse before anything is touched."""
    for label, value in (("file", m.file), ("project", m.project)):
        if value and not within_root(root, value):
            return f"{label} {value!r} resolves outside the repository root"
    return None


def safe_git_ref(ref: str) -> bool:
    """A ref is never an option: a leading dash would be read by git as a flag."""
    return bool(re.fullmatch(r"[A-Za-z0-9_][A-Za-z0-9._/~^@{}-]*", ref))


def applicability(root: Path, m: Mutant) -> str | None:
    """None when `find` occurs exactly once in the file; otherwise why it does not."""
    outside = escapes_root(root, m)
    if outside:
        return outside
    target = root / m.file
    if not target.is_file():
        return f"{m.file} does not exist"
    n = target.read_bytes().count(m.find.encode("utf-8"))
    return None if n == 1 else f"find text occurs {n} times in {m.file}, need exactly 1"


# ----------------------------------------------------------------------------- runners


@dataclass
class RunResult:
    passed: set[str] = field(default_factory=set)
    failed: set[str] = field(default_factory=set)
    build_error: bool = False
    no_result: bool = False
    timed_out: bool = False
    tail: str = ""

    @property
    def total(self) -> int:
        return len(self.passed) + len(self.failed)


def kill_tree(proc: subprocess.Popen) -> None:
    if os.name == "nt":
        subprocess.run(["taskkill", "/T", "/F", "/PID", str(proc.pid)],
                       capture_output=True, check=False)
    else:
        with contextlib.suppress(ProcessLookupError):
            os.killpg(proc.pid, signal.SIGKILL)


def run_process(cmd: list[str], cwd: Path, env: dict[str, str], timeout: float) -> tuple[str, bool]:
    """Run to completion (output merged) or kill the whole tree on timeout."""
    kwargs: dict = {"creationflags": subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == "nt" \
        else {"start_new_session": True}
    proc = subprocess.Popen(cmd, cwd=cwd, env=env, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT, text=True, errors="replace", **kwargs)
    try:
        out, _ = proc.communicate(timeout=timeout)
        return out, False
    except subprocess.TimeoutExpired:
        kill_tree(proc)
        out, _ = proc.communicate()
        return out, True


def clean_env() -> dict[str, str]:
    """`dotnet test` must NOT see DOTNET_ENVIRONMENT: it wakes the demo seeders in the test
    harness and fails the whole EF suite (.claude/rules/dotnet-env.md, measured 2026-09-13)."""
    env = dict(os.environ)
    for key in ("DOTNET_ENVIRONMENT", "ASPNETCORE_ENVIRONMENT"):
        env.pop(key, None)
    return env


TRX_NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
_BAD_OUTCOMES = {"Failed", "Error", "Timeout", "Aborted"}


def parse_trx(text: str) -> RunResult:
    res = RunResult()
    root = ET.fromstring(text)
    for r in root.iter(f"{TRX_NS}UnitTestResult"):
        name, outcome = r.get("testName", ""), r.get("outcome", "")
        if outcome == "Passed":
            res.passed.add(name)
        elif outcome in _BAD_OUTCOMES:
            res.failed.add(name)
    return res


def run_dotnet(root: Path, project: str, test_filter: str, timeout: float) -> RunResult:
    results_dir = Path(tempfile.mkdtemp(prefix="mut-trx-"))
    try:
        cmd = ["dotnet", "test", str(root / project), "--filter", test_filter, "--nologo",
               "--logger", "trx;LogFileName=result.trx", "--results-directory", str(results_dir)]
        out, timed_out = run_process(cmd, root, clean_env(), timeout)
        trx = results_dir / "result.trx"
        if trx.is_file():
            res = parse_trx(trx.read_text(encoding="utf-8", errors="replace"))
        else:
            res = RunResult()
            res.build_error = bool(re.search(r"error (CS|MSB|NU)\d+|Build FAILED", out))
            res.no_result = not res.build_error
        res.timed_out = timed_out
        res.tail = out[-1500:]
        return res
    finally:
        shutil.rmtree(results_dir, ignore_errors=True)


_UT_LINE = re.compile(r"^(\S+) \(([\w.]+)\)")
_UT_VERDICT = re.compile(r" \.\.\. (ok|FAIL|ERROR|skipped.*|expected failure|unexpected success)$")


def parse_unittest(text: str) -> RunResult:
    """Parse `python -m unittest -v`. A module that fails to import is reported as a test
    named `unittest.loader._FailedTest...`: that is this runner's compile error, not a kill."""
    res = RunResult()
    current = None
    for line in text.splitlines():
        head = _UT_LINE.match(line)
        if head:
            current = head.group(2)
        verdict = _UT_VERDICT.search(line)
        if verdict and current:
            word = verdict.group(1)
            if word.startswith("skipped"):
                current = None  # a skip is neither a pass nor a failure
            elif "_FailedTest" in current or "ModuleImportFailure" in current:
                res.build_error = True
            elif word in ("ok", "expected failure"):
                res.passed.add(current)
            else:
                res.failed.add(current)
            current = None
    if re.search(r"^(SyntaxError|IndentationError)", text, re.M):
        res.build_error = True
    return res


def run_unittest(root: Path, project: str, test_filter: str, timeout: float) -> RunResult:
    # A fresh bytecode cache per run: .pyc invalidation keys on source mtime (whole seconds) and
    # size, so a same-size mutation made in the same second as the original would otherwise run
    # the ORIGINAL bytecode and report a false survivor.
    cache = tempfile.mkdtemp(prefix="mut-pyc-")
    try:
        cmd = [sys.executable, "-B", "-X", f"pycache_prefix={cache}", "-m", "unittest", "-v", test_filter]
        out, timed_out = run_process(cmd, root / (project or "."), clean_env(), timeout)
    finally:
        shutil.rmtree(cache, ignore_errors=True)
    res = parse_unittest(out)
    res.timed_out = timed_out
    res.tail = out[-1500:]
    return res


RUNNERS = {"dotnet": run_dotnet, "unittest": run_unittest}


# ----------------------------------------------------------------------------- journal


class Journal:
    """What is mutated right now, kept outside the repo so a hard kill leaves a way back."""

    def __init__(self, state_dir: Path):
        self.dir = Path(state_dir)
        # The state dir is OUTSIDE the repository on purpose: it holds the originals and the journal,
        # which must survive anything that happens to the checkout. It only ever receives files this
        # harness names itself (hash-named .orig copies and journal.json).
        self.dir.mkdir(parents=True, exist_ok=True)  # NOSONAR python:S8707
        self.path = self.dir / "journal.json"

    def entries(self) -> list[dict]:
        if not self.path.is_file():
            return []
        return json.loads(self.path.read_text(encoding="utf-8"))["entries"]

    def _save(self, entries: list[dict]) -> None:
        tmp = self.path.with_suffix(".tmp")
        write_bytes_durable(tmp, json.dumps({"entries": entries}, indent=2).encode("utf-8"))
        os.replace(tmp, self.path)

    def require_empty(self) -> None:
        if self.entries():
            names = ", ".join(e["file"] for e in self.entries())
            raise JournalError(f"an earlier run left a mutation outstanding on: {names}. "
                               "Run `mutation-harness.py recover` first.")

    def open_entry(self, target: Path, original: bytes) -> dict:
        backup = self.dir / f"{hashlib.sha256(str(target).encode()).hexdigest()[:16]}.orig"
        write_bytes_durable(backup, original)
        entry = {"file": str(target), "backup": str(backup),
                 "sha256_original": hashlib.sha256(original).hexdigest()}
        self._save(self.entries() + [entry])
        return entry

    def close_entry(self, entry: dict) -> None:
        self._save([e for e in self.entries() if e["file"] != entry["file"]])
        Path(entry["backup"]).unlink(missing_ok=True)


def restore(entry: dict) -> None:
    """Put the saved original back and PROVE it by hash. Raises RestoreError, never returns
    quietly on a mismatch."""
    target, backup = Path(entry["file"]), Path(entry["backup"])
    if not backup.is_file():
        raise RestoreError(f"backup {backup} is missing; {target} may still be mutated")
    original = backup.read_bytes()
    if hashlib.sha256(original).hexdigest() != entry["sha256_original"]:
        raise RestoreError(f"backup {backup} no longer matches the recorded hash")
    write_bytes_durable(target, original)
    if sha256_of(target) != entry["sha256_original"]:
        raise RestoreError(f"{target} does not hash to its original after restore")


def recover(journal: Journal) -> int:
    done = 0
    for entry in journal.entries():
        restore(entry)
        journal.close_entry(entry)
        print(f"restored {entry['file']} (sha256 verified)")
        done += 1
    print(f"recovered {done} file(s)")
    return done


# ----------------------------------------------------------------------------- engine


@dataclass
class Outcome:
    id: str
    status: str
    verdict: str
    detail: str = ""
    failed_tests: list[str] = field(default_factory=list)
    seconds: float = 0.0


def classify(res: RunResult) -> str | None:
    """Everything that stops a result being a verdict about the tests, else None."""
    if res.timed_out:
        return TIMEOUT
    if res.build_error:
        return BUILD_ERROR
    if res.no_result:
        return NO_RESULT
    if res.total == 0:
        return NO_TESTS
    return None


def verdict_for(m: Mutant, status: str) -> str:
    if status in INVALID_STATUSES:
        return "INVALID"
    if status == SURVIVED:
        return "OK" if m.expect == "survive" else "SURVIVOR"
    if status == KILLED:
        return "OK" if m.expect == "killed" else "STALE_EXPECTATION"
    return "WRONG_KILLER"


class Harness:
    def __init__(self, root: Path, journal: Journal, timeout: float = 1800.0):
        self.root, self.journal, self.timeout = Path(root), journal, timeout
        self._baselines: dict[tuple, RunResult] = {}
        self._active: dict | None = None

    # -- baseline
    def baseline(self, m: Mutant) -> str | None:
        """None when the unmutated tests are non-empty and green; else the failing status."""
        if m.group not in self._baselines:
            self._baselines[m.group] = RUNNERS[m.runner](self.root, m.project, m.filter, self.timeout)
        res = self._baselines[m.group]
        problem = classify(res)
        if problem:
            return problem
        return BASELINE_RED if res.failed else None

    # -- one mutant
    def run_one(self, m: Mutant) -> Outcome:
        started = time.monotonic()
        out = self._run_one(m)
        out.seconds = round(time.monotonic() - started, 1)
        return out

    def _run_one(self, m: Mutant) -> Outcome:
        outside = escapes_root(self.root, m)
        if outside:
            return Outcome(m.id, ESCAPES_ROOT, "INVALID", outside)
        why = applicability(self.root, m)
        if why:
            return Outcome(m.id, INAPPLICABLE, "INVALID", why)
        base = self.baseline(m)
        if base:
            return Outcome(m.id, base, "INVALID",
                           "unmutated tests are not a usable control: " + base)
        target = self.root / m.file
        original = target.read_bytes()
        mutated = original.replace(m.find.encode("utf-8"), m.replace.encode("utf-8"))
        self._active = entry = self.journal.open_entry(target, original)
        try:
            write_bytes_durable(target, mutated)
            if sha256_of(target) == entry["sha256_original"]:
                raise RuntimeError("the mutation did not change the file")
            res = RUNNERS[m.runner](self.root, m.project, m.filter, self.timeout)
        finally:
            restore(entry)  # RestoreError propagates and leaves the journal entry in place
            self.journal.close_entry(entry)
            self._active = None
        return self._outcome(m, res)

    def _outcome(self, m: Mutant, res: RunResult) -> Outcome:
        problem = classify(res)
        if problem:
            return Outcome(m.id, problem, "INVALID", res.tail[-300:])
        failed = sorted(res.failed)
        if not failed:
            return Outcome(m.id, SURVIVED, verdict_for(m, SURVIVED))
        if m.killed_by and not any(k in t for t in failed for k in m.killed_by):
            return Outcome(m.id, KILLED_BY_OTHER, verdict_for(m, KILLED_BY_OTHER),
                           f"killed, but not by any of {m.killed_by}", failed[:10])
        return Outcome(m.id, KILLED, verdict_for(m, KILLED), failed_tests=failed[:10])

    def emergency_restore(self) -> None:
        if self._active:
            entry, self._active = self._active, None
            restore(entry)
            self.journal.close_entry(entry)


class Interrupted(BaseException):  # NOSONAR python:S5709
    """Raised from a signal handler so `finally` blocks run and the file is restored.

    Deliberately NOT an Exception: an ordinary `except Exception:` must not be able to swallow it,
    or the file stays mutated. That stranded mutation is the failure this harness exists to prevent."""


def install_signal_handlers(harness: Harness) -> None:
    def handler(signum, _frame):
        raise Interrupted(f"signal {signum}")

    for name in ("SIGINT", "SIGTERM", "SIGBREAK"):
        sig = getattr(signal, name, None)
        if sig is not None:
            signal.signal(sig, handler)
    atexit.register(harness.emergency_restore)


def run_all(harness: Harness, mutants: list[Mutant], max_seconds: float | None = None) -> list[Outcome]:
    harness.journal.require_empty()
    deadline = time.monotonic() + max_seconds if max_seconds is not None else None
    outcomes: list[Outcome] = []
    for m in mutants:
        if deadline is not None and time.monotonic() >= deadline:
            outcomes.append(Outcome(m.id, NO_RESULT, "INVALID", "time budget spent before this mutant started"))
            continue
        outcomes.append(harness.run_one(m))
    return outcomes


def exit_code(outcomes: list[Outcome]) -> int:
    if any(o.verdict == "INVALID" for o in outcomes):
        return EXIT_INVALID
    return EXIT_PROBLEM if any(o.verdict != "OK" for o in outcomes) else EXIT_OK


def render(outcomes: list[Outcome]) -> str:
    lines = [f"{'mutant':<44} {'status':<16} {'verdict':<18} secs"]
    for o in outcomes:
        lines.append(f"{o.id:<44} {o.status:<16} {o.verdict:<18} {o.seconds}")
        if o.verdict != "OK" and o.detail:
            lines.append(f"    {o.detail}")
    killed = sum(1 for o in outcomes if o.status == KILLED)
    lines.append(f"{killed}/{len(outcomes)} killed; "
                 f"{sum(1 for o in outcomes if o.verdict == 'OK')}/{len(outcomes)} as expected")
    return "\n".join(lines)


# ----------------------------------------------------------------------------- selection / CLI


def changed_files(root: Path, ref: str) -> set[str]:
    if not safe_git_ref(ref):
        raise ValueError(f"refusing git ref {ref!r}")
    # `ref` passed safe_git_ref above (never starts with a dash, so git cannot read it as an option)
    # and the argument vector is a list, never a shell string.
    out = subprocess.run(["git", "diff", "--name-only", f"{ref}...HEAD"], cwd=root,  # NOSONAR python:S8705
                         capture_output=True, text=True, check=True).stdout
    return {line.strip() for line in out.splitlines() if line.strip()}


def select(mutants: list[Mutant], only: list[str] | None, changed: set[str] | None) -> list[Mutant]:
    if only:
        unknown = set(only) - {m.id for m in mutants}
        if unknown:
            raise ValueError(f"unknown mutant id(s): {sorted(unknown)}")
        mutants = [m for m in mutants if m.id in only]
    if changed is not None:
        mutants = [m for m in mutants if changed & ({m.file} | set(m.watch))]
    return mutants


def ensure_clean(root: Path, mutants: list[Mutant]) -> None:
    """The saved original must be the committed one, not someone's unfinished edit."""
    files = sorted({m.file for m in mutants})
    if not files:
        return
    done = subprocess.run(["git", "status", "--porcelain", "--"] + files, cwd=root,
                          capture_output=True, text=True, check=False)
    if done.returncode != 0:
        raise JournalError("cannot verify the files are committed (is this a git checkout?); "
                           "pass --allow-dirty to accept the current content as the original")
    out = done.stdout
    if out.strip():
        raise JournalError("refusing to mutate files with uncommitted changes:\n" + out)


def default_state_dir(root: Path) -> Path:
    key = hashlib.sha1(str(Path(root).resolve()).encode()).hexdigest()[:12]
    return Path(tempfile.gettempdir()) / "hcs-mutation-harness" / key


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("command", choices=["run", "list", "check-manifest", "recover"])
    ap.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    ap.add_argument("--root", type=Path, default=REPO_ROOT)
    ap.add_argument("--state-dir", type=Path)
    ap.add_argument("--only", nargs="*")
    ap.add_argument("--changed", metavar="REF", help="only mutants whose file or watch paths differ from REF")
    ap.add_argument("--max-seconds", type=float, help="stop STARTING mutants after this; never interrupts one")
    ap.add_argument("--timeout", type=float, default=1800.0, help="per test run, seconds")
    ap.add_argument("--report", type=Path)
    ap.add_argument("--allow-dirty", action="store_true",
                    help="skip the committed-original check (the saved original is whatever is on disk)")
    args = ap.parse_args(argv)

    journal = Journal(args.state_dir or default_state_dir(args.root))
    try:
        if args.command == "recover":
            recover(journal)
            return EXIT_OK
        if not within_root(args.root, args.manifest):
            raise ValueError(f"manifest {str(args.manifest)!r} resolves outside the repository root")
        if args.report and not within_root(args.root, args.report):
            raise ValueError(f"report path {str(args.report)!r} resolves outside the repository root")
        mutants = select(load_manifest(args.manifest), args.only,
                         changed_files(args.root, args.changed) if args.changed else None)
        if args.command == "list":
            for m in mutants:
                print(f"{m.id}\t{m.expect}\t{m.file}")
            return EXIT_OK
        if args.command == "check-manifest":
            bad = [(m.id, applicability(args.root, m)) for m in mutants]
            bad = [(i, w) for i, w in bad if w]
            for i, w in bad:
                print(f"STALE {i}: {w}")
            print(f"{len(mutants) - len(bad)}/{len(mutants)} mutants still apply")
            return EXIT_INVALID if bad else EXIT_OK
        escaping = [(m.id, escapes_root(args.root, m)) for m in mutants]
        escaping = [(i, w) for i, w in escaping if w]
        for i, w in escaping:
            print(f"ESCAPES_ROOT {i}: {w}", file=sys.stderr)
        if escaping:
            return EXIT_INVALID
        journal.require_empty()
        if not args.allow_dirty:
            ensure_clean(args.root, mutants)
        harness = Harness(args.root, journal, args.timeout)
        install_signal_handlers(harness)
        outcomes = run_all(harness, mutants, args.max_seconds)
    except ValueError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return EXIT_INVALID
    except JournalError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return EXIT_JOURNAL
    except RestoreError as exc:
        print(f"RESTORE FAILED: {exc}\nRun `mutation-harness.py recover` before touching the tree.",
              file=sys.stderr)
        return EXIT_RESTORE
    print(render(outcomes))
    if args.report:
        # Contained: within_root was checked before the run started.
        with open(Path(args.root) / args.report, "w", encoding="utf-8", newline="\n") as fh:  # NOSONAR python:S8707
            json.dump([o.__dict__ for o in outcomes], fh, indent=2)
            fh.write("\n")
    return exit_code(outcomes)


if __name__ == "__main__":
    sys.exit(main())
