"""Tests for .claude/scripts/embed-generated-regions.py.

The script fills GENERATED regions in documentation from committed snapshots, so a
document cannot drift from the code the way APPOINTMENT-LIFECYCLE.md did: before the
2026-09 pass it taught eleven transitions the state machine does not permit, missed three
it does, and showed an unreachable chain as the happy path.

THE TWO TESTS THAT CARRY THE DESIGN are in ConverterRoundTripTests and
ConverterHeaderTests. Adrian chose rendered tables over verbatim embedding, which means a
transform sits between the snapshot and the reader, and a wrong transform does not look
wrong -- it produces a plausible table. So:

  - the round trip proves no row was dropped, merged, truncated or reordered;
  - the literal header assertion proves the column labels are honest, which the round trip
    says nothing about. A converter can round-trip perfectly and still mislabel a column,
    and a reader trusts the header.

Everything else here exists to keep the script quiet and predictable: unknown facts fail
by name, duplicate stems fail by name, line endings survive, and an up-to-date document
rewrites nothing.
"""

import io
import os
import unittest

from gate_loader import load

embed = load("embed_generated_regions", ".claude/scripts/embed-generated-regions.py")


def write(path, text, newline="\n"):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with io.open(path, "w", encoding="utf-8", newline="") as handle:
        handle.write(text.replace("\n", newline))


AUTH_ROWS = [
    "Ns.Svc.CreateAsync(Dto) -> class=(authenticated) method=CaseEvaluation.Thing.Create",
    "Ns.Svc.GetAsync(Guid) -> class=CaseEvaluation.Thing method=-",
]


class ConverterRoundTripTests(unittest.TestCase):
    """Every converter must be lossless. This is the whole safety argument for rendering."""

    def test_authorization_surface_round_trips_exactly(self):
        render, parse = embed.CONVERTERS["authorization-surface"]
        header, table = render(AUTH_ROWS)
        lines = embed.to_markdown(header, table)
        parsed_header, parsed_table = embed.from_markdown(lines)
        self.assertEqual(parse(parsed_table), AUTH_ROWS)
        self.assertEqual(parsed_header, header)

    def test_every_registered_converter_round_trips(self):
        """Guards the converters added later, not just today's one."""
        samples = {"authorization-surface": AUTH_ROWS}
        for name, (render, parse) in embed.CONVERTERS.items():
            with self.subTest(converter=name):
                rows = samples.get(name)
                self.assertIsNotNone(rows, "add a sample for the %s converter" % name)
                header, table = render(rows)
                _h, parsed = embed.from_markdown(embed.to_markdown(header, table))
                self.assertEqual(parse(parsed), rows)

    def test_row_count_and_order_are_preserved(self):
        render, parse = embed.CONVERTERS["authorization-surface"]
        header, table = render(AUTH_ROWS)
        _h, parsed = embed.from_markdown(embed.to_markdown(header, table))
        self.assertEqual(len(parsed), len(AUTH_ROWS))
        self.assertEqual(parse(parsed)[0], AUTH_ROWS[0])

    def test_a_pipe_inside_a_cell_does_not_invent_a_column(self):
        rows = ["Ns.Svc.M(a|b) -> class=(authenticated) method=-"]
        render, parse = embed.CONVERTERS["authorization-surface"]
        header, table = render(rows)
        _h, parsed = embed.from_markdown(embed.to_markdown(header, table))
        self.assertEqual(parse(parsed), rows)


class RoundTripDiscriminationTests(unittest.TestCase):
    """Proves the round trip would CATCH a wrong converter, not merely pass a right one.

    A test that only ever sees correct input cannot tell you it discriminates. The honest
    way to show that here is a purpose-built LOSSY converter used as a fixture -- rather
    than breaking the shipped one, which Adrian's 2026-09-28 testing rule drops.

    These three fixtures are the three ways a renderer goes wrong in practice: it drops a
    column, it merges rows, or it reorders them. Each must be rejected.
    """

    def _round_trips(self, render, parse, rows):
        header, table = render(rows)
        _h, parsed = embed.from_markdown(embed.to_markdown(header, table))
        try:
            return parse(parsed) == rows
        except (ValueError, IndexError):
            return False

    def test_a_converter_that_drops_a_column_is_rejected(self):
        def lossy(rows):
            return ["Member", "Class-level"], [
                [r.partition(" -> ")[0], "(dropped)"] for r in rows
            ]

        def parse(table):
            return ["%s -> class=%s method=-" % (a, b) for a, b in table]

        self.assertFalse(self._round_trips(lossy, parse, AUTH_ROWS))

    def test_a_converter_that_merges_rows_is_rejected(self):
        def lossy(rows):
            _h, table = embed.CONVERTERS["authorization-surface"][0](rows)
            return ["Member", "Class-level", "Method-level"], table[:1]

        _r, parse = embed.CONVERTERS["authorization-surface"]
        self.assertFalse(self._round_trips(lossy, parse, AUTH_ROWS))

    def test_a_converter_that_reorders_rows_is_rejected(self):
        def lossy(rows):
            header, table = embed.CONVERTERS["authorization-surface"][0](rows)
            return header, list(reversed(table))

        _r, parse = embed.CONVERTERS["authorization-surface"]
        self.assertFalse(self._round_trips(lossy, parse, AUTH_ROWS))

    def test_the_same_harness_ACCEPTS_the_real_converter(self):
        """Without this, the three above would pass even if the harness rejected everything."""
        render, parse = embed.CONVERTERS["authorization-surface"]
        self.assertTrue(self._round_trips(render, parse, AUTH_ROWS))


