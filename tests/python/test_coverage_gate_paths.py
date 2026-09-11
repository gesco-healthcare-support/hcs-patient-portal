"""Glob translation, path normalisation, and exclusion matching.

`glob_to_regex` is hand-written rather than delegated to `fnmatch`, because
`fnmatch` lets `*` cross a path separator and would silently over-match. That
distinction is the whole reason the function exists, so it is asserted in both
directions here.
"""

from __future__ import annotations

import unittest

from gate_loader import gate

BACKSLASH = chr(92)  # written this way so no escape sequence appears in source


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


if __name__ == "__main__":
    unittest.main()
