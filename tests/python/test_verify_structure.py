"""Structural checks in `.claude/scripts/verify_structure.py` (#786).

WHY THE FIXTURE HANDLING IS THE INTERESTING PART, and why it is asserted rather
than assumed. The script resolves `REPO_ROOT` from `__file__` at import (line
28) and DERIVES `DOMAIN_ROOT` from it at import (line 61). Patching `REPO_ROOT`
alone therefore does NOT move `DOMAIN_ROOT`: the feature check would keep
reading the real repository, against a fixture that was never consulted, and
report a plausible result for the wrong reason.

`FixtureIsInUseTests` exists to fail when that happens. It asserts on a feature
name that cannot occur in this repository and on a count only the fixture can
produce, so a run reading the live tree goes red instead of quietly passing.
Delete either assignment in `setUp` and that class fails by name.

Run: python -m unittest discover -s tests/python -p "test_*.py"
"""

from __future__ import annotations

import contextlib
import io
import json
import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path

from gate_loader import load

# `verify_structure.py` sits in `.claude/scripts/`, which is not a package and is
# not on `sys.path`. `gate_loader.load()` already takes a module name and a
# repo-relative path -- the mechanism #825 wrote and #843 reused -- so it is
# reused here rather than reimplemented. Importing is safe: the only entry point
# is guarded behind `if __name__ == "__main__"`.
verify_structure = load("verify_structure", ".claude/scripts/verify_structure.py")

# A feature name that cannot exist in the real repository. Load-bearing, not
# decoration: it is what makes "the fixture is in use" provable.
SENTINEL_FEATURE = "ZzzFixtureOnlyFeature"


class QuietTestCase(unittest.TestCase):
    """Swallows the script's own stdout for the duration of each test.

    `Report.ok`, `warn` and `fail` print as they record, so an unsuppressed run
    emits hundreds of lines -- including literal `FAIL` lines produced by tests
    that are PASSING. In a CI log that is worse than noise: it buries the real
    unittest summary and gives a log scanner something to match on. The buffer is
    retained as `self.printed` so a test can assert on the output if it needs to.
    """

    def setUp(self):
        buffer = io.StringIO()
        redirect = contextlib.redirect_stdout(buffer)
        redirect.__enter__()
        self.addCleanup(redirect.__exit__, None, None, None)
        self.printed = buffer


class FixtureRepoTestCase(QuietTestCase):
    """Builds a throwaway repository tree and points the script at it.

    BOTH module globals are reassigned. `DOMAIN_ROOT` is derived from
    `REPO_ROOT` at import time, so setting only the first leaves the second
    addressing the real `src/` tree.
    """

    def setUp(self):
        super().setUp()
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        # Held separately from the module global so the helpers below can write
        # here even when that global is NOT patched. See `feature()`.
        self.fixture_domain = self.root / "src" / "HealthcareSupport.CaseEvaluation.Domain"

        self._original_repo_root = verify_structure.REPO_ROOT
        self._original_domain_root = verify_structure.DOMAIN_ROOT
        self.addCleanup(self._restore_globals)

        verify_structure.REPO_ROOT = self.root
        verify_structure.DOMAIN_ROOT = self.fixture_domain

    def _restore_globals(self):
        verify_structure.REPO_ROOT = self._original_repo_root
        verify_structure.DOMAIN_ROOT = self._original_domain_root

    def write(self, relative, contents="placeholder"):
        """Create a file under the fixture root, making parents as needed."""
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(contents, encoding="utf-8")
        return path

    def feature(self, name, with_claude_md=True):
        """Create a Domain feature directory, optionally carrying its CLAUDE.md.

        Anchored to `self.fixture_domain`, NEVER to `verify_structure.DOMAIN_ROOT`.
        Writing through the module global means this helper escapes into the real
        `src/` tree the moment that global is unpatched -- which is exactly the
        state the seen-to-fail proof creates deliberately. Found the hard way: an
        earlier version of this file left four directories and a stray `.cs` file
        in `src/HealthcareSupport.CaseEvaluation.Domain/` after that run. A
        fixture that can write outside itself is a worse defect than the one it
        guards, so the anchor is the fixture path and the guard compares the two.
        """
        directory = self.fixture_domain / name
        directory.mkdir(parents=True, exist_ok=True)
        if with_claude_md:
            (directory / "CLAUDE.md").write_text("# " + name, encoding="utf-8")
        return directory

    def report(self):
        return verify_structure.Report()


