"""The tracked-file check must run whether or not the workflow asks it to (#683).

#856 made the check correct. This file covers the two things that survive from
the parallel implementation on #860, per Adrian's review of it:

1. that `main()` actually CALLS the guard -- a guard that exists and is never
   invoked is the same as no guard, and that is invisible to a unit test of the
   guard itself. Deleting the call from `main()` leaves every test of `in_repo`
   and `assert_tracked` green.
2. that discovery happens inside the script when no `--tracked-files` is given,
   and that a git failure EXITS rather than being absorbed.

The second replaced a branch that printed "check SKIPPED" and carried on. A
stated skip and a silent one both end with the check not running, and #683 is
about contamination that arrives with no signal.
"""

import contextlib
import io
import os
import pathlib
import subprocess
import sys
import tempfile
import unittest

from gate_loader import gate

COBERTURA = (
    '<?xml version="1.0"?><coverage><sources><source>.</source></sources><packages>'
    '<package name="p"><classes>'
    '<class filename="{path}" name="x"><lines>'
    '<line number="1" hits="1"/><line number="2" hits="0"/>'
    '</lines></class>'
    '</classes></package></packages></coverage>'
)


class DiscoverTrackedTests(unittest.TestCase):
    """`git ls-files`, called by the gate rather than by the workflow."""

    def test_finds_this_repository_own_files(self):
        tracked = gate.discover_tracked()
        self.assertIn("scripts/coverage-gate.py", tracked)
        self.assertIn(".coverage-exclusions", tracked)
        self.assertGreater(len(tracked), 500)

    def test_the_result_satisfies_in_repo_for_an_absolute_runner_path(self):
        # The shape that mattered: a file of OURS arriving with a runner prefix.
        # Comparing by equality reported 925 of 925 untracked; this pins that
        # discovery and matching agree on a real path from a real report.
        tracked = gate.discover_tracked()
        runner = ("/home/runner/work/hcs-patient-portal/hcs-patient-portal/"
                  "scripts/coverage-gate.py")
        self.assertTrue(gate.in_repo(runner, tracked))

    def test_a_git_failure_exits_rather_than_returning_an_empty_set(self):
        # Run from a directory that is not a work tree so git genuinely fails --
        # no stub of subprocess, because the failure path is the whole point.
        # An empty set would fail every file; absorbing the error would disable
        # the guard exactly when it cannot be evaluated.
        original = os.getcwd()
        buf = io.StringIO()
        with tempfile.TemporaryDirectory() as tmp:
            os.chdir(tmp)
            try:
                with contextlib.redirect_stdout(buf):
                    with self.assertRaises(SystemExit) as caught:
                        gate.discover_tracked()
            finally:
                os.chdir(original)
        self.assertEqual(caught.exception.code, 1)
        self.assertIn("git ls-files", buf.getvalue())

    def test_an_EMPTY_list_exits_rather_than_failing_every_file(self):
        # git SUCCEEDING and returning nothing is a DIFFERENT failure from git
        # failing, and the more dangerous of the two. An empty set makes
        # in_repo() false for every counted file, so the gate would report the
        # entire report as contaminated -- 925 of 925, the exact shape of the
        # false positive this branch was rebuilt to remove. Burying the real
        # signal under a wall of false ones is worse than not checking.
        #
        # Reproduced with a real empty repository rather than a subprocess
        # stub, for the same reason as the test above: the point is what git
        # actually does, not what a stub is told to say.
        original = os.getcwd()
        buf = io.StringIO()
        with tempfile.TemporaryDirectory() as tmp:
            subprocess.run(["git", "init", "--quiet", tmp],
                           check=True, capture_output=True)
            os.chdir(tmp)
            try:
                with contextlib.redirect_stdout(buf):
                    with self.assertRaises(SystemExit) as caught:
                        gate.discover_tracked()
            finally:
                os.chdir(original)
        self.assertEqual(caught.exception.code, 1)
        self.assertIn("returned nothing", buf.getvalue())

class MainWiringTests(unittest.TestCase):
    """That the guard is reached at all.

    `in_repo` and `assert_tracked` are unit-tested by #856. Nothing there fails
    if `main()` stops calling them, which is the gap this closes.
    """

    def _run(self, counted_path, tracked_paths=None):
        """Run main() over a one-file report; returns (exit code, stdout)."""
        with tempfile.TemporaryDirectory() as tmp:
            root = pathlib.Path(tmp)
            report = root / "coverage.xml"
            report.write_text(COBERTURA.format(path=counted_path), encoding="utf-8")
            argv = ["--cobertura", str(report), "--floor-backend", "0",
                    "--exclusions", ".coverage-exclusions"]
            if tracked_paths is not None:
                manifest = root / "tracked.txt"
                manifest.write_text("\n".join(tracked_paths), encoding="utf-8")
                argv += ["--tracked-files", str(manifest)]
            # main() reads sys.argv rather than taking one, so argv is patched
            # rather than changing the production signature -- the point of this
            # file is that main() reaches the guard, not how it parses.
            buf = io.StringIO()
            code = 0
            original_argv = sys.argv
            sys.argv = ["coverage-gate.py"] + argv
            try:
                with contextlib.redirect_stdout(buf):
                    try:
                        code = gate.main()
                    except SystemExit as exc:
                        code = exc.code
            finally:
                sys.argv = original_argv
            return code, buf.getvalue()

    def test_a_tracked_file_is_measured_normally(self):
        code, out = self._run("scripts/thing.py", ["scripts/thing.py"])
        self.assertEqual(code, 0)
        self.assertIn("backend: measured", out)

    def test_an_untracked_file_stops_the_run(self):
        # The wiring assertion. If main() ever stops calling assert_tracked,
        # this is the only test that notices.
        code, out = self._run("/home/runner/work/Vendor/src/X.cs", ["scripts/thing.py"])
        self.assertEqual(code, 1)
        self.assertIn("not tracked in this repository", out)

    def test_the_check_runs_with_NO_manifest_supplied(self):
        # The branch that used to print "check SKIPPED" and pass. Discovery now
        # falls back to git, so omitting the flag cannot disable the guard.
        code, out = self._run("/home/runner/work/Vendor/src/X.cs")
        self.assertEqual(code, 1)
        self.assertIn("not tracked in this repository", out)

    def test_no_manifest_still_accepts_our_own_files(self):
        # The complement: the fallback must not fail everything, which is the
        # failure mode a wrong comparison produced at 925 of 925.
        code, out = self._run("scripts/coverage-gate.py")
        self.assertEqual(code, 0)
        self.assertIn("backend: measured", out)


if __name__ == "__main__":
    unittest.main()
