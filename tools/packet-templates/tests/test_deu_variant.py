"""The patient packet's DEU variant (patient_deu.html, 2026-11-24): the DWC Employee's Disability Questionnaire.

Sent instead of patient.html for a Panel QME patient with NO applicant attorney. The golden test pins the bytes; this
pins the INTENT:

- the variant is the 12-page packet plus deu1 + deu2 at the END, and the two pages are in no other document;
- the 32 field names the Case Tracker was promised: 13 always fillable, 19 printed when the portal knows the value
  and an empty fillable field under the same OLD name when it does not;
- the fallback, decided at render time by the renderer's token substitution, simulated here the way the renderer
  does it (every value HTML-escaped into the markup, attributes included);
- "From a list of doctors" starts selected, and nothing else does.

Synthetic values only.
"""

from __future__ import annotations

import html as html_lib
import importlib.util
import os
import re
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)

# ASCII-only on purpose, like the renderer's and the .NET side's token pattern (see test_blank_fields.py).
TOKEN = re.compile(r"##[A-Za-z][A-Za-z0-9_]*\.[A-Za-z][A-Za-z0-9_]*##")
FIELD = re.compile(r'<(?:input|textarea)[^>]*?name="([^"]+)"')
CC = re.compile(r'href="cc:([^"]+)"')
# One pre-filled value as the builder emits it: the printed span, then the fallback wrapper with its input.
PAIR = re.compile(
    r'<span class="(?:deu|dq)-val" data-known="([^"]*)"[^>]*><span class="tok">([^<]*)</span></span>'
    r'<span data-unknown="([^"]*)"><input type="text" class="[^"]*" name="([^"]+)"')

ALWAYS_FILLABLE = {
    "packet.patient.deu1.intl_address",
    "packet.patient.deu1.nature_business",
    "packet.patient.deu2.doctor_selected#from_list",
    "packet.patient.deu2.doctor_selected#other",
    "packet.patient.deu2.doctor_other",
    "packet.patient.deu2.job_duties",
    "packet.patient.deu2.disability",
    "packet.patient.deu2.work_effect",
    "packet.patient.deu2.prior_disability",
    "packet.patient.deu2.prior_when",
    "packet.patient.deu2.describe_disability",
    "packet.patient.deu2.date",
    "packet.patient.deu2.signature",
}
PREFILLED = {
    "packet.patient.deu1." + n for n in
    ("first_name", "mi", "last_name", "ssn", "street1", "street2", "city", "state", "zip", "dob", "doi", "employer")
} | {"packet.patient.deu2." + n for n in ("claim1", "claim2", "claim3", "claim4", "claim5", "doctor_name",
                                          "exam_scheduled")}