class FixtureIsInUseTests(FixtureRepoTestCase):
    """SEEN-TO-FAIL GUARD. Remove either patch in setUp and this class goes red."""

    def test_the_feature_check_reads_the_fixture_and_not_the_repository(self):
        self.feature(SENTINEL_FEATURE)
        r = self.report()

        covered = verify_structure.check_feature_claude_coverage(r)

        # Only the fixture can produce exactly one feature; the real Domain tree
        # holds many.
        self.assertEqual(covered, 1)
        self.assertTrue(
            any(SENTINEL_FEATURE in message for message in r.passes),
            "the sentinel feature was not reported, so DOMAIN_ROOT is not the fixture",
        )
        self.assertFalse(
            any("Appointments" in message for message in r.passes + r.fails),
            "a real repository feature was reported, so DOMAIN_ROOT still points at src/",
        )

    def test_repo_root_is_the_fixture(self):
        self.assertEqual(verify_structure.REPO_ROOT, self.root)

    def test_domain_root_is_under_the_fixture(self):
        # The derived global is the one that silently survives a partial patch.
        self.assertEqual(verify_structure.DOMAIN_ROOT.parents[1], self.root)


class ReportTests(QuietTestCase):
    """The verdict accumulator. No fixture needed; it touches no filesystem."""

    def test_a_new_report_is_empty_and_exits_zero(self):
        r = verify_structure.Report()
        self.assertEqual((r.passes, r.warns, r.fails), ([], [], []))
        self.assertEqual(r.exit_code(), 0)

    def test_warnings_alone_still_exit_zero(self):
        r = verify_structure.Report()
        r.ok("fine")
        r.warn("worth knowing")
        self.assertEqual(r.exit_code(), 0)

    def test_any_failure_exits_one(self):
        r = verify_structure.Report()
        r.ok("fine")
        r.fail("broken")
        self.assertEqual(r.exit_code(), 1)

    def test_messages_land_in_their_own_bucket(self):
        r = verify_structure.Report()
        r.ok("a")
        r.warn("b")
        r.fail("c")
        self.assertEqual((r.passes, r.warns, r.fails), (["a"], ["b"], ["c"]))


class CountFeatureRowsTests(unittest.TestCase):
    """`count_feature_rows_in_claude_md` is pure: text in, integer out."""

    HEADING = "### Feature CLAUDE.md Index"

    def test_counts_rows_after_the_separator(self):
        text = (
            self.HEADING + "\n\n| Feature | Path |\n| --- | --- |\n"
            "| Appointments | a |\n| Patients | b |\n"
        )
        self.assertEqual(verify_structure.count_feature_rows_in_claude_md(text), 2)

    def test_returns_zero_when_the_heading_is_absent(self):
        self.assertEqual(verify_structure.count_feature_rows_in_claude_md("# Nothing here"), 0)

    def test_stops_at_the_next_heading(self):
        text = (
            self.HEADING + "\n\n| F | P |\n| --- | --- |\n| One | a |\n"
            "\n### Another Section\n\n| X | Y |\n| --- | --- |\n| Two | b |\n"
        )
        self.assertEqual(verify_structure.count_feature_rows_in_claude_md(text), 1)

    def test_stops_at_a_blank_line_after_the_table(self):
        text = self.HEADING + "\n\n| F | P |\n| --- | --- |\n| One | a |\n\nprose\n"
        self.assertEqual(verify_structure.count_feature_rows_in_claude_md(text), 1)

    def test_a_heading_with_no_table_counts_zero(self):
        self.assertEqual(
            verify_structure.count_feature_rows_in_claude_md(self.HEADING + "\n\nprose only\n"), 0
        )


