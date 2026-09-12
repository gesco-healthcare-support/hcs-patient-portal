"""The fail-fast guards, the summarisers, and the blind-spot detector.

These are the parts that decide whether the gate can pass WITHOUT MEASURING
ANYTHING, which is the failure the enforcement phase exists to remove. They are
pinned here so that softening one breaks a test by name rather than quietly
widening what the gate lets through.

One characterization test records a defect rather than fixing it -- see
`test_an_existing_but_EMPTY_report_is_rejected`.
"""

from __future__ import annotations

import contextlib
import io
import tempfile
import unittest
from pathlib import Path

from gate_loader import gate


@contextlib.contextmanager
def dying():
    """Assert the gate exits, and SWALLOW the `::error::` it prints on the way.

    `die()` writes a GitHub Actions workflow command to STDOUT, and Actions
    parses those into annotations. Without this, the five guard tests below
    stamp five red `failure` annotations onto every GREEN run of `Python: Test`
    -- measured on the first CI run of #825, which reported 6 annotations, 5 at
    failure level, while passing.

    A check that displays errors while succeeding is the always-red signal this
    programme exists to remove, so the tests must not manufacture one. pytest
    captures stdout by default; stdlib unittest does not, so it is done here.
    """
    with contextlib.redirect_stdout(io.StringIO()):
        yield


def _patterns(*globs):
    return [gate.glob_to_regex(g) for g in globs]


class TestRequireReport(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.tmp_path = Path(self._tmp.name)
        self.addCleanup(self._tmp.cleanup)

    def test_a_missing_report_exits_non_zero(self):
        """A skipped job reports Success; an absent report must NOT pass."""
        with dying(), self.assertRaises(SystemExit) as cm:
            gate.require_report(self.tmp_path / "nope.xml", "backend")
        self.assertEqual(cm.exception.code, 1)

    def test_an_existing_but_EMPTY_report_is_rejected(self):
        """CHARACTERIZATION -- pins current behaviour, does NOT endorse it.

        Empty is the right rejection for a coverage report: an empty one means
        the suite produced nothing. It is the WRONG rejection for a diff, where
        empty legitimately means the submission changes nothing -- which is why
        `load_changed_diff` deliberately does not call this helper.

        This is a recorded defect. It is pinned, not corrected, because the
        phase's change class forbids folding a behaviour change into a coverage
        commit. If this test starts failing, the fix landed somewhere -- confirm
        it was intended before updating the assertion.
        """
        empty = self.tmp_path / "empty.xml"
        empty.write_text("", encoding="utf-8")
        with dying(), self.assertRaises(SystemExit) as cm:
            gate.require_report(empty, "backend")
        self.assertEqual(cm.exception.code, 1)

    def test_a_non_empty_report_is_accepted(self):
        report = self.tmp_path / "ok.xml"
        report.write_text("<coverage/>", encoding="utf-8")
        self.assertIsNone(gate.require_report(report, "backend"))


class TestRequireFloor(unittest.TestCase):
    def test_an_unset_floor_exits_rather_than_passing(self):
        """An unconfigured threshold is a check nobody finished wiring."""
        with dying(), self.assertRaises(SystemExit):
            gate.require_floor(None, "backend")

    def test_a_blank_floor_exits(self):
        with dying(), self.assertRaises(SystemExit):
            gate.require_floor("   ", "backend")

    def test_a_non_numeric_floor_exits(self):
        with dying(), self.assertRaises(SystemExit):
            gate.require_floor("ninety", "backend")

    def test_a_valid_floor_is_returned_as_a_float(self):
        self.assertEqual(gate.require_floor("72", "backend"), 72.0)
        self.assertEqual(gate.require_floor("90.5", "changed"), 90.5)

    def test_zero_is_a_legitimate_value_and_is_NOT_treated_as_unset(self):
        """A floor of 0 is a real, deliberate setting -- not an unset one.

        The rationale first written here was WRONG, and seen-to-fail exposed it:
        it claimed "`0` is falsy", but argparse hands this function the STRING
        `"0"`, and a non-empty string is truthy. So a plain truthiness check
        cannot reject it and the test guarded nothing against that mutation.

        The mutation it does guard against is numeric coercion --
        `not float(value)`, or `float(value) == 0` -- which is a plausible
        refactor ("reject an empty or zero floor") and would reject a floor
        somebody set to 0 on purpose.
        """
        self.assertEqual(gate.require_floor("0", "backend"), 0.0)
        self.assertEqual(gate.require_floor("0.0", "frontend"), 0.0)


class TestSummarise(unittest.TestCase):
    def test_counts_lines_files_and_hits(self):
        per_file = {"a.cs": {1: 1, 2: 0, 3: 4}, "b.cs": {1: 0}}
        self.assertEqual(gate.summarise(per_file, []), (4, 2, 2))

    def test_EXCLUDED_files_are_removed_from_both_sides(self):
        """THE DECOY IS LOAD-BEARING.

        The fixture deliberately contains a file the pattern MUST remove. Built
        without it, this test asserts only what `summarise` keeps and would pass
        with the exclusion check deleted -- a negative guarantee cannot be
        proven against a fixture that lacks the thing being excluded.
        """
        per_file = {
            "src/App/Real.cs": {1: 1, 2: 0},
            "src/App/Migrations/Init.cs": {1: 0, 2: 0, 3: 0, 4: 0},  # the decoy
        }
        found, hit, files = gate.summarise(per_file, _patterns("**/Migrations/**"))
        self.assertEqual(files, 1, "the excluded file still reached the file count")
        self.assertEqual(found, 2, "the excluded file's lines still reached the denominator")
        self.assertEqual(hit, 1)

    def test_an_empty_map_yields_zeroes(self):
        self.assertEqual(gate.summarise({}, []), (0, 0, 0))


class TestUnmeasuredChanged(unittest.TestCase):
    def test_flags_a_changed_source_file_the_report_never_mentions(self):
        per_file = {"angular/src/app/seen.ts": {1: 1}}
        changed = {"angular/src/app/seen.ts": {1}, "angular/src/app/unseen.ts": {5}}
        self.assertEqual(
            gate.unmeasured_changed(per_file, changed, []), ["angular/src/app/unseen.ts"]
        )

    def test_scopes_itself_by_the_extensions_the_report_uses(self):
        """Self-configuring per stack: this is why `.py` enters the check for free."""
        per_file = {"scripts/gate.py": {1: 1}}
        changed = {"docs/README.md": {1}, "scripts/other.py": {1}, "ci.yml": {2}}
        self.assertEqual(gate.unmeasured_changed(per_file, changed, []), ["scripts/other.py"])

    def test_an_excluded_file_is_not_flagged(self):
        per_file = {"src/App/Real.cs": {1: 1}}
        changed = {"src/App/Migrations/New.cs": {1}}
        self.assertEqual(
            gate.unmeasured_changed(per_file, changed, _patterns("**/Migrations/**")), []
        )

    def test_a_report_with_no_extensions_flags_nothing(self):
        """No report means no scope; guessing one would flag the whole diff."""
        self.assertEqual(gate.unmeasured_changed({}, {"a.cs": {1}}, []), [])


if __name__ == "__main__":
    unittest.main()
