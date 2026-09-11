"""Glob translation, path normalisation, and exclusion matching.

`glob_to_regex` is hand-written rather than delegated to `fnmatch`, because
`fnmatch` lets `*` cross a path separator and would silently over-match. That
distinction is the whole reason the function exists, so it is asserted in both
directions here.
"""

from __future__ import annotations

import contextlib
import io
import tempfile
import unittest
from pathlib import Path

from gate_loader import gate

BACKSLASH = chr(92)  # written this way so no escape sequence appears in source
NEWLINE = chr(10)    # same convention, for the fixture files below


class TestGlobToRegex(unittest.TestCase):
    def test_double_star_prefix_matches_any_leading_directories(self):
        pattern = gate.glob_to_regex("**/Program.cs")
        self.assertTrue(pattern.match("Program.cs"))
        self.assertTrue(pattern.match("src/App/Program.cs"))
        self.assertTrue(pattern.match("a/b/c/Program.cs"))

    def test_double_star_suffix_matches_any_trailing_directories(self):
        pattern = gate.glob_to_regex("angular/src/app/proxy/**")
        self.assertTrue(pattern.match("angular/src/app/proxy"))
        self.assertTrue(pattern.match("angular/src/app/proxy/models.ts"))
        self.assertTrue(pattern.match("angular/src/app/proxy/a/b/deep.ts"))

    def test_single_star_does_NOT_cross_a_separator(self):
        """The reason this function is not `fnmatch`."""
        pattern = gate.glob_to_regex("scripts/*.py")
        self.assertTrue(pattern.match("scripts/coverage-gate.py"))
        self.assertFalse(pattern.match("scripts/maintenance/import-issues.py"))

    def test_question_mark_matches_exactly_one_non_separator(self):
        pattern = gate.glob_to_regex("a/?.ts")
        self.assertTrue(pattern.match("a/b.ts"))
        self.assertFalse(pattern.match("a/bc.ts"))
        self.assertFalse(pattern.match("a//.ts"))

    def test_pattern_is_anchored_at_both_ends(self):
        pattern = gate.glob_to_regex("src/App.cs")
        self.assertTrue(pattern.match("src/App.cs"))
        self.assertFalse(pattern.match("other/src/App.cs"))
        self.assertFalse(pattern.match("src/App.cs.bak"))

    def test_regex_metacharacters_in_a_pattern_are_literal(self):
        """A `.` in a pattern must not match an arbitrary character."""
        pattern = gate.glob_to_regex("a/b.ts")
        self.assertTrue(pattern.match("a/b.ts"))
        self.assertFalse(pattern.match("a/bXts"))


class TestNormalise(unittest.TestCase):
    def test_converts_windows_separators(self):
        raw = "src" + BACKSLASH + "app" + BACKSLASH + "x.ts"
        self.assertEqual(gate.normalise(raw, ""), "src/app/x.ts")

    def test_applies_the_prefix(self):
        self.assertEqual(gate.normalise("src/app/x.ts", "angular"), "angular/src/app/x.ts")

    def test_a_trailing_slash_on_the_prefix_does_not_double(self):
        self.assertEqual(gate.normalise("src/x.ts", "angular/"), "angular/src/x.ts")

    def test_empty_prefix_leaves_the_path_repo_relative(self):
        self.assertEqual(
            gate.normalise("scripts/coverage-gate.py", ""), "scripts/coverage-gate.py"
        )

    def test_windows_separators_AND_prefix_together(self):
        """Both halves matter: this is the karma case the docstring records."""
        raw = "src" + BACKSLASH + "app" + BACKSLASH + "proxy" + BACKSLASH + "x.ts"
        self.assertEqual(gate.normalise(raw, "angular"), "angular/src/app/proxy/x.ts")

    def test_a_leading_dot_directory_keeps_its_dot(self):
        """#787: `lstrip("./")` strips a CHARACTER SET, not a prefix.

        `.claude/scripts/x.py` came back as `claude/scripts/x.py`. Harmless
        while only lcov and the .NET Cobertura were ingested -- neither emits a
        dot-directory -- and live the moment Python coverage arrives, since
        `.claude/scripts/*.py` is in its denominator. The damage would have been
        silent in BOTH consumers at once: the `.claude/...` entries in
        `.coverage-exclusions` would match nothing, and the changed-lines floor
        would compare the diff's `.claude/scripts/x.py` against the report's
        `claude/scripts/x.py`, find no record, and treat the file as invisible.
        """
        self.assertEqual(
            gate.normalise(".claude/scripts/verify_structure.py", ""),
            ".claude/scripts/verify_structure.py",
        )

    def test_a_dot_github_path_keeps_its_dot(self):
        self.assertEqual(
            gate.normalise(".github/workflows/ci.yml", ""), ".github/workflows/ci.yml"
        )

    def test_a_leading_dot_slash_is_still_removed(self):
        # The behaviour the old lstrip was actually there for.
        self.assertEqual(gate.normalise("./src/a.cs", ""), "src/a.cs")

    def test_repeated_leading_dot_slash_is_removed(self):
        self.assertEqual(gate.normalise("././src/a.cs", ""), "src/a.cs")

    def test_a_dot_directory_survives_prefixing_too(self):
        self.assertEqual(
            gate.normalise(".claude/x.py", "sub"), "sub/.claude/x.py"
        )


