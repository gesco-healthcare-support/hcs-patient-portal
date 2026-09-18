"""The --per-file breakdown.

WHAT THESE PIN IS THE DENOMINATOR, not the formatting. The output exists to
replace throwaway ranking scripts that applied their own idea of what counts,
and a ranking that disagrees with the gate about the counted set sends work at
files the gate does not grade. So the load-bearing assertion here is that the
emitted set is EXACTLY the set `summarise` counts -- asserted against
`summarise` itself rather than against a number written down beside it, because
a hardcoded expectation drifts the moment the exclusion list changes and nothing
reports it.

The ordering tests look cosmetic and are not: the output is regenerated and
compared across runs, so an unstable sort turns every regeneration into a noisy
diff that hides the real change.
"""

from __future__ import annotations

import contextlib
import io
import json
import sys
import tempfile
import unittest
import unittest.mock
from pathlib import Path

from gate_loader import gate

# Two counted files and one that the proxy exclusion must remove. The proxy file
# is given UNCOVERED lines deliberately: if it leaked into the output it would
# rank at the top, which is the loudest possible way for this to fail.
LCOV = """SF:src/app/a.component.ts
DA:1,1
DA:2,0
DA:3,0
end_of_record
SF:src/app/b.component.ts
DA:1,1
DA:2,0
end_of_record
SF:src/app/proxy/generated.ts
DA:1,0
DA:2,0
DA:3,0
DA:4,0
end_of_record
"""

# Same uncovered count on both files, so the tie-break is what decides the
# order. Named z before a in the report to prove the sort is not just echoing
# insertion order.
LCOV_TIED = """SF:src/app/z.component.ts
DA:1,0
end_of_record
SF:src/app/a.component.ts
DA:1,0
end_of_record
"""

# One line repeated across records with the HIGHER hit count second, so
# last-wins and max disagree. The parser resolves this; the breakdown inherits
# it rather than reimplementing it, and that inheritance is what is pinned.
LCOV_REPEATED = """SF:src/app/a.component.ts
DA:1,0
DA:1,3
DA:2,0
end_of_record
"""


def patterns():
    """The one exclusion that can match an Angular lcov path."""
    return [gate.glob_to_regex("angular/src/app/proxy/**")]


def run_main(extra_argv, tmp):
    """Drive main() with a patched argv and return (exit code, stdout).

    ONE copy, used by both test classes below. It was two identical copies until
    SonarCloud pointed out the second -- the duplicate carried its own
    `except SystemExit`, so the copy-paste showed up as a second finding rather
    than as duplicated code, which is a roundabout way to be told.

    The except is NOT avoidable here and is the accepted false positive in this
    file: main() RETURNS a code on success and RAISES on die(), so capturing
    both paths needs the catch. assertRaises cannot express "may or may not
    raise, tell me the code either way".
    """
    report = Path(tmp) / "lcov.info"
    report.write_text(LCOV, encoding="utf-8")
    manifest = Path(tmp) / "tracked.txt"
    manifest.write_text("angular/src/app/a.component.ts\n"
                        "angular/src/app/b.component.ts\n", encoding="utf-8")
    argv = ["--lcov", str(report), "--lcov-prefix", "angular",
            "--exclusions", ".coverage-exclusions",
            "--tracked-files", str(manifest)] + extra_argv
    buf = io.StringIO()
    original = sys.argv
    sys.argv = ["coverage-gate.py"] + argv
    try:
        with contextlib.redirect_stdout(buf):
            try:
                code = gate.main()
            except SystemExit as exc:
                code = exc.code
    finally:
        sys.argv = original
    return code, buf.getvalue()


