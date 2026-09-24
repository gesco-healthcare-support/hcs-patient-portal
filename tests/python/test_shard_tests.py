"""Splitting the EF Core test assembly across CI jobs (#1032), and proving no class was lost.

A partition that silently drops a class is the failure this script exists to prevent: a test
that no shard's filter matches never runs, and nothing reports it. So every property the CI
relies on is asserted here -- the shards are exhaustive, disjoint, deterministic and balanced,
an empty shard is refused (an empty filter would run EVERY test), and `verify` fails when the
classes a shard executed differ from the classes it was given, in either direction.
"""

from __future__ import annotations

import io
import tempfile
import unittest
import xml.etree.ElementTree as ET
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path

from gate_loader import load

shard = load("shard_tests", "scripts/shard-tests.py")

LISTING = """\
  Determining projects to restore...
Test run for /w/test/X.Tests/bin/Release/net10.0/X.Tests.dll (.NETCoreApp,Version=v10.0)
VSTest version 17.14.1 (x64)

The following Tests are available:
    Ns.Alpha.One
    Ns.Alpha.Two
    Ns.Alpha.Three
    Ns.Beta.One(status: Pending)
    Ns.Beta.One(status: Sent)
    Ns.Gamma.Reads_an_email(email: "a.b@example.test")
    Ns.Deep.Delta.One
"""


def _trx(*class_outcomes: tuple[str, str]) -> str:
    """A minimal .trx carrying one TestMethod per (className, outcome)."""
    units = "".join(
        f'<UnitTest name="t{i}" id="id{i}"><TestMethod className="{cls}" name="t{i}" /></UnitTest>'
        for i, (cls, _) in enumerate(class_outcomes)
    )
    results = "".join(
        f'<UnitTestResult testId="id{i}" testName="t{i}" outcome="{outcome}" />'
        for i, (_, outcome) in enumerate(class_outcomes)
    )
    return (
        '<?xml version="1.0" encoding="utf-8"?>'
        '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
        f"<Results>{results}</Results><TestDefinitions>{units}</TestDefinitions></TestRun>"
    )


