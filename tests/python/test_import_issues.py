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


class BatchKeyTests(unittest.TestCase):
    """The idempotency key must denote WHAT a batch covers, not its rank (#820).

    The defect this replaces was silent in the worse direction. `SWEEP-NN` was
    assigned by position in a list sorted by descending finding count, so the
    key meant "the Nth largest group when you ran it". Measured 2026-09-10,
    24 of 25 fresh keys denoted a different directory than the ledger row of the
    same name, and the dry run reported "1 would be created, 124 already exist"
    while silently skipping `test` (96 findings), `scripts` (64), `docker` and
    `tests`. A duplicate would have been visible; a skip was not.
    """

    def test_reproduces_the_hand_written_ledger_keys(self):
        # These five rows were appended to .issue-map.tsv by hand on 2026-09-10
        # in the shape the fix was meant to adopt, so they are the contract.
        for name, expected in (
            ("test", "SWEEP-PATH-test"),
            ("scripts", "SWEEP-PATH-scripts"),
            ("docker and 1 smaller directories",
             "SWEEP-PATH-docker-and-1-smaller-directories"),
            ("tests and 1 smaller directories",
             "SWEEP-PATH-tests-and-1-smaller-directories"),
            (".claude and 1 smaller directories",
             "SWEEP-PATH-claude-and-1-smaller-directories"),
        ):
            with self.subTest(name=name):
                self.assertEqual(import_issues.batch_key(name), expected)

    def test_the_key_does_not_depend_on_finding_count_or_order(self):
        # The defect itself. The batch name is the only input, so the same
        # directory keeps its key however the counts move around it.
        self.assertEqual(
            import_issues.batch_key("angular/src"),
            import_issues.batch_key("angular/src"),
        )

    def test_different_directories_get_different_keys(self):
        names = [
            "angular/src",
            "angular/src/app/appointments",
            "src/HealthcareSupport.CaseEvaluation.Application",
            "test",
            "tests",
        ]
        keys = [import_issues.batch_key(n) for n in names]
        self.assertEqual(len(set(keys)), len(names))

    def test_a_leading_dot_is_dropped_rather_than_becoming_a_separator(self):
        self.assertEqual(
            import_issues.batch_key(".claude"), "SWEEP-PATH-claude"
        )

    def test_path_separators_and_dots_both_become_hyphens(self):
        self.assertEqual(
            import_issues.batch_key("src/A.B.C/Sub"), "SWEEP-PATH-src-a-b-c-sub"
        )

    def test_the_slug_is_lowercase_with_no_runs_of_separators(self):
        # The SWEEP-PATH- prefix stays upper case, matching every other key
        # family in the ledger (BUG-, HARD-). Only the derived slug is lowered.
        key = import_issues.batch_key("src/HealthcareSupport.CaseEvaluation.Application")
        prefix, slug = key[:11], key[11:]
        self.assertEqual(prefix, "SWEEP-PATH-")
        self.assertEqual(slug, slug.lower())
        self.assertNotIn("--", slug)
        self.assertFalse(slug.endswith("-"))
        self.assertFalse(slug.startswith("-"))


class LedgerMigrationTests(unittest.TestCase):
    """The committed ledger must be fully migrated and internally consistent."""

    @classmethod
    def setUpClass(cls):
        cls.rows = [
            line.split("\t", 1)
            for line in import_issues.MAP.read_text(encoding="utf-8").splitlines()
            if "\t" in line
        ]

    def test_no_positional_sweep_key_survives(self):
        stale = [k for k, _ in self.rows if k.startswith("SWEEP-") and "PATH" not in k]
        self.assertEqual(
            stale, [],
            "a positional SWEEP-NN row is left in the ledger; already_created() "
            "would match it against whatever group happens to rank there next",
        )

    def test_every_key_is_unique(self):
        keys = [k for k, _ in self.rows]
        dupes = sorted({k for k in keys if keys.count(k) > 1})
        self.assertEqual(dupes, [])

    def test_every_row_points_at_an_issue_url(self):
        for key, url in self.rows:
            with self.subTest(key=key):
                self.assertRegex(url, r"^https://github\.com/.+/issues/\d+$")

    def test_the_sweep_rows_all_use_the_derived_shape(self):
        sweeps = [k for k, _ in self.rows if "SWEEP" in k]
        self.assertTrue(sweeps)
        for key in sweeps:
            with self.subTest(key=key):
                self.assertTrue(key.startswith("SWEEP-PATH-"))


class ExistingCleanupPathsTests(unittest.TestCase):
    """Parsing the `Paths:` block out of an issue body (#820 defect 2)."""

    def test_extracts_every_bulleted_path(self):
        body = (
            "12 open Sonar issues.\n\n"
            "**Paths (this issue owns these exclusively):**\n"
            "- `angular/src`\n- `angular/src/app`\n\n"
            "Assigning yourself is the claim."
        )
        self.assertEqual(
            import_issues.PATH_BULLET_RE.findall(body),
            ["angular/src", "angular/src/app"],
        )

    def test_ignores_prose_that_merely_mentions_a_path(self):
        body = "See `angular/src` for detail.\n- not a backticked path\n"
        self.assertEqual(import_issues.PATH_BULLET_RE.findall(body), [])

    def test_an_empty_body_yields_nothing(self):
        self.assertEqual(import_issues.PATH_BULLET_RE.findall(""), [])


class AssertDisjointTests(unittest.TestCase):
    """Within a run, and -- since #820 -- across runs."""

    def _batch(self, key, paths):
        return {"key": key, "paths": paths}

    def test_disjoint_batches_pass(self):
        import_issues.assert_disjoint(
            [self._batch("A", ["src/a"]), self._batch("B", ["src/b"])]
        )

    def test_a_duplicate_path_within_one_run_exits(self):
        with self.assertRaises(SystemExit):
            import_issues.assert_disjoint(
                [self._batch("A", ["src/a"]), self._batch("B", ["src/a"])]
            )

    def test_a_nested_path_within_one_run_exits(self):
        with self.assertRaises(SystemExit):
            import_issues.assert_disjoint(
                [self._batch("A", ["src"]), self._batch("B", ["src/a"])]
            )

    def test_a_path_owned_by_a_different_open_issue_exits(self):
        # The cross-run case. #628, #631 and #665 were each clashed with on
        # EXACT paths by a re-bundled group, and every check passed.
        with self.assertRaises(SystemExit):
            import_issues.assert_disjoint(
                [self._batch("SWEEP-PATH-new", ["angular/src"])],
                existing={"angular/src": "#628"},
            )

    def test_a_batch_may_re_state_the_paths_of_the_issue_it_already_is(self):
        # Without this the check would fire on every unchanged batch, which
        # would make it useless and get it removed.
        original = import_issues.MAP
        with tempfile.TemporaryDirectory() as tmp:
            ledger = pathlib.Path(tmp) / ".issue-map.tsv"
            ledger.write_text(
                "SWEEP-PATH-angular-src\thttps://github.com/o/r/issues/628\n",
                encoding="utf-8",
            )
            import_issues.MAP = ledger
            try:
                import_issues.assert_disjoint(
                    [self._batch("SWEEP-PATH-angular-src", ["angular/src"])],
                    existing={"angular/src": "#628"},
                )
            finally:
                import_issues.MAP = original

    def test_no_existing_map_skips_the_cross_run_check(self):
        import_issues.assert_disjoint([self._batch("A", ["src/a"])], existing={})
