"""The changed-lines floor on a tests-only submission (#970).

C# test files are excluded from coverage, so a pull request whose diff is entirely test code has
zero coverable changed lines and takes the pass-by-default branch. That is the right verdict -- no
production line was added -- but the floor measured NOTHING, and it used to print the same words a
docs-only PR gets. These tests pin that the two are told apart, and, as the positive control, that
the floor still REFUSES when a coverable changed line is uncovered: a gate that cannot refuse would
make the wording tests above it meaningless.
"""

from __future__ import annotations

import argparse
import contextlib
import io
import re
import tempfile
import unittest
from pathlib import Path

from gate_loader import gate

PER_FILE = {"src/App/Thing.cs": {1: 1, 2: 0, 3: 0}}
NO_PATTERNS: list[re.Pattern[str]] = []


def diff_for(path: str, first: int, count: int) -> str:
    body = "".join(f"+line{i}\n" for i in range(count))
    return f"diff --git a/{path} b/{path}\n--- a/{path}\n+++ b/{path}\n@@ -0,0 +{first},{count} @@\n{body}"


class TestTestsOnlyFiles(unittest.TestCase):
    def test_all_test_code_is_recognised(self):
        changed = {"test/X.Tests/A.cs": {1}, "angular/src/app/a.spec.ts": {2}}
        per_file = {"src/App/Thing.cs": {1: 1}, "angular/src/app/b.ts": {1: 1}}
        self.assertEqual(
            gate.tests_only_files(changed, per_file),
            ["angular/src/app/a.spec.ts", "test/X.Tests/A.cs"],
        )

    def test_one_production_file_means_it_is_not_tests_only(self):
        changed = {"test/X.Tests/A.cs": {1}, "src/App/Thing.cs": {5}}
        self.assertEqual(gate.tests_only_files(changed, PER_FILE), [])

    def test_docs_and_yaml_do_not_hide_a_tests_only_submission(self):
        changed = {"test/X.Tests/A.cs": {1}, "docs/readme.md": {1}, ".github/workflows/ci.yml": {3}}
        self.assertEqual(gate.tests_only_files(changed, PER_FILE), ["test/X.Tests/A.cs"])

    def test_a_docs_only_submission_is_not_tests_only(self):
        self.assertEqual(gate.tests_only_files({"docs/readme.md": {1}}, PER_FILE), [])


class TestEnforceChangedLines(unittest.TestCase):
    def run_floor(self, diff_text: str):
        with tempfile.TemporaryDirectory() as tmp:
            diff = Path(tmp) / "c.diff"
            diff.write_text(diff_text, encoding="utf-8")
            args = argparse.Namespace(changed_diff=str(diff), floor_changed="80")
            buf = io.StringIO()
            with contextlib.redirect_stdout(buf):
                ok = gate.enforce_changed_lines(args, PER_FILE, NO_PATTERNS)
        return ok, buf.getvalue()

    def test_tests_only_passes_but_says_the_floor_was_vacuous(self):
        ok, out = self.run_floor(diff_for("test/X.Tests/A.cs", 1, 3))
        self.assertTrue(ok)
        self.assertIn("VACUOUS", out)
        self.assertIn("::notice::", out)

    def test_docs_only_keeps_the_ordinary_wording(self):
        ok, out = self.run_floor(diff_for("docs/readme.md", 1, 3))
        self.assertTrue(ok)
        self.assertNotIn("VACUOUS", out)
        self.assertIn("nothing to enforce", out)

    # THE POSITIVE CONTROL: without it the above pass with a gate that always returns True.
    def test_the_floor_still_refuses_an_uncovered_production_change(self):
        ok, out = self.run_floor(diff_for("src/App/Thing.cs", 2, 2))
        self.assertFalse(ok)
        self.assertIn("FAIL", out)

    def test_a_production_change_alongside_tests_is_graded_not_vacuous(self):
        ok, out = self.run_floor(diff_for("src/App/Thing.cs", 2, 2) + diff_for("test/X.Tests/A.cs", 1, 3))
        self.assertFalse(ok)
        self.assertNotIn("VACUOUS", out)


if __name__ == "__main__":
    unittest.main()