class TestExcluded(unittest.TestCase):
    def test_returns_true_when_any_pattern_matches(self):
        patterns = [
            gate.glob_to_regex("**/Migrations/**"),
            gate.glob_to_regex("angular/src/app/proxy/**"),
        ]
        self.assertTrue(gate.excluded("angular/src/app/proxy/models.ts", patterns))
        self.assertTrue(gate.excluded("src/Ef/Migrations/Init.cs", patterns))

    def test_returns_false_when_no_pattern_matches(self):
        patterns = [gate.glob_to_regex("**/Migrations/**")]
        self.assertFalse(gate.excluded("src/App/Service.cs", patterns))

    def test_an_empty_pattern_list_excludes_nothing(self):
        self.assertFalse(gate.excluded("anything/at/all.cs", []))


class TestInRepo(unittest.TestCase):
    """#683: is a counted file one of ours?

    Suffix matching rather than equality, because the backend report carries
    absolute runner paths. Comparing those against `git ls-files` output by
    equality reports EVERY file untracked -- observed once as 925 of 925, which
    is a tell rather than a result. Both directions are asserted, because
    over-matching would quietly re-admit the vendor source this catches.
    """

    TRACKED = {"src/App/Foo.cs", "angular/src/app/x.ts"}

    def test_exact_repo_relative_path_matches(self):
        self.assertTrue(gate.in_repo("src/App/Foo.cs", self.TRACKED))

    def test_absolute_runner_path_matches_by_suffix(self):
        """The case that makes an equality comparison useless."""
        self.assertTrue(gate.in_repo(
            "home/runner/work/hcs-patient-portal/hcs-patient-portal/src/App/Foo.cs",
            self.TRACKED))

    def test_vendor_sourcelink_root_does_NOT_match(self):
        """FluentValidation arrives rooted at an underscore. This is the point."""
        self.assertFalse(gate.in_repo(
            "_/src/FluentValidation/AbstractValidator.cs", self.TRACKED))

    def test_match_is_on_a_separator_boundary(self):
        """Ends with a tracked path but is not that file."""
        self.assertFalse(gate.in_repo("Xsrc/App/Foo.cs", self.TRACKED))

    def test_a_partial_suffix_is_not_a_match(self):
        self.assertFalse(gate.in_repo("App/Foo.cs", self.TRACKED))

    def test_empty_tracked_set_matches_nothing(self):
        self.assertFalse(gate.in_repo("src/App/Foo.cs", set()))


class TestAssertTracked(unittest.TestCase):
    """The guarantee, asserted in the direction that can actually break.

    A fixture where every file is tracked would pass with the function body
    deleted, so the untracked case is seeded explicitly and is load-bearing.
    """

    def setUp(self):
        self.patterns = [gate.glob_to_regex("**/obj/**")]
        self.tracked = {"src/App/Foo.cs"}

    def test_all_counted_files_tracked_is_silent(self):
        gate.assert_tracked({"src/App/Foo.cs": {1: 1}}, self.patterns, self.tracked)

    def test_an_untracked_counted_file_FAILS_and_names_it(self):
        per_file = {
            "src/App/Foo.cs": {1: 1},
            "_/src/FluentValidation/AbstractValidator.cs": {1: 0},
        }
        with contextlib.redirect_stdout(io.StringIO()) as buf:
            with self.assertRaises(SystemExit) as ctx:
                gate.assert_tracked(per_file, self.patterns, self.tracked)
        self.assertEqual(ctx.exception.code, 1)
        self.assertIn("AbstractValidator.cs", buf.getvalue())

    def test_an_EXCLUDED_untracked_file_is_not_reported(self):
        """Exclusions apply first; naming a vendor is the sanctioned fix."""
        per_file = {
            "src/App/Foo.cs": {1: 1},
            "src/App/obj/Release/Generated.g.cs": {1: 0},
        }
        gate.assert_tracked(per_file, self.patterns, self.tracked)


class TestLoadTracked(unittest.TestCase):
    """Both failure modes, because either would make the check pass blindly."""

    def test_a_missing_list_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            with contextlib.redirect_stdout(io.StringIO()):
                with self.assertRaises(SystemExit):
                    gate.load_tracked(Path(tmp) / "absent.txt")

    def test_a_list_with_no_entries_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / "empty.txt"
            p.write_text(NEWLINE.join(["# only a comment", "", ""]), encoding="utf-8")
            with contextlib.redirect_stdout(io.StringIO()):
                with self.assertRaises(SystemExit):
                    gate.load_tracked(p)

    def test_comments_and_blanks_are_ignored(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / "list.txt"
            p.write_text(
                NEWLINE.join(["# header", "", "src/A.cs", "  src/B.cs  ", ""]),
                encoding="utf-8")
            self.assertEqual(gate.load_tracked(p), {"src/A.cs", "src/B.cs"})


if __name__ == "__main__":
    unittest.main()
