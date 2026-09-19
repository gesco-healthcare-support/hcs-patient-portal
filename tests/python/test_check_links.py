"""Tests for .claude/scripts/check-links.py (#786).

This script gates documentation links in CI. It had no tests, so nothing
distinguished "no broken links" from "found no links to check" -- and the
second failure mode is silent: a regex that stops matching, or a gather step
that returns nothing, both report success.

Several tests below exist specifically to make that distinction, rather than
only asserting that valid links pass.
"""

import pathlib
import unittest

from gate_loader import load

check_links = load("check_links", ".claude/scripts/check-links.py")


class IsExcludedTests(unittest.TestCase):
    """Build output must not be scanned; real docs must be."""

    def test_excludes_each_configured_part(self):
        for part in ("bin", "obj", "node_modules", "dist", ".angular"):
            with self.subTest(part=part):
                self.assertTrue(
                    check_links.is_excluded(pathlib.Path("a") / part / "x.md")
                )

    def test_does_not_exclude_an_ordinary_docs_path(self):
        self.assertFalse(check_links.is_excluded(pathlib.Path("docs/runbooks/x.md")))

    def test_matches_a_whole_path_part_not_a_substring(self):
        # "binaries" contains "bin"; excluding it would silently skip real docs.
        self.assertFalse(check_links.is_excluded(pathlib.Path("docs/binaries/x.md")))


class SkipReasonTests(unittest.TestCase):
    """Links that need no filesystem resolution, and their reasons."""

    def test_empty_target(self):
        self.assertEqual(check_links._skip_reason(""), (True, "empty"))

    def test_external_schemes(self):
        for target in ("http://x.test", "https://x.test", "mailto:a@b.test"):
            with self.subTest(target=target):
                self.assertEqual(check_links._skip_reason(target), (True, "external"))

    def test_anchor_only(self):
        self.assertEqual(check_links._skip_reason("#section"), (True, "anchor"))

    def test_template_placeholder(self):
        self.assertEqual(check_links._skip_reason("docs/{name}.md"), (True, "template"))

    def test_a_real_relative_path_is_not_skipped(self):
        # The negative case matters most: if this ever returned a skip, every
        # broken link in the repo would report as valid.
        self.assertIsNone(check_links._skip_reason("docs/INDEX.md"))

    def test_a_windows_drive_letter_is_not_treated_as_a_scheme(self):
        # Single letter then colon. The scheme regex requires 2+ chars before
        # the colon, so this stays a filesystem path.
        self.assertIsNone(check_links._skip_reason("C:/x.md"))


class ValidateLinkTests(unittest.TestCase):
    """Resolution order: source-relative, then repo-root, then fail."""

    def setUp(self):
        import tempfile

        self._tmp = tempfile.TemporaryDirectory()
        self.root = pathlib.Path(self._tmp.name)
        self.addCleanup(self._tmp.cleanup)

        (self.root / "docs").mkdir()
        (self.root / "docs" / "target.md").write_text("x", encoding="utf-8")
        (self.root / "top.md").write_text("x", encoding="utf-8")
        self.source = self.root / "docs" / "source.md"
        self.source.write_text("x", encoding="utf-8")

        self._original_root = check_links.REPO_ROOT
        check_links.REPO_ROOT = self.root
        self.addCleanup(setattr, check_links, "REPO_ROOT", self._original_root)

    def test_sibling_file_resolves(self):
        ok, detail = check_links.validate_link(self.source, "target.md")
        self.assertTrue(ok)
        self.assertEqual(detail, "ok")

    def test_missing_file_fails_and_names_the_path(self):
        ok, detail = check_links.validate_link(self.source, "nope.md")
        self.assertFalse(ok)
        self.assertIn("nope.md", detail)

    def test_anchor_fragment_is_stripped_before_resolving(self):
        ok, _ = check_links.validate_link(self.source, "target.md#a-heading")
        self.assertTrue(ok)

    def test_a_broken_path_with_an_anchor_still_fails(self):
        # Guards the obvious way to get fragment-stripping wrong: dropping
        # everything after "#" and then not checking what is left.
        ok, _ = check_links.validate_link(self.source, "nope.md#a-heading")
        self.assertFalse(ok)

    def test_repo_root_fallback(self):
        ok, detail = check_links.validate_link(self.source, "top.md")
        self.assertTrue(ok)
        self.assertEqual(detail, "ok-from-root")

    def test_workspace_relative_leading_slash(self):
        ok, detail = check_links.validate_link(self.source, "/docs/target.md")
        self.assertTrue(ok)
        self.assertEqual(detail, "ok-from-root")

    def test_workspace_relative_missing_file_fails(self):
        ok, detail = check_links.validate_link(self.source, "/docs/nope.md")
        self.assertFalse(ok)
        self.assertIn("workspace-relative", detail)

    def test_surrounding_whitespace_is_tolerated(self):
        ok, _ = check_links.validate_link(self.source, "  target.md  ")
        self.assertTrue(ok)

    def test_bare_fragment_after_strip(self):
        ok, detail = check_links.validate_link(self.source, "#only-an-anchor")
        self.assertTrue(ok)
        self.assertEqual(detail, "anchor")


