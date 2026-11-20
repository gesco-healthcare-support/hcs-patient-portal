"""blank_fields.py in the main Python suite (the one whose coverage Sonar counts).

The same module is also tested beside the generators (tools/packet-templates/tests/test_blank_fields.py), which runs
the real builders. These are the pure cases: what a token span becomes, and what is refused.
"""

import pathlib
import sys
import unittest

SHARED = pathlib.Path(__file__).resolve().parents[2] / "tools" / "packet-templates" / "shared"
sys.path.insert(0, str(SHARED))
import blank_fields  # noqa: E402


class BlankifyTests(unittest.TestCase):
    def test_an_office_token_stays_printed(self):
        html = '<span class="tok">##Office.PracticeName##</span>'
        self.assertEqual(blank_fields.blankify(html, "p"), html)

    def test_an_unknown_token_is_refused(self):
        with self.assertRaises(ValueError):
            blank_fields.blankify('<span class="tok">##Nope.Unknown##</span>', "p")

    def test_a_token_alone_in_a_cell_gets_a_cell_wide_field_named_by_the_override(self):
        html = '<td class="tok-cell"><span class="tok">##Patients.LastName##</span></td>'
        out = blank_fields.blankify(html, "p", {"##Patients.LastName##": "employee_last"})
        self.assertTrue(out.startswith('<td class="tok-cell">'))
        self.assertIn("blk-fill", out)
        self.assertIn('name="p.employee_last"', out)

    def test_a_token_in_running_text_gets_a_fixed_width_line(self):
        out = blank_fields.blankify('<span class="tok">##Patients.DateOfBirth##</span>', "p")
        self.assertIn('style="width:1.2in"', out)
        self.assertNotIn("blk-fill", out)

    def test_a_repeated_field_gets_a_numeric_suffix(self):
        span = '<span class="tok">##Appointments.AvailableDate##</span>'
        out = blank_fields.blankify(span + span, "p")
        self.assertIn('name="p.exam_date"', out)
        self.assertIn('name="p.exam_date_2"', out)

    def test_first_and_last_name_side_by_side_become_one_patient_name_field(self):
        html = ('<span class="tok">##Patients.FirstName##</span>&nbsp;'
                '<span class="tok">##Patients.LastName##</span>')
        out = blank_fields.blankify(html, "p")
        self.assertEqual(out.count("<input"), 1)
        self.assertIn('name="p.patient_name"', out)

    def test_the_hyphenated_fee_ticket_type_is_one_field(self):
        html = '<span class="tok">##Appointments.EvaluationKind##-##Appointments.AppointmentTypeCode##</span>'
        self.assertIn('name="p.type"', blank_fields.blankify(html, "p"))


if __name__ == "__main__":
    unittest.main()