class RequiredFilesTests(FixtureRepoTestCase):
    def test_every_required_file_present_and_non_empty_passes(self):
        for relative in verify_structure.REQUIRED_FILES:
            self.write(relative)
        r = self.report()

        verify_structure.check_required_files(r)

        self.assertEqual(r.fails, [])
        self.assertEqual(len(r.passes), len(verify_structure.REQUIRED_FILES))

    def test_a_missing_file_fails_and_names_it(self):
        for relative in verify_structure.REQUIRED_FILES[1:]:
            self.write(relative)
        missing = verify_structure.REQUIRED_FILES[0]
        r = self.report()

        verify_structure.check_required_files(r)

        self.assertEqual(len(r.fails), 1)
        self.assertIn(missing, r.fails[0])

    def test_an_empty_file_fails_rather_than_passing(self):
        """Present-but-empty is the case a bare existence check would miss."""
        for relative in verify_structure.REQUIRED_FILES:
            self.write(relative)
        (self.root / verify_structure.REQUIRED_FILES[0]).write_text("", encoding="utf-8")
        r = self.report()

        verify_structure.check_required_files(r)

        self.assertEqual(len(r.fails), 1)
        self.assertIn(verify_structure.REQUIRED_FILES[0], r.fails[0])


class FeatureCoverageTests(FixtureRepoTestCase):
    def test_a_feature_without_a_claude_md_fails(self):
        self.feature("Covered")
        self.feature("Bare", with_claude_md=False)
        r = self.report()

        covered = verify_structure.check_feature_claude_coverage(r)

        self.assertEqual(covered, 1)
        self.assertEqual(len(r.fails), 1)
        self.assertIn("Bare", r.fails[0])

    def test_excluded_directories_are_skipped_entirely(self):
        """A negative guarantee, so the excluded directory is PRESENT and bare."""
        excluded = sorted(verify_structure.DOMAIN_EXCLUDE)[0]
        self.feature(excluded, with_claude_md=False)
        self.feature("Real")
        r = self.report()

        covered = verify_structure.check_feature_claude_coverage(r)

        self.assertEqual(covered, 1)
        self.assertEqual(r.fails, [], "an excluded directory was graded")
        self.assertFalse(any(excluded in message for message in r.passes))

    def test_loose_files_in_domain_are_not_treated_as_features(self):
        self.fixture_domain.mkdir(parents=True, exist_ok=True)
        (self.fixture_domain / "Stray.cs").write_text("//", encoding="utf-8")
        r = self.report()

        self.assertEqual(verify_structure.check_feature_claude_coverage(r), 0)
        self.assertEqual(r.fails, [])

    def test_a_missing_domain_root_fails_and_returns_zero(self):
        r = self.report()

        covered = verify_structure.check_feature_claude_coverage(r)

        self.assertEqual(covered, 0)
        self.assertEqual(len(r.fails), 1)


class LayerClaudeFilesTests(FixtureRepoTestCase):
    def test_present_layer_files_pass(self):
        for relative in verify_structure.LAYER_CLAUDE_FILES:
            self.write(relative)
        r = self.report()

        verify_structure.check_layer_claude_files(r)

        self.assertEqual(r.fails, [])

    def test_a_missing_layer_file_fails_and_names_it(self):
        r = self.report()

        verify_structure.check_layer_claude_files(r)

        self.assertEqual(len(r.fails), len(verify_structure.LAYER_CLAUDE_FILES))
        self.assertIn(verify_structure.LAYER_CLAUDE_FILES[0], r.fails[0])


