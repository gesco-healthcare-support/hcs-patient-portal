"""Tests for import-issues.py's redaction and lookup helpers (#816).

`redact()` is the guard that keeps real names and tenant GUIDs out of issues
published to a PUBLIC repository, and it had no test at all. The sweep that
prompted these tests changed the email pattern, so the pattern's behaviour is
pinned here rather than taken on trust -- including the property the change was
made for, which is that it stays linear on hostile input.

Run: python -m unittest discover -s tests/python -p "test_*.py"
"""

import pathlib
import tempfile
import time
import unittest

from gate_loader import load

# `import-issues.py` is hyphenated, so it cannot be imported by name. This is
# the same problem `gate_loader` was written for in #825, and its `load()`
# already takes the module name and repo-relative path -- so it is reused here
# rather than reimplemented. Importing is safe: every entry point in the script
# is guarded behind `if __name__ == "__main__"`.
import_issues = load("import_issues", "scripts/maintenance/import-issues.py")


class RedactEmailTests(unittest.TestCase):
    """Addresses must not survive into a published issue."""

    def test_replaces_a_plain_address(self):
        self.assertEqual(import_issues.redact("ping someone@example.com now"), "ping <person> now")

    def test_replaces_every_address_not_just_the_first(self):
        out = import_issues.redact("a@x.com and b@y.org and c@z.net")
        self.assertEqual(out, "<person> and <person> and <person>")
        self.assertNotIn("@", out)

    def test_replaces_addresses_with_the_full_local_part_character_set(self):
        for local in ("a.b", "a+b", "a_b", "a%b", "a-b", "A.B_c+d%e-f"):
            with self.subTest(local=local):
                self.assertEqual(import_issues.redact(local + "@example.com"), "<person>")

    def test_replaces_a_multi_label_domain(self):
        self.assertEqual(import_issues.redact("x@sub.domain.co.uk"), "<person>")

    def test_keeps_surrounding_engineering_text(self):
        out = import_issues.redact("AppointmentManager.cs:50 owner someone@example.com line 50")
        self.assertEqual(out, "AppointmentManager.cs:50 owner <person> line 50")

    def test_leaves_a_bare_word_alone(self):
        self.assertEqual(import_issues.redact("no address here"), "no address here")

    def test_leaves_a_dotless_domain_alone(self):
        # `user@localhost` has no dot, so it is not matched. Pinned because the
        # domain half of the pattern is what an earlier ReDoS fix rewrote.
        self.assertEqual(import_issues.redact("user@localhost"), "user@localhost")


class RedactGuidTests(unittest.TestCase):
    """Tenant GUIDs identify a real practice."""

    def test_replaces_a_guid(self):
        out = import_issues.redact("tenant 3f2504e0-4f89-11d3-9a0c-0305e82c3301 failed")
        self.assertEqual(out, "tenant <office-id> failed")

    def test_replaces_an_uppercase_guid(self):
        self.assertEqual(
            import_issues.redact("3F2504E0-4F89-11D3-9A0C-0305E82C3301"), "<office-id>"
        )

    def test_leaves_a_short_hex_run_alone(self):
        self.assertEqual(import_issues.redact("commit 3f2504e0"), "commit 3f2504e0")

    def test_redacts_both_kinds_in_one_pass(self):
        out = import_issues.redact("a@b.com owns 3f2504e0-4f89-11d3-9a0c-0305e82c3301")
        self.assertEqual(out, "<person> owns <office-id>")


