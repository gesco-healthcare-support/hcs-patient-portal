"""The blank (reusable) packet variants the Case Tracker fills by field name.

The golden test pins the exact bytes. This pins the INTENT: the blank copies print no per-case value, every new
field has a unique name, no sent name changes, and the sent packets were not touched.
"""

from __future__ import annotations

import importlib.util
import os
import re
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, os.path.join(ROOT, "shared"))
import blank_fields  # noqa: E402

# [A-Za-z0-9_] is NOT written as \w on purpose: \w is Unicode-aware, and this pattern must agree character for
# character with the ASCII-only token pattern of the renderer and the .NET side (python:S6353 is declined).
TOKEN = re.compile(r"##[A-Za-z][A-Za-z0-9_]*\.[A-Za-z][A-Za-z0-9_]*##")
FIELD = re.compile(r'<(?:input|textarea)[^>]*?name="([^"]+)"')


def _load(folder, script):
    spec = importlib.util.spec_from_file_location(script, os.path.join(ROOT, folder, script + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def _per_case_tokens(html):
    return sorted({t for t in TOKEN.findall(html) if not t.startswith("##Office.")})


class BlankFieldsUnitTests(unittest.TestCase):
    def test_first_and_last_name_side_by_side_become_one_field(self):
        html = ('<b><span class="tok">##Patients.FirstName##</span>  '
                '<span class="tok">##Patients.LastName##</span></b>')
        out = blank_fields.blankify(html, "packet.patient.x")
        self.assertEqual(1, out.count("<input"))
        self.assertIn('name="packet.patient.x.patient_name"', out)
        self.assertNotIn("##", out)

    def test_office_tokens_stay_printed(self):
        html = '<span class="tok">##Office.PracticeName##</span>'
        self.assertEqual(html, blank_fields.blankify(html, "p"))

    def test_a_repeated_field_gets_a_numeric_suffix_so_names_stay_unique(self):
        span = '<span class="tok">##Appointments.AvailableDate##</span>'
        out = blank_fields.blankify(span + span, "p")
        self.assertEqual(["p.exam_date", "p.exam_date_2"], FIELD.findall(out))

    def test_a_token_that_fills_a_cell_gets_a_cell_wide_field(self):
        html = '<td class="tok-cell"><span class="tok">##Patients.LastName##</span></td>'
        out = blank_fields.blankify(html, "p", {"##Patients.LastName##": "employee_last"})
        self.assertIn("blk-fill", out)
        self.assertIn('name="p.employee_last"', out)

    def test_an_unknown_token_is_refused_rather_than_printed(self):
        with self.assertRaises(ValueError):
            blank_fields.blankify('<span class="tok">##Nope.Unknown##</span>', "p")


class BlankVariantTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        patient = _load("patient", "build_patient")
        doctor = _load("doctor", "build_doctor")
        cls.patient_sent, cls.patient_blank = patient.build_standard(), patient.build_blank()
        cls.doctor_sent = doctor.build_variant(doctor.VARIANTS["doctor"])
        cls.doctor_blank = doctor.build_variant(doctor.VARIANTS["doctor-blank"])

    def test_the_blank_copies_print_no_per_case_token(self):
        self.assertEqual([], _per_case_tokens(self.patient_blank))
        self.assertEqual([], _per_case_tokens(self.doctor_blank))

    def test_the_sent_packets_still_print_their_tokens(self):
        self.assertTrue(_per_case_tokens(self.patient_sent))
        self.assertTrue(_per_case_tokens(self.doctor_sent))

    def test_new_field_counts(self):
        self.assertEqual(17, len(FIELD.findall(self.patient_blank)) - len(FIELD.findall(self.patient_sent)))
        self.assertEqual(19, len(FIELD.findall(self.doctor_blank)) - len(FIELD.findall(self.doctor_sent)))

    def test_every_sent_field_name_survives_and_text_names_are_not_duplicated(self):
        for sent, blank in ((self.patient_sent, self.patient_blank), (self.doctor_sent, self.doctor_blank)):
            sent_names, blank_names = FIELD.findall(sent), FIELD.findall(blank)
            self.assertTrue(set(sent_names) <= set(blank_names))
            new = [n for n in blank_names if n not in set(sent_names)]
            self.assertLessEqual(len(new), len(set(new)), "a new field name repeats")

    def test_the_blank_variant_is_never_the_sent_file(self):
        self.assertNotEqual(self.patient_sent, self.patient_blank)
        self.assertNotEqual(self.doctor_sent, self.doctor_blank)


if __name__ == "__main__":
    unittest.main()