class ConverterHeaderTests(unittest.TestCase):
    """The round trip proves the DATA survived; it says nothing about the labels."""

    def test_authorization_surface_header_is_exactly_this(self):
        render, _parse = embed.CONVERTERS["authorization-surface"]
        header, _table = render(AUTH_ROWS)
        self.assertEqual(header, ["Member", "Class-level", "Method-level"])


class TempRepo(unittest.TestCase):
    def setUp(self):
        import tempfile

        self._tmp = tempfile.TemporaryDirectory()
        self.root = self._tmp.name
        self.addCleanup(self._tmp.cleanup)

    def snapshot(self, stem, rows, folder="test/Some.Tests"):
        write(os.path.join(self.root, folder, stem + ".approved.txt"), "\n".join(rows) + "\n")

    def doc(self, rel, body, newline="\n"):
        path = os.path.join(self.root, rel.replace("/", os.sep))
        write(path, body, newline=newline)
        return path

    def run_main(self, *extra):
        import contextlib

        buffer = io.StringIO()
        with contextlib.redirect_stdout(buffer):
            code = embed.main(["embed.py", self.root] + list(extra))
        return code, buffer.getvalue()


class RegionRewriteTests(TempRepo):
    REGION = (
        "# Doc\n\nProse above the markers, maintained by a person.\n\n"
        "<!-- GENERATED: authorization-surface BEGIN - do not edit by hand -->\n"
        "stale content\n"
        "<!-- GENERATED: authorization-surface END -->\n\nProse below.\n"
    )

    def test_a_region_is_filled_from_its_snapshot(self):
        self.snapshot("authorization-surface", AUTH_ROWS)
        path = self.doc("docs/page.md", self.REGION)
        code, _out = self.run_main()
        body = io.open(path, encoding="utf-8").read()
        self.assertEqual(code, 1, "first fill is a change, so it reports one")
        self.assertIn("| Member | Class-level | Method-level |", body)
        self.assertNotIn("stale content", body)

    def test_prose_outside_the_markers_is_untouched(self):
        self.snapshot("authorization-surface", AUTH_ROWS)
        path = self.doc("docs/page.md", self.REGION)
        self.run_main()
        body = io.open(path, encoding="utf-8").read()
        self.assertIn("Prose above the markers, maintained by a person.", body)
        self.assertIn("Prose below.", body)

    def test_an_up_to_date_document_rewrites_nothing_and_exits_zero(self):
        self.snapshot("authorization-surface", AUTH_ROWS)
        path = self.doc("docs/page.md", self.REGION)
        self.run_main()
        first = io.open(path, "rb").read()
        code, out = self.run_main()
        self.assertEqual(code, 0, out)
        self.assertEqual(io.open(path, "rb").read(), first)

    def test_check_only_reports_without_writing(self):
        self.snapshot("authorization-surface", AUTH_ROWS)
        path = self.doc("docs/page.md", self.REGION)
        before = io.open(path, "rb").read()
        code, out = self.run_main("--check")
        self.assertEqual(code, 1)
        self.assertIn("out of date", out)
        self.assertEqual(io.open(path, "rb").read(), before)

    def test_a_document_with_no_markers_is_ignored(self):
        self.snapshot("authorization-surface", AUTH_ROWS)
        path = self.doc("docs/plain.md", "# Plain\n\nNothing generated here.\n")
        before = io.open(path, "rb").read()
        code, _out = self.run_main()
        self.assertEqual(code, 0)
        self.assertEqual(io.open(path, "rb").read(), before)