class EmailPatternPerformanceTests(unittest.TestCase):
    """The pattern must stay linear on input that never matches.

    This is the property the #816 change was made for. The previous pattern was
    quadratic: the local part was retried at every offset inside a run of
    local-part characters, each retry consuming the rest of the run before
    failing to find the `@`. A 16 KB input took 26 seconds.

    A wall-clock budget is asserted rather than a growth ratio, because a ratio
    is noisy on a loaded CI runner. Both directions were measured on the real
    patterns rather than estimated: the fixed pattern runs these inputs in
    ~4 ms (500x under the budget), and reintroducing the quadratic one takes
    23.9 s and 15.6 s respectively (12x and 8x over it). So a slow runner
    cannot trip the budget and a quadratic pattern cannot pass it.
    """

    HOSTILE = "a" * 16000 + "@" + "b" * 16000  # local part, then a dotless domain
    BUDGET_SECONDS = 2.0

    def test_hostile_input_completes_well_inside_the_budget(self):
        start = time.perf_counter()
        result = import_issues.redact(self.HOSTILE)
        elapsed = time.perf_counter() - start
        # Nothing matches: the domain has no dot.
        self.assertEqual(result, self.HOSTILE)
        self.assertLess(
            elapsed,
            self.BUDGET_SECONDS,
            "redact() took {:.2f}s on a 32 KB non-matching input; the email "
            "pattern has regained super-linear backtracking".format(elapsed),
        )

    def test_a_dot_heavy_non_match_also_stays_fast(self):
        # 24k dot-separated pairs rather than 8k: at 8k the quadratic pattern
        # came in at 2.7s against a 2.0s budget, a margin too thin to rely on.
        # At this size it is ~24s, matching the headline case.
        hostile = "a." * 24000
        start = time.perf_counter()
        import_issues.redact(hostile)
        self.assertLess(time.perf_counter() - start, self.BUDGET_SECONDS)

    def test_an_address_at_the_end_of_a_long_run_is_still_found(self):
        # The lookbehind pins a match to the start of a local-part run, so this
        # guards the obvious way to get linearity wrong: refusing the match.
        text = "x" * 5000 + "@example.com"
        self.assertEqual(import_issues.redact(text), "<person>")


class AlreadyCreatedTests(unittest.TestCase):
    """The .issue-map.tsv reader, rewritten to dict() by the same sweep."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.tmp = pathlib.Path(self._tmp.name)
        self.addCleanup(self._tmp.cleanup)

    def _read_map(self, contents):
        path = self.tmp / ".issue-map.tsv"
        path.write_text(contents, encoding="utf-8")
        original = import_issues.MAP
        import_issues.MAP = path
        try:
            return import_issues.already_created()
        finally:
            import_issues.MAP = original

    def test_parses_tab_separated_rows(self):
        out = self._read_map("BUG-001\thttps://x/1\nBUG-002\thttps://x/2\n")
        self.assertEqual(out, {"BUG-001": "https://x/1", "BUG-002": "https://x/2"})

    def test_skips_rows_without_a_tab(self):
        out = self._read_map("BUG-001\thttps://x/1\ngarbage line\n")
        self.assertEqual(out, {"BUG-001": "https://x/1"})

    def test_keeps_tabs_inside_the_url(self):
        # split("\t", 1) splits once, so a stray tab stays in the value.
        out = self._read_map("K\thttps://x/1\textra\n")
        self.assertEqual(out, {"K": "https://x/1\textra"})

    def test_returns_empty_when_the_map_is_absent(self):
        original = import_issues.MAP
        import_issues.MAP = self.tmp / "does-not-exist.tsv"
        try:
            self.assertEqual(import_issues.already_created(), {})
        finally:
            import_issues.MAP = original


class LabelConstantTests(unittest.TestCase):
    """The LABELS table now uses the constants defined above it (S1192)."""

    def test_label_names_are_unchanged(self):
        names = [name for name, _colour, _desc in import_issues.LABELS]
        self.assertEqual(
            names,
            [
                "severity/high",
                "severity/medium",
                "severity/low",
                "severity/observation",
                "type/bug",
                "type/observation",
                "type/hardening",
                "type/sweep",
                "source/finding",
                "source/hardening",
                "source/backlog",
                "source/sweep",
            ],
        )

    def test_every_label_name_is_unique(self):
        names = [name for name, _c, _d in import_issues.LABELS]
        self.assertEqual(len(names), len(set(names)))


if __name__ == "__main__":
    unittest.main()