class _TempDirCase(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.tmp = Path(self._tmp.name)
        self.addCleanup(self._tmp.cleanup)

    def write(self, name: str, text: str) -> Path:
        path = self.tmp / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        return path


class TestParseListing(unittest.TestCase):
    def test_reads_only_the_indented_names_after_the_marker(self):
        names = shard.parse_listing(LISTING)

        self.assertEqual(len(names), 7)
        self.assertEqual(names[0], "Ns.Alpha.One")
        self.assertNotIn("Determining projects to restore...", names)

    def test_no_marker_means_no_names(self):
        self.assertEqual(shard.parse_listing("Build succeeded.\n    Ns.Alpha.One\n"), [])


class TestClassOf(unittest.TestCase):
    def test_a_plain_name_drops_the_method(self):
        self.assertEqual(shard.class_of("Ns.Deep.Delta.One"), "Ns.Deep.Delta")

    def test_theory_arguments_are_ignored_even_when_they_contain_dots(self):
        self.assertEqual(shard.class_of('Ns.Gamma.Reads_an_email(email: "a.b@example.test")'), "Ns.Gamma")

    def test_a_name_with_no_dot_is_refused(self):
        with self.assertRaises(ValueError):
            shard.class_of("NoDotHere")


class TestPartition(unittest.TestCase):
    COUNTS = {f"Ns.C{i:02d}": (i % 7) + 1 for i in range(40)}

    def test_every_class_lands_in_exactly_one_shard(self):
        shards = shard.partition(self.COUNTS, 4)

        flat = [cls for bucket in shards for cls in bucket]
        self.assertEqual(sorted(flat), sorted(self.COUNTS))
        self.assertEqual(len(flat), len(set(flat)))

    def test_the_result_does_not_depend_on_input_order(self):
        items = list(self.COUNTS.items())
        expected = shard.partition(self.COUNTS, 4)

        # Fixed reorderings rather than a shuffle: reproducible, and they move every class.
        for reordered in (items[::-1], items[1::2] + items[::2]):
            self.assertEqual(shard.partition(dict(reordered), 4), expected)

    def test_shards_differ_by_at_most_the_heaviest_class(self):
        shards = shard.partition(self.COUNTS, 4)

        loads = [sum(self.COUNTS[c] for c in bucket) for bucket in shards]
        self.assertLessEqual(max(loads) - min(loads), max(self.COUNTS.values()))

    def test_fewer_classes_than_shards_is_refused(self):
        """An empty shard would get an empty filter, and an empty filter runs EVERY test."""
        with self.assertRaises(ValueError):
            shard.partition({"Ns.A": 1, "Ns.B": 1}, 4)

    def test_zero_shards_is_refused(self):
        with self.assertRaises(ValueError):
            shard.partition(self.COUNTS, 0)


class TestFilterFor(unittest.TestCase):
    def test_each_class_is_matched_up_to_its_trailing_dot(self):
        """The trailing dot stops `Ns.Alpha` from also matching `Ns.AlphaBeta`."""
        self.assertEqual(
            shard.filter_for(["Ns.Beta", "Ns.Alpha"]),
            "FullyQualifiedName~Ns.Alpha.|FullyQualifiedName~Ns.Beta.",
        )

    def test_an_empty_class_list_is_refused(self):
        with self.assertRaises(ValueError):
            shard.filter_for([])


class TestRunsettingsFor(unittest.TestCase):
    def test_carries_the_filter_and_fails_a_shard_that_matches_nothing(self):
        root = ET.fromstring(shard.runsettings_for(["Ns.Beta", "Ns.Alpha"]))

        self.assertEqual(root.findtext("RunConfiguration/TestCaseFilter"), shard.filter_for(["Ns.Alpha", "Ns.Beta"]))
        self.assertEqual(root.findtext("RunConfiguration/TreatNoTestsAsError"), "true")

    def test_an_empty_class_list_is_refused(self):
        with self.assertRaises(ValueError):
            shard.runsettings_for([])


SLNX = """<Solution>
  <Folder Name="/src/">
    <Project Path="src/App/App.csproj" />
  </Folder>
  <Folder Name="/test/">
    <Project Path="test\\Domain.Tests\\Domain.Tests.csproj" />
    <Project Path="test/Ef.Tests/Ef.Tests.csproj" />
    <Project Path="test/TestBase/TestBase.csproj" />
    <Project Path="test/App.Tests/App.Tests.csproj" />
  </Folder>
</Solution>
"""


class TestOtherTestProjects(unittest.TestCase):
    def test_lists_every_other_tests_project_with_forward_slashes(self):
        self.assertEqual(
            shard.other_test_projects(SLNX, "test/Ef.Tests/Ef.Tests.csproj"),
            ["test/App.Tests/App.Tests.csproj", "test/Domain.Tests/Domain.Tests.csproj"],
        )

    def test_a_sharded_project_missing_from_the_solution_is_refused(self):
        """A renamed EF project must not leave shard 1 running it whole AND the shards running it."""
        with self.assertRaises(ValueError):
            shard.other_test_projects(SLNX, "test/Gone.Tests/Gone.Tests.csproj")


class TestExecutedClasses(_TempDirCase):
    def test_collects_classes_from_every_trx_including_skipped_tests(self):
        self.write("a/one.trx", _trx(("Ns.Alpha", "Passed"), ("Ns.Beta", "NotExecuted")))
        self.write("b/two.trx", _trx(("Ns.Gamma", "Failed")))

        classes, tests = shard.executed_classes(self.tmp)

        self.assertEqual(classes, {"Ns.Alpha", "Ns.Beta", "Ns.Gamma"})
        self.assertEqual(tests, 3)

    def test_no_trx_file_is_an_error_not_an_empty_result(self):
        with self.assertRaises(ValueError):
            shard.executed_classes(self.tmp)


class TestVerify(_TempDirCase):
    def test_matching_sets_pass(self):
        self.write("r/x.trx", _trx(("Ns.Alpha", "Passed"), ("Ns.Beta", "Passed")))

        self.assertEqual(shard.verify({"Ns.Alpha", "Ns.Beta"}, self.tmp), [])

    def test_a_class_that_never_ran_is_reported(self):
        self.write("r/x.trx", _trx(("Ns.Alpha", "Passed")))

        problems = shard.verify({"Ns.Alpha", "Ns.Beta"}, self.tmp)

        self.assertEqual(len(problems), 1)
        self.assertIn("Ns.Beta", problems[0])
        self.assertIn("assigned but not run", problems[0])

    def test_a_class_run_by_the_wrong_shard_is_reported(self):
        self.write("r/x.trx", _trx(("Ns.Alpha", "Passed"), ("Ns.Other", "Passed")))

        problems = shard.verify({"Ns.Alpha"}, self.tmp)

        self.assertEqual(len(problems), 1)
        self.assertIn("Ns.Other", problems[0])
        self.assertIn("run but not assigned", problems[0])

    def test_a_run_with_no_tests_reports_every_assigned_class_as_not_run(self):
        self.write("r/x.trx", _trx())

        problems = shard.verify({"Ns.Alpha"}, self.tmp)

        self.assertEqual(len(problems), 1)
        self.assertIn("assigned but not run: Ns.Alpha", problems[0])


class TestMain(_TempDirCase):
    def _run(self, *argv: str) -> tuple[object, str, str]:
        """Run main() capturing output; a SystemExit's code is returned like a normal exit code."""
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            try:
                code: object = shard.main(list(argv))
            except SystemExit as stop:
                code = stop.code
        return code, out.getvalue(), err.getvalue()

    def _partition(self, listing: Path, shards: str, index: str) -> tuple[object, str, str]:
        return self._run(
            "partition", "--listing", str(listing), "--shards", shards, "--index", index,
            "--classes-out", str(self.tmp / "classes.txt"),
            "--runsettings-out", str(self.tmp / "shard.runsettings"),
        )

    def test_partition_writes_the_classes_and_runsettings_of_the_chosen_shard(self):
        listing = self.write("list.txt", LISTING)

        code, out, _ = self._partition(listing, "2", "1")

        self.assertEqual(code, 0)
        chosen = (self.tmp / "classes.txt").read_text(encoding="utf-8").split()
        self.assertTrue(chosen)
        self.assertEqual((self.tmp / "shard.runsettings").read_text(encoding="utf-8"), shard.runsettings_for(chosen))
        self.assertIn("shard 1/2", out)

    def test_the_shards_of_one_listing_together_hold_every_class_once(self):
        listing = self.write("list.txt", LISTING)
        seen: list[str] = []

        for index in ("1", "2", "3"):
            code, _, _ = self._partition(listing, "3", index)
            self.assertEqual(code, 0)
            seen += (self.tmp / "classes.txt").read_text(encoding="utf-8").split()

        self.assertEqual(sorted(seen), ["Ns.Alpha", "Ns.Beta", "Ns.Deep.Delta", "Ns.Gamma"])

    def test_partition_refuses_an_index_outside_the_shard_count(self):
        code, _, err = self._partition(self.write("list.txt", LISTING), "2", "3")

        self.assertEqual(code, 1)
        self.assertIn("index", err)

    def test_partition_refuses_a_listing_with_no_tests(self):
        code, _, err = self._partition(self.write("list.txt", "Build succeeded.\n"), "2", "1")

        self.assertEqual(code, 1)
        self.assertIn("no tests", err)

    def test_partition_refuses_more_shards_than_classes(self):
        code, _, err = self._partition(self.write("list.txt", LISTING), "5", "1")

        self.assertEqual(code, 1)
        self.assertIn("empty shard", err)

    def test_other_projects_prints_one_path_per_line(self):
        slnx = self.write("x.slnx", SLNX)

        code, out, _ = self._run("other-projects", "--slnx", str(slnx), "--sharded", "test/Ef.Tests/Ef.Tests.csproj")

        self.assertEqual(code, 0)
        self.assertEqual(out.split(), ["test/App.Tests/App.Tests.csproj", "test/Domain.Tests/Domain.Tests.csproj"])

    def test_other_projects_exits_one_when_the_sharded_project_is_missing(self):
        slnx = self.write("x.slnx", SLNX)

        code, _, err = self._run("other-projects", "--slnx", str(slnx), "--sharded", "test/Gone/Gone.Tests.csproj")

        self.assertEqual(code, 1)
        self.assertIn("not in the solution", err)

    def test_verify_exits_zero_on_a_match_and_one_on_a_mismatch(self):
        classes = self.write("classes.txt", "Ns.Alpha\nNs.Beta\n")
        self.write("good/x.trx", _trx(("Ns.Alpha", "Passed"), ("Ns.Beta", "Passed")))
        self.write("bad/x.trx", _trx(("Ns.Alpha", "Passed")))

        good, _, _ = self._run("verify", "--classes", str(classes), "--trx-dir", str(self.tmp / "good"))
        bad, _, err = self._run("verify", "--classes", str(classes), "--trx-dir", str(self.tmp / "bad"))

        self.assertEqual(good, 0)
        self.assertEqual(bad, 1)
        self.assertIn("Ns.Beta", err)

    def test_verify_exits_one_when_there_is_no_trx(self):
        classes = self.write("classes.txt", "Ns.Alpha\n")

        code, _, err = self._run("verify", "--classes", str(classes), "--trx-dir", str(self.tmp / "none"))

        self.assertEqual(code, 1)
        self.assertIn(".trx", err)


if __name__ == "__main__":
    unittest.main()