class LinkRegexTests(unittest.TestCase):
    """The regex is what decides whether anything is checked at all."""

    def _targets(self, text):
        return [m.group("target") for m in check_links.LINK_RE.finditer(text)]

    def test_extracts_a_simple_link(self):
        self.assertEqual(self._targets("see [docs](docs/INDEX.md) now"), ["docs/INDEX.md"])

    def test_extracts_several_links_from_one_line(self):
        self.assertEqual(
            self._targets("[a](one.md) and [b](two.md)"), ["one.md", "two.md"]
        )

    def test_ignores_a_bare_url_with_no_link_syntax(self):
        self.assertEqual(self._targets("https://example.test/x.md"), [])

    def test_ignores_an_image_target_is_not_expected_to_be_special(self):
        # Images use the same syntax with a leading "!", so they ARE matched.
        # Pinned as current behaviour: image targets get validated too, which is
        # wanted -- a broken image path is a broken link.
        self.assertEqual(self._targets("![alt](img/x.png)"), ["img/x.png"])


class GatherMdFilesTests(unittest.TestCase):
    """The step whose silent failure mode is "found nothing, reported success".

    Called ONCE for the whole class. It globs `**/CLAUDE.md` from the repository
    root, which walks `angular/node_modules` -- about 13 seconds per call here.
    Four calls made this file slower than the rest of the suite combined.
    """

    @classmethod
    def setUpClass(cls):
        cls.files = check_links.gather_md_files()

    def test_finds_the_repository_s_own_markdown(self):
        self.assertGreater(
            len(self.files), 10,
            "gather_md_files returned almost nothing; the link check would pass "
            "by scanning no files rather than by finding no broken links",
        )

    def test_every_returned_path_is_markdown(self):
        self.assertTrue(all(p.suffix == ".md" for p in self.files))

    def test_no_excluded_path_is_returned(self):
        self.assertFalse([p for p in self.files if check_links.is_excluded(p)])

    def test_the_result_is_sorted_and_deduplicated(self):
        self.assertEqual(self.files, sorted(set(self.files)))


class MainTests(unittest.TestCase):
    """The exit code CI acts on, and the report it prints.

    SCAN_DIRS and CLAUDE_GLOBS are module-level and computed at import from the
    real repository root, so they are redirected alongside REPO_ROOT -- patching
    only REPO_ROOT would leave main() scanning the real tree and make the result
    depend on the repo's own link health rather than on the fixture.
    """

    def setUp(self):
        import tempfile

        self._tmp = tempfile.TemporaryDirectory()
        self.root = pathlib.Path(self._tmp.name)
        self.addCleanup(self._tmp.cleanup)
        (self.root / "docs").mkdir()
        (self.root / "docs" / "target.md").write_text("ok", encoding="utf-8")

        for name, value in (
            ("REPO_ROOT", self.root),
            ("SCAN_DIRS", [self.root / "docs"]),
            ("CLAUDE_GLOBS", []),
        ):
            self.addCleanup(setattr, check_links, name, getattr(check_links, name))
            setattr(check_links, name, value)

    def _write(self, name, body):
        (self.root / "docs" / name).write_text(body, encoding="utf-8")

    def test_returns_zero_when_every_link_resolves(self):
        self._write("a.md", "see [t](target.md) and [ext](https://x.test)")
        self.assertEqual(check_links.main(), 0)

    def test_returns_one_when_a_link_is_broken(self):
        self._write("a.md", "see [t](missing.md)")
        self.assertEqual(check_links.main(), 1)

    def test_reports_the_failing_file_and_target(self):
        import contextlib
        import io as _io

        self._write("a.md", "see [t](missing.md)")
        buf = _io.StringIO()
        with contextlib.redirect_stdout(buf):
            check_links.main()
        out = buf.getvalue()
        self.assertIn("FAIL", out)
        self.assertIn("missing.md", out)
        self.assertIn("docs/a.md", out)

    def test_counts_the_links_it_validated(self):
        import contextlib
        import io as _io

        self._write("a.md", "[t](target.md) [u](target.md) [v](target.md)")
        buf = _io.StringIO()
        with contextlib.redirect_stdout(buf):
            check_links.main()
        self.assertIn("validated 3 links", buf.getvalue())

    def test_an_unreadable_file_warns_rather_than_crashing(self):
        # Invalid UTF-8: the handler catches UnicodeDecodeError and continues.
        (self.root / "docs" / "bad.md").write_bytes(bytes([0xFF, 0xFE, 0x00]) + b"bad")
        import contextlib
        import io as _io

        buf = _io.StringIO()
        with contextlib.redirect_stdout(buf):
            rc = check_links.main()
        self.assertEqual(rc, 0)
        self.assertIn("WARN unreadable", buf.getvalue())


if __name__ == "__main__":
    unittest.main()
