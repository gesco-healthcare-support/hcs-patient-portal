"""Unified-diff parsing for the changed-lines floor.

Only the NEW side of a diff is read. A line the submission deletes cannot be
covered by a test, so a deleted file must contribute nothing -- asserted here,
because including it would quietly drag the changed-lines percentage down with
lines no test could ever reach.

The hunk-header arithmetic is off-by-one prone and its count is OPTIONAL:
`@@ -1 +7 @@` means exactly one line at 7, not zero and not an open range.
"""

from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from gate_loader import gate

BACKSLASH = chr(92)

DIFF_TWO_FILES = """diff --git a/src/a.cs b/src/a.cs
--- a/src/a.cs
+++ b/src/a.cs
@@ -10,0 +11,3 @@
+one
+two
+three
diff --git a/src/b.cs b/src/b.cs
--- a/src/b.cs
+++ b/src/b.cs
@@ -4,0 +5 @@
+only
"""

DIFF_WITH_DELETION = """diff --git a/src/gone.cs b/src/gone.cs
--- a/src/gone.cs
+++ /dev/null
@@ -1,5 +0,0 @@
-a
diff --git a/src/kept.cs b/src/kept.cs
--- a/src/kept.cs
+++ b/src/kept.cs
@@ -1,0 +2,2 @@
+x
+y
"""


class TestDiffTarget(unittest.TestCase):
    def test_strips_the_b_prefix(self):
        self.assertEqual(gate._diff_target("+++ b/src/app/x.ts"), "src/app/x.ts")

    def test_returns_None_for_a_deleted_file(self):
        """None means the submission deleted it; it must not join the changed set."""
        self.assertIsNone(gate._diff_target("+++ /dev/null"))

    def test_converts_backslashes(self):
        raw = "+++ b/src" + BACKSLASH + "app" + BACKSLASH + "x.ts"
        self.assertEqual(gate._diff_target(raw), "src/app/x.ts")

    def test_a_path_without_the_b_prefix_survives(self):
        self.assertEqual(gate._diff_target("+++ src/app/x.ts"), "src/app/x.ts")


class TestHunkLines(unittest.TestCase):
    def test_a_hunk_with_an_explicit_count(self):
        self.assertEqual(list(gate._hunk_lines("@@ -10,0 +11,3 @@")), [11, 12, 13])

    def test_an_omitted_count_means_exactly_one_line(self):
        """`+5` is one line at 5. Not zero, not unbounded."""
        self.assertEqual(list(gate._hunk_lines("@@ -4,0 +5 @@")), [5])

    def test_a_zero_count_covers_nothing(self):
        self.assertEqual(list(gate._hunk_lines("@@ -1,5 +0,0 @@")), [])

    def test_a_non_hunk_line_yields_an_empty_range(self):
        """Empty rather than None, so the caller's loop stays flat."""
        self.assertEqual(list(gate._hunk_lines("+ some added source line")), [])
        self.assertEqual(list(gate._hunk_lines("--- a/x.cs")), [])

    def test_trailing_context_after_the_header_is_tolerated(self):
        self.assertEqual(list(gate._hunk_lines("@@ -1,2 +3,2 @@ class Foo")), [3, 4])


class TestParseChangedLines(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.tmp_path = Path(self._tmp.name)
        self.addCleanup(self._tmp.cleanup)

    def _diff(self, text: str) -> Path:
        target = self.tmp_path / "changed.diff"
        target.write_text(text, encoding="utf-8")
        return target

    def test_collects_new_side_lines_per_file(self):
        self.assertEqual(
            gate.parse_changed_lines(self._diff(DIFF_TWO_FILES)),
            {"src/a.cs": {11, 12, 13}, "src/b.cs": {5}},
        )

    def test_a_deleted_file_contributes_nothing(self):
        """THE DECOY IS LOAD-BEARING: the fixture contains a real deletion.

        Against a diff with no deletion this test would pass with the
        `/dev/null` branch removed, proving only what the parser keeps.
        """
        result = gate.parse_changed_lines(self._diff(DIFF_WITH_DELETION))
        self.assertNotIn("src/gone.cs", result)
        self.assertEqual(result, {"src/kept.cs": {2, 3}})

    def test_an_empty_diff_yields_no_files(self):
        """An empty diff is legitimate: the submission changes nothing."""
        self.assertEqual(gate.parse_changed_lines(self._diff("")), {})


if __name__ == "__main__":
    unittest.main()
