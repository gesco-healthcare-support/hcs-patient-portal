"""The renderer's template version (the blank forms' ETag input) and its endpoint.

Runs against the stubbed third-party imports used by test_packet_renderer_paths.py. The version has to change
exactly when a render could change -- the template, the post-processing, a library version or an installed font --
and never otherwise, or the Case Tracker either keeps a stale form or re-downloads one that did not change.
"""

import pathlib
import tempfile
import unittest

from test_packet_renderer_paths import load_app


class TemplateVersionTests(unittest.TestCase):
    def setUp(self):
        self.app = load_app()
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        self.template = pathlib.Path(self.dir.name) / "doctor_blank.html"
        self.template.write_text("<html>one</html>", encoding="utf-8")
        self.app._TEMPLATE_FILES = {"doctor-blank": self.template}
        self.fonts = pathlib.Path(self.dir.name) / "fonts"
        self.fonts.mkdir()
        self.app._FONT_ROOTS = (str(self.fonts),)
        self.versions = {"weasyprint": "1.0", "pikepdf": "2.0", "pydyf": "3.0"}
        self.app.importlib.metadata.version = lambda package: self.versions[package]
        self.app._environment_fingerprint.cache_clear()

    def test_a_known_template_answers_its_sha256(self):
        body = self.app.get_template_version("doctor-blank")
        self.assertEqual(body[1]["template"], "doctor-blank")
        self.assertRegex(body[1]["sha256"], r"^[0-9a-f]{64}$")
        self.assertEqual(body[1]["sha256"], self.app.template_version("doctor-blank"))

    def test_an_unknown_template_is_a_404(self):
        _, status = self.app.get_template_version("nope")
        self.assertEqual(status, 404)

    def test_an_unreadable_template_is_a_500(self):
        self.template.unlink()
        _, status = self.app.get_template_version("doctor-blank")
        self.assertEqual(status, 500)

    def test_the_version_is_stable_until_the_template_changes(self):
        first = self.app.template_version("doctor-blank")
        self.assertEqual(first, self.app.template_version("doctor-blank"))
        self.template.write_text("<html>two</html>", encoding="utf-8")
        self.assertNotEqual(first, self.app.template_version("doctor-blank"))

    def test_the_environment_part_is_computed_once(self):
        calls = []
        self.app.importlib.metadata.version = lambda package: calls.append(package) or "1"
        self.app._environment_fingerprint.cache_clear()
        self.app.template_version("doctor-blank")
        self.app.template_version("doctor-blank")
        self.assertEqual(sorted(calls), ["pikepdf", "pydyf", "weasyprint"])

    def test_a_library_version_is_part_of_the_version(self):
        first = self.app.template_version("doctor-blank")
        self.versions["weasyprint"] = "1.1"
        self.app._environment_fingerprint.cache_clear()
        self.assertNotEqual(first, self.app.template_version("doctor-blank"))

    def test_an_installed_font_file_is_part_of_the_version(self):
        first = self.app.template_version("doctor-blank")
        (self.fonts / "Sample.ttf").write_bytes(b"font")
        self.app._environment_fingerprint.cache_clear()
        added = self.app.template_version("doctor-blank")
        self.assertNotEqual(first, added)
        (self.fonts / "Sample.ttf").write_bytes(b"font, a different size")
        self.app._environment_fingerprint.cache_clear()
        self.assertNotEqual(added, self.app.template_version("doctor-blank"))

    def test_a_missing_font_directory_is_not_an_error(self):
        self.app._FONT_ROOTS = (str(self.fonts / "absent"),)
        self.app._environment_fingerprint.cache_clear()
        self.assertRegex(self.app.template_version("doctor-blank"), r"^[0-9a-f]{64}$")


if __name__ == "__main__":
    unittest.main()