class RepoMapFreshnessTests(FixtureRepoTestCase):
    INDEX = "docs/repo-map/index.json"

    def _index(self, generated_at):
        self.write(self.INDEX, json.dumps({"generated_at": generated_at}))

    def test_a_recent_map_passes(self):
        self._index((datetime.now(timezone.utc) - timedelta(days=1)).isoformat())
        r = self.report()

        verify_structure.check_repo_map_freshness(r)

        self.assertEqual((r.fails, r.warns), ([], []))

    def test_a_map_older_than_thirty_days_warns_rather_than_fails(self):
        self._index((datetime.now(timezone.utc) - timedelta(days=45)).isoformat())
        r = self.report()

        verify_structure.check_repo_map_freshness(r)

        self.assertEqual(r.fails, [])
        self.assertEqual(len(r.warns), 1)

    def test_a_missing_index_fails(self):
        r = self.report()

        verify_structure.check_repo_map_freshness(r)

        self.assertEqual(len(r.fails), 1)

    def test_an_unparseable_index_fails_rather_than_raising(self):
        self.write(self.INDEX, "{not json")
        r = self.report()

        verify_structure.check_repo_map_freshness(r)

        self.assertEqual(len(r.fails), 1)

    def test_an_index_without_generated_at_fails(self):
        self.write(self.INDEX, json.dumps({"other": 1}))
        r = self.report()

        verify_structure.check_repo_map_freshness(r)

        self.assertEqual(len(r.fails), 1)


class DecisionsAndSecurityTests(FixtureRepoTestCase):
    def test_decisions_with_only_a_readme_warns(self):
        self.write("docs/decisions/README.md")
        r = self.report()

        verify_structure.check_decisions_not_empty(r)

        self.assertEqual(len(r.warns), 1)
        self.assertEqual(r.fails, [])

    def test_decisions_with_an_adr_passes(self):
        self.write("docs/decisions/README.md")
        self.write("docs/decisions/0001-choose-a-thing.md")
        r = self.report()

        verify_structure.check_decisions_not_empty(r)

        self.assertEqual((r.warns, r.fails), ([], []))

    def test_a_missing_decisions_directory_fails(self):
        r = self.report()

        verify_structure.check_decisions_not_empty(r)

        self.assertEqual(len(r.fails), 1)

    def test_fewer_than_five_security_documents_warns(self):
        for name in ("a", "b"):
            self.write("docs/security/{}.md".format(name))
        r = self.report()

        verify_structure.check_security_dir_populated(r)

        self.assertEqual(len(r.warns), 1)

    def test_five_security_documents_pass(self):
        for name in ("a", "b", "c", "d", "e"):
            self.write("docs/security/{}.md".format(name))
        r = self.report()

        verify_structure.check_security_dir_populated(r)

        self.assertEqual((r.warns, r.fails), ([], []))


class ProxyReadmeTests(FixtureRepoTestCase):
    def test_present_readme_passes(self):
        self.write("angular/src/app/proxy/README.md")
        r = self.report()

        verify_structure.check_proxy_not_tracked(r)

        self.assertEqual((r.warns, r.fails), ([], []))

    def test_a_missing_readme_warns_rather_than_failing(self):
        r = self.report()

        verify_structure.check_proxy_not_tracked(r)

        self.assertEqual(len(r.warns), 1)
        self.assertEqual(r.fails, [])


class FeatureCountConsistencyTests(FixtureRepoTestCase):
    HEADING = "### Feature CLAUDE.md Index"

    def _root_claude_md(self, rows):
        body = self.HEADING + "\n\n| Feature | Path |\n| --- | --- |\n"
        body += "".join("| F{} | p |\n".format(i) for i in range(rows))
        self.write("CLAUDE.md", body)

    def test_matching_counts_pass(self):
        self._root_claude_md(3)
        r = self.report()

        verify_structure.check_feature_count_consistency(r, 3)

        self.assertEqual((r.warns, r.fails), ([], []))

    def test_a_mismatch_warns_and_reports_both_numbers(self):
        self._root_claude_md(3)
        r = self.report()

        verify_structure.check_feature_count_consistency(r, 5)

        self.assertEqual(len(r.warns), 1)
        self.assertIn("3", r.warns[0])
        self.assertIn("5", r.warns[0])

    def test_a_missing_root_claude_md_fails(self):
        r = self.report()

        verify_structure.check_feature_count_consistency(r, 1)

        self.assertEqual(len(r.fails), 1)

    def test_an_empty_index_table_warns(self):
        self.write("CLAUDE.md", "# no index here")
        r = self.report()

        verify_structure.check_feature_count_consistency(r, 1)

        self.assertEqual(len(r.warns), 1)


if __name__ == "__main__":
    unittest.main()
