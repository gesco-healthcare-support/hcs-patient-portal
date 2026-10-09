"""No packet may carry one office's identity as a literal.

WHY THIS EXISTS. Until 2026-10-09 every packet template hardcoded ONE practice's letterhead,
physician name, practice name, mailing address, phone, fax, records addresses and
missed-appointment charge. Every other office's packets therefore went to attorneys,
claim examiners and the WCAB under that practice's name. Those values are now ##Office.*##
tokens filled per office by the .NET side (OfficeLetterhead). The golden test pins bytes;
this pins the INTENT, so a future edit that pastes a real practice's details back into a
template fails here with a reason instead of slipping through a golden-hash update.

It also pins the two rendering rules the office tokens depend on:
  - every template carries the rule that hides an element whose data-if token rendered
    empty, so an office with no fax prints no "FAX:" label;
  - no token appears inside a <style> block: token values are HTML-escaped by the renderer,
    which does nothing for CSS, so an office name containing "*/" could otherwise rewrite
    the stylesheet.
"""

from __future__ import annotations

import os
import re
import shutil
import subprocess
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)

BUILDERS = {
    os.path.join("doctor", "build_doctor.py"): ["doctor.html", "doctor_landscape.html"],
    os.path.join("patient", "build_patient.py"): ["patient.html"],
    os.path.join("attorney", "build_attorney.py"): ["ame_ime.html", "pqme.html"],
}

# Fragments of the one practice's identity the templates used to hardcode. Matched
# case-insensitively against the generated HTML.
FORMER_LITERALS = [
    "falkinstein",
    "faaos",
    "orthopaedic surgeons",
    "west coast spine",
    "encino",
    "ventura blvd",
    "261656",
    "582-2600",
    "855-2466",
    "503.75",
]

TOKEN = re.compile(r"##[A-Za-z][A-Za-z0-9_]*\.[A-Za-z][A-Za-z0-9_]*##")
HIDE_EMPTY_RULE = '[data-if=""] { display: none !important; }'


def _generate() -> dict[str, str]:
    documents: dict[str, str] = {}
    for script, outputs in sorted(BUILDERS.items()):
        work = tempfile.mkdtemp(prefix="packet-identity-")
        try:
            result = subprocess.run(
                [sys.executable, os.path.join(ROOT, script)], cwd=work, capture_output=True, text=True
            )
            if result.returncode != 0:
                raise AssertionError("%s exited %d\n%s" % (script, result.returncode, result.stderr))
            for name in outputs:
                with open(os.path.join(work, name), encoding="utf-8") as fh:
                    documents[name] = fh.read()
        finally:
            shutil.rmtree(work, ignore_errors=True)
    return documents


class OfficeIdentityTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.documents = _generate()

    def test_every_document_was_generated(self) -> None:
        expected = {name for outputs in BUILDERS.values() for name in outputs}
        self.assertEqual(expected, set(self.documents))

    def test_no_document_hardcodes_a_practices_identity(self) -> None:
        for name, html in self.documents.items():
            lowered = html.lower()
            for literal in FORMER_LITERALS:
                with self.subTest(document=name, literal=literal):
                    self.assertFalse(literal in lowered, "%s still contains %r" % (name, literal))

    def test_the_notices_and_doctor_forms_name_the_offices_own_physician(self) -> None:
        for name in ("ame_ime.html", "pqme.html", "doctor.html", "doctor_landscape.html"):
            with self.subTest(document=name):
                self.assertTrue("##Office.PhysicianName##" in self.documents[name], name)

    def test_the_letters_carry_the_offices_letterhead(self) -> None:
        for name in ("ame_ime.html", "pqme.html", "patient.html"):
            with self.subTest(document=name):
                self.assertTrue("##Office.LetterheadName##" in self.documents[name], name)

    def test_the_patient_packet_names_the_offices_practice(self) -> None:
        self.assertTrue("##Office.PracticeName##" in self.documents["patient.html"])

    def test_documents_using_data_if_carry_the_rule_that_hides_empty_lines(self) -> None:
        for name, html in self.documents.items():
            if 'data-if="' not in html:
                continue
            with self.subTest(document=name):
                self.assertTrue(HIDE_EMPTY_RULE in html, name)

    def test_every_data_if_names_a_single_office_token(self) -> None:
        for name, html in self.documents.items():
            markup = re.sub(r"<style>.*?</style>", "", html, flags=re.S)
            for value in re.findall(r'data-if="([^"]*)"', markup):
                with self.subTest(document=name, value=value):
                    self.assertRegex(value, r"^##Office\.[A-Za-z]+##$")

    def test_no_token_is_substituted_inside_a_stylesheet(self) -> None:
        for name, html in self.documents.items():
            for style in re.findall(r"<style>(.*?)</style>", html, flags=re.S):
                with self.subTest(document=name):
                    self.assertEqual([], TOKEN.findall(style))

    def test_the_pregnancy_page_no_longer_embeds_a_letterhead_image(self) -> None:
        self.assertNotIn("pregnancy_letterhead", self.documents["patient.html"])
        self.assertFalse(
            os.path.exists(os.path.join(ROOT, "patient", "images", "pregnancy_letterhead.png")),
            "the image crop of one practice's letterhead should not ship in the renderer image",
        )


if __name__ == "__main__":
    unittest.main()