class WritePerFile(unittest.TestCase):
    def emit(self, lcov_text=LCOV, prefix="angular"):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            report = root / "lcov.info"
            report.write_text(lcov_text, encoding="utf-8")
            out = root / "per-file.json"
            per_file = gate.parse_lcov(report, prefix)
            count = gate.write_per_file(str(out), per_file, patterns())
            return json.loads(out.read_text(encoding="utf-8")), count, per_file

    def test_one_object_per_counted_file(self):
        rows, count, _ = self.emit()
        self.assertEqual(count, 2)
        self.assertEqual([r["path"] for r in rows],
                         ["angular/src/app/a.component.ts", "angular/src/app/b.component.ts"])

    def test_every_row_carries_the_four_fields(self):
        rows, _, _ = self.emit()
        for row in rows:
            self.assertEqual(set(row), {"path", "found", "hit", "uncovered"})

    def test_uncovered_is_found_minus_hit(self):
        rows, _, _ = self.emit()
        for row in rows:
            self.assertEqual(row["uncovered"], row["found"] - row["hit"])

    def test_the_counts_are_right(self):
        rows, _, _ = self.emit()
        first = rows[0]
        self.assertEqual((first["found"], first["hit"], first["uncovered"]), (3, 1, 2))

    # THE LOAD-BEARING ONE. Asserted against summarise() rather than against a
    # literal, so the two cannot drift apart without a test failing.
    def test_the_emitted_set_is_exactly_what_summarise_counts(self):
        rows, count, per_file = self.emit()
        found, hit, files = gate.summarise(per_file, patterns())
        self.assertEqual(count, files)
        self.assertEqual(sum(r["found"] for r in rows), found)
        self.assertEqual(sum(r["hit"] for r in rows), hit)

    def test_an_excluded_file_is_omitted_even_though_it_would_rank_first(self):
        rows, _, _ = self.emit()
        self.assertNotIn("angular/src/app/proxy/generated.ts", [r["path"] for r in rows])
        # The positive control: without the exclusion it WOULD be there, and
        # first. Without this, the assertion above passes on an empty list.
        with tempfile.TemporaryDirectory() as tmp:
            report = Path(tmp) / "lcov.info"
            report.write_text(LCOV, encoding="utf-8")
            out = Path(tmp) / "out.json"
            gate.write_per_file(str(out), gate.parse_lcov(report, "angular"), [])
            unfiltered = json.loads(out.read_text(encoding="utf-8"))
        self.assertEqual(unfiltered[0]["path"], "angular/src/app/proxy/generated.ts")

    def test_ranked_by_uncovered_descending(self):
        rows, _, _ = self.emit()
        self.assertEqual([r["uncovered"] for r in rows], [2, 1])

    def test_ties_are_broken_by_path(self):
        rows, _, _ = self.emit(LCOV_TIED)
        self.assertEqual([r["path"] for r in rows],
                         ["angular/src/app/a.component.ts", "angular/src/app/z.component.ts"])

    def test_two_runs_over_one_report_are_byte_identical(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            report = root / "lcov.info"
            report.write_text(LCOV, encoding="utf-8")
            first, second = root / "1.json", root / "2.json"
            gate.write_per_file(str(first), gate.parse_lcov(report, "angular"), patterns())
            gate.write_per_file(str(second), gate.parse_lcov(report, "angular"), patterns())
            self.assertEqual(first.read_bytes(), second.read_bytes())

    def test_the_lcov_prefix_is_already_applied(self):
        rows, _, _ = self.emit()
        # Not "src/app/a.component.ts". Without the prefix the exclusion cannot
        # match and the proxy file would be graded as ours.
        self.assertTrue(all(r["path"].startswith("angular/") for r in rows))

    def test_a_repeated_line_takes_the_maximum_hit_count(self):
        rows, _, _ = self.emit(LCOV_REPEATED)
        # Line 1 is covered (hit 3 beats hit 0), line 2 is not.
        self.assertEqual((rows[0]["found"], rows[0]["hit"]), (2, 1))

    def test_an_empty_map_writes_an_empty_array(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "per-file.json"
            count = gate.write_per_file(str(out), {}, patterns())
            self.assertEqual(count, 0)
            self.assertEqual(json.loads(out.read_text(encoding="utf-8")), [])


class WiredIntoMain(unittest.TestCase):
    """main() must actually call it. Without these, the flag can be parsed and
    silently ignored and every test above still passes."""

    def _run(self, extra_argv, tmp):
        return run_main(extra_argv, tmp)

    def test_the_flag_writes_the_file_in_measure_only_mode(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "per-file.json"
            code, output = self._run(["--measure-only", "--per-file", str(out)], tmp)
            self.assertEqual(code, 0)
            self.assertTrue(out.is_file())
            self.assertIn("per-file: wrote", output)

    def test_the_flag_also_writes_while_gating(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "per-file.json"
            code, _ = self._run(["--floor-frontend", "0", "--per-file", str(out)], tmp)
            self.assertEqual(code, 0)
            self.assertTrue(out.is_file())

    # The complement, and the reason the two above are not enough on their own:
    # a flag that always wrote would pass both.
    def test_without_the_flag_nothing_is_written(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "per-file.json"
            code, output = self._run(["--measure-only"], tmp)
            self.assertEqual(code, 0)
            self.assertFalse(out.exists())
            self.assertNotIn("per-file:", output)


class DiagnosticsEmitAfterTheVerdict(unittest.TestCase):
    """GATES EVALUATE BEFORE DIAGNOSTICS EMIT.

    The emit sat above the floor loop until the review of #975. That placement,
    not the unhandled write, was the real defect: a mistyped --per-file path
    failed BEFORE the floor was evaluated, so a reporting flag could destroy the
    answer the gate exists to give. Both the reviewer and SonarCloud stopped at
    the missing try and neither named the ordering.

    Pinning the ORDER rather than just the outcome, because the outcome is the
    same either way on a healthy run -- the difference only shows when the write
    fails, which is exactly when nobody is looking.
    """

    def _run(self, extra_argv, tmp):
        return run_main(extra_argv, tmp)

    def test_the_floor_verdict_is_printed_before_the_write_fails(self):
        # The ordering assertion. If the emit moves back above the floor loop,
        # the VERDICT line is absent and this fails.
        #
        # Keyed on "-> PASS" and not on "frontend:", and that distinction is the
        # whole test. The first version asserted on the label and was VACUOUS:
        # measure_stack prints `frontend: measured ...` during measurement,
        # which happens before EITHER placement, so the assertion held with the
        # emit moved back where the review rejected it. Caught by running the
        # probe rather than by reading it. "-> PASS" is printed only by
        # report(), which runs only inside the floor loop.
        with tempfile.TemporaryDirectory() as tmp:
            bad = Path(tmp) / "nope" / "per-file.json"
            code, out = self._run(
                ["--floor-frontend", "0", "--per-file", str(bad)], tmp)
        self.assertEqual(code, 1)
        self.assertIn("-> PASS", out)
        self.assertIn("--per-file was given", out)
        self.assertLess(out.index("-> PASS"), out.index("--per-file was given"))

    def test_a_failing_write_cannot_turn_a_pass_into_a_silent_loss_of_the_verdict(self):
        # The verdict is still readable from the log even though the run died,
        # which is the whole point of computing it first.
        with tempfile.TemporaryDirectory() as tmp:
            bad = Path(tmp) / "nope" / "per-file.json"
            _code, out = self._run(
                ["--floor-frontend", "0", "--per-file", str(bad)], tmp)
        self.assertIn("-> PASS", out)

    def test_the_breakdown_is_written_on_a_FAILING_run_too(self):
        # A failing floor is exactly when someone wants to know which files are
        # dragging the figure down, so the report must not be skipped.
        with tempfile.TemporaryDirectory() as tmp:
            out_path = Path(tmp) / "per-file.json"
            code, out = self._run(
                ["--floor-frontend", "99", "--per-file", str(out_path)], tmp)
            self.assertTrue(out_path.is_file())
        self.assertEqual(code, 1)
        self.assertIn("-> FAIL", out)

    def test_the_exit_code_is_the_floors_verdict_not_the_writes(self):
        with tempfile.TemporaryDirectory() as tmp:
            out_path = Path(tmp) / "per-file.json"
            code, _ = self._run(
                ["--floor-frontend", "0", "--per-file", str(out_path)], tmp)
        self.assertEqual(code, 0)


class TheOutputPathIsValidated(unittest.TestCase):
    """A bad --per-file path must fail the way everything else in this script
    fails: through die(), with a sentence saying what was wrong.

    Found twice, by two routes, which is why it is pinned rather than trusted:
    the reviewer flagged the bare write_text as a missing-parent sharp edge, and
    SonarCloud flagged the same line as a CLI argument reaching a file write.
    The same rule already fires three times on this file on main, because every
    path this gate touches arrives from a flag -- so the thing worth asserting
    is not that the path is untainted, it is that a bad one is EXPLAINED.
    """

    def _dies(self, destination):
        """Run the write and capture stdout, so the `::error::` cannot reach
        Actions as an annotation on a green run -- and so the message survives
        to be asserted.
        """
        buf = io.StringIO()
        # patterns() is hoisted OUT of the assertRaises block. Left inside, the
        # block contains two invocations and the assertion no longer says which
        # one was expected to throw -- if patterns() ever raised, this would
        # pass for the wrong reason.
        pats = patterns()
        with contextlib.redirect_stdout(buf):
            with self.assertRaises(SystemExit) as caught:
                gate.write_per_file(destination, {}, pats)
        return caught.exception.code, buf.getvalue()

    def test_a_missing_parent_directory_is_explained_not_a_traceback(self):
        with tempfile.TemporaryDirectory() as tmp:
            target = Path(tmp) / "nope" / "per-file.json"
            code, out = self._dies(str(target))
        self.assertEqual(code, 1)
        self.assertIn("parent directory", out)
        self.assertIn("does not exist", out)

    def test_it_refuses_to_create_the_directory_for_you(self):
        # Stated as its own test because the alternative is defensible and was
        # rejected: silently creating the tree hides a mistyped path until
        # someone goes looking for a report written somewhere else.
        with tempfile.TemporaryDirectory() as tmp:
            target = Path(tmp) / "nope" / "per-file.json"
            self._dies(str(target))
            self.assertFalse((Path(tmp) / "nope").exists())

    def test_an_existing_directory_as_the_destination_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            code, out = self._dies(tmp)
        self.assertEqual(code, 1)
        self.assertIn("existing", out)

    def test_a_write_that_fails_is_explained_rather_than_raised(self):
        """A path that validates but cannot be written must still die() with a
        message, not escape as an OSError traceback.

        FORCED rather than provoked, and deliberately so. The faithful version
        is a read-only file, but whether the filesystem enforces that depends on
        the runner -- as root it does not, and the test would SKIP. A skipped
        test covers no lines, which is exactly what the changed-lines floor is
        asking for here. Determinism wins over faithfulness for a branch whose
        only job is to route an OSError into die().
        """
        with tempfile.TemporaryDirectory() as tmp:
            # Everything but the call under test is hoisted out of the
            # assertRaises block, so exactly one invocation inside it can throw.
            destination = str(Path(tmp) / "per-file.json")
            pats = patterns()
            buf = io.StringIO()
            with unittest.mock.patch.object(
                Path, "write_text", side_effect=OSError("no space left on device")
            ):
                with contextlib.redirect_stdout(buf):
                    with self.assertRaises(SystemExit) as caught:
                        gate.write_per_file(destination, {}, pats)
        self.assertEqual(caught.exception.code, 1)
        self.assertIn("could not write the per-file breakdown", buf.getvalue())
        self.assertIn("no space left on device", buf.getvalue())

    # THE POSITIVE CONTROL. Without it the three above pass with the validation
    # written as an unconditional die(), which would break the flag entirely.
    def test_a_good_path_still_writes(self):
        with tempfile.TemporaryDirectory() as tmp:
            target = Path(tmp) / "per-file.json"
            count = gate.write_per_file(str(target), {}, patterns())
        self.assertEqual(count, 0)


if __name__ == "__main__":
    unittest.main()