def _load(folder, script):
    spec = importlib.util.spec_from_file_location(script, os.path.join(ROOT, folder, script + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def _body(document):
    return document[document.index("<body>"):document.index("</body>")]


def _substitute(document, values):
    """What the renderer does: every ##Group.Field## replaced by its HTML-escaped value, attributes included."""
    return TOKEN.sub(lambda m: html_lib.escape(values.get(m.group(0), ""), quote=True), document)


def _visible(document):
    """(printed field names, fallback field names) that would SHOW after substitution: a data-known that resolved
    to "" is hidden, and a data-unknown that resolved to anything else is hidden."""
    printed, fallback = set(), set()
    for known, _text, unknown, name in PAIR.findall(document):
        if known != "":
            printed.add(name)
        if unknown == "":
            fallback.add(name)
    return printed, fallback


class DeuVariantTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.mod = _load("patient", "build_patient")
        cls.standard = cls.mod.build_standard()
        cls.blank = cls.mod.build_blank()
        cls.deu = cls.mod.build_deu()
        body = _body(cls.deu)
        cls.deu_pages = body[body.index('<div class="page" id="p-deu1">'):]
        cls.tokens = sorted(set(TOKEN.findall(cls.deu_pages)))

    def test_the_deu_pages_are_only_in_the_variant(self):
        for name, document in (("patient.html", self.standard), ("patient_blank.html", self.blank)):
            with self.subTest(document=name):
                self.assertNotIn('id="p-deu1"', document)
                self.assertNotIn('id="p-deu2"', document)
                self.assertNotIn("##Deu.", document)
        self.assertIn('id="p-deu1"', self.deu)
        self.assertIn('id="p-deu2"', self.deu)

    def test_the_variant_is_the_standard_packet_with_the_two_pages_at_the_end(self):
        standard, deu = _body(self.standard), _body(self.deu)
        self.assertTrue(deu.startswith(standard.rstrip("\n")))
        tail = deu[len(standard.rstrip("\n")):]
        self.assertLess(tail.index('id="p-deu1"'), tail.index('id="p-deu2"'))
        self.assertEqual(14, deu.count('<div class="page'))
        self.assertEqual([slug for slug, _ in self.mod.DEU_PAGES], ["deu1", "deu2"])

    def test_the_hide_rules_ship_only_with_the_variant(self):
        for rule in ('[data-known=""] { display: none !important; }',
                     '[data-unknown]:not([data-unknown=""]) { display: none !important; }'):
            with self.subTest(rule=rule):
                self.assertIn(rule, self.deu)
                self.assertNotIn(rule, self.standard)

    def test_the_32_promised_names_and_no_other(self):
        names = set(FIELD.findall(self.deu_pages)) | {
            n[:-len(self.mod.PRESET_ON_SUFFIX)] if n.endswith(self.mod.PRESET_ON_SUFFIX) else n
            for n in CC.findall(self.deu_pages)}
        self.assertEqual(ALWAYS_FILLABLE | PREFILLED, names)
        self.assertEqual(32, len(names))

    def test_every_prefilled_value_is_one_printed_token_and_one_fallback_under_its_old_name(self):
        pairs = PAIR.findall(self.deu_pages)
        self.assertEqual(PREFILLED, {name for *_, name in pairs})
        self.assertEqual(19, len(pairs))
        for known, text, unknown, name in pairs:
            with self.subTest(field=name):
                self.assertEqual(known, text)
                self.assertEqual(known, unknown)
                self.assertTrue(known.startswith("##Deu."), known)

    def test_the_deu_pages_use_only_the_19_deu_tokens(self):
        self.assertEqual(19, len(self.tokens))
        self.assertTrue(all(t.startswith("##Deu.") for t in self.tokens), self.tokens)
        mapped = set(self.mod.DEU1_PREFILLED.values()) | set(self.mod.DEU2_PREFILLED.values())
        self.assertEqual(mapped, set(self.tokens))

    def test_everything_known_prints_every_value_and_offers_no_fallback(self):
        printed, fallback = _visible(_substitute(self.deu_pages, dict.fromkeys(self.tokens, "SYNTHETIC")))
        self.assertEqual(PREFILLED, printed)
        self.assertEqual(set(), fallback)

    def test_an_empty_value_becomes_a_fillable_field_under_its_old_name_and_prints_nothing(self):
        empty = ["##Deu.Ssn##", "##Deu.DateOfBirth##", "##Deu.Street1##", "##Deu.DateOfInjury##", "##Deu.Employer##",
                 "##Deu.Claim1##", "##Deu.Claim2##", "##Deu.Claim3##", "##Deu.Claim4##", "##Deu.Claim5##"]
        values = dict.fromkeys(self.tokens, "SYNTHETIC")
        values.update(dict.fromkeys(empty, ""))
        printed, fallback = _visible(_substitute(self.deu_pages, values))
        expected = {"packet.patient.deu1." + n for n in ("ssn", "dob", "street1", "doi", "employer")} | {
            f"packet.patient.deu2.claim{i}" for i in range(1, 6)}
        self.assertEqual(expected, fallback)
        self.assertEqual(PREFILLED - expected, printed)

    def test_a_value_that_is_markup_stays_text_and_still_counts_as_known(self):
        printed, fallback = _visible(_substitute(self.deu_pages, dict.fromkeys(self.tokens, "<i>&'\"</i>")))
        self.assertEqual(PREFILLED, printed)
        self.assertEqual(set(), fallback)

    def test_only_from_a_list_of_doctors_starts_selected(self):
        preset = [n for n in CC.findall(self.deu_pages) if n.endswith(self.mod.PRESET_ON_SUFFIX)]
        self.assertEqual(["packet.patient.deu2.doctor_selected#from_list" + self.mod.PRESET_ON_SUFFIX], preset)
        self.assertNotIn(self.mod.PRESET_ON_SUFFIX, self.standard)

    def test_the_preset_suffix_is_the_one_post_process_strips(self):
        with open(os.path.join(ROOT, "shared", "post_process.py"), encoding="utf-8") as fh:
            source = fh.read()
        match = re.search(r'^PRESET_ON_SUFFIX = "([^"]+)"', source, re.M)
        self.assertIsNotNone(match)
        self.assertEqual(match.group(1), self.mod.PRESET_ON_SUFFIX)


if __name__ == "__main__":
    unittest.main()