class FailLoudlyTests(TempRepo):
    def test_a_marker_naming_an_unknown_fact_fails_by_name(self):
        self.doc(
            "docs/page.md",
            "<!-- GENERATED: no-such-fact BEGIN -->\nx\n<!-- GENERATED: no-such-fact END -->\n",
        )
        code, out = self.run_main()
        self.assertEqual(code, 1)
        self.assertIn("NO SNAPSHOT", out)
        self.assertIn("no-such-fact", out)

    def test_a_snapshot_without_a_converter_fails_only_when_a_document_asks_for_it(self):
        """A snapshot nobody embeds is normal, not an error.

        The authorization surface is deliberately pointer-only, so a rule of "any snapshot
        lacking a converter fails" would fail forever on a decision that was made on
        purpose. The failure belongs at the point of USE.
        """
        self.snapshot("tenancy-surface", ["Thing -> IMultiTenant"])
        code, out = self.run_main()
        self.assertEqual(code, 0, out)

        self.doc(
            "docs/page.md",
            "<!-- GENERATED: tenancy-surface BEGIN -->\nx\n<!-- GENERATED: tenancy-surface END -->\n",
        )
        code, out = self.run_main()
        self.assertEqual(code, 1)
        self.assertIn("NO CONVERTER", out)
        self.assertIn("tenancy-surface", out)

    def test_duplicate_snapshot_stems_fail_naming_both(self):
        self.snapshot("authorization-surface", AUTH_ROWS, folder="test/A.Tests")
        self.snapshot("authorization-surface", AUTH_ROWS, folder="test/B.Tests")
        code, out = self.run_main()
        self.assertEqual(code, 1)
        self.assertIn("DUPLICATE", out)
        self.assertIn("A.Tests", out)
        self.assertIn("B.Tests", out)

    def test_an_unterminated_region_fails_rather_than_corrupting_the_file(self):
        self.snapshot("authorization-surface", AUTH_ROWS)
        path = self.doc(
            "docs/page.md",
            "# D\n<!-- GENERATED: authorization-surface BEGIN -->\nbody\n\nmore prose\n",
        )
        before = io.open(path, "rb").read()
        code, out = self.run_main()
        self.assertEqual(code, 1)
        self.assertIn("UNTERMINATED", out)
        self.assertEqual(io.open(path, "rb").read(), before)

    def test_a_missing_directory_is_a_usage_error(self):
        self.assertEqual(embed.main(["embed.py", os.path.join("no", "such")]), 2)

    def test_a_gitignored_document_is_not_scanned(self):
        """Found by running the script on the real repo before trusting it.

        docs/plans/ is gitignored and holds this feature's own plan files, which quote
        the marker syntax as examples. Unfiltered, the script reported two findings about
        its own design notes. A checker that reports on files nobody ships gets skipped.
        """
        import subprocess

        try:
            subprocess.run(
                ["git", "init", "-q"], cwd=self.root, check=True,
                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            )
        except (OSError, subprocess.SubprocessError):  # pragma: no cover
            self.skipTest("git unavailable")
        write(os.path.join(self.root, ".gitignore"), "docs/plans/\n")
        self.doc(
            "docs/plans/notes.md",
            "<!-- GENERATED: no-such-fact BEGIN -->\nx\n<!-- GENERATED: no-such-fact END -->\n",
        )
        code, out = self.run_main()
        self.assertEqual(code, 0, out)
        self.assertNotIn("no-such-fact", out)


class LineEndingTests(TempRepo):
    """A rewriting script that flips line endings leaves a permanently dirty tree.

    Hit for real on 2026-09-28: Python text mode converted four handoff files to CRLF on
    write. Here it would mean CI failing on a change nobody made.
    """

    def test_crlf_input_stays_crlf(self):
        self.snapshot("authorization-surface", AUTH_ROWS)
        path = self.doc("docs/page.md", RegionRewriteTests.REGION, newline="\r\n")
        self.run_main()
        raw = io.open(path, "rb").read()
        self.assertIn(b"\r\n", raw)
        self.assertNotIn(b"\n\n\r", raw)

    def test_lf_input_stays_lf(self):
        self.snapshot("authorization-surface", AUTH_ROWS)
        path = self.doc("docs/page.md", RegionRewriteTests.REGION, newline="\n")
        self.run_main()
        self.assertNotIn(b"\r", io.open(path, "rb").read())


if __name__ == "__main__":  # pragma: no cover
    unittest.main()
