"""Blank (reusable) variants of a packet: every per-case printed token becomes an EMPTY form field.

The Case Tracker (CT) downloads a blank per-office copy of the patient and doctor packets and fills it from what a
patient tells staff on an iPad. So in the blank variant the values the sent packet PRINTS (name, date, confirmation
number, ...) are fields instead, each with its own unique dotted name (WeasyPrint does not merge same-named inputs
into one field, and a CT that fills by name needs exactly one field per name).

The SENT packets are never touched: the builders call `blankify` only for the blank variant, and a golden hash pins
the sent files byte-for-byte.

`##Office.*##` tokens are NOT per-case; they stay printed text, filled from the office letterhead as today.
"""

import re

# One printed token span, as both builders emit it. A span can hold several tokens joined by a hyphen
# ("##Appointments.EvaluationKind##-##Appointments.AppointmentTypeCode##", the fee ticket's type).
_TOKEN = r"##[A-Za-z][A-Za-z0-9_]*\.[A-Za-z][A-Za-z0-9_]*##"
_SPAN = re.compile(rf'(<td class="tok-cell">)?<span class="tok">((?:{_TOKEN}-?)+)</span>')
# First name and last name printed side by side are ONE field: the patient's name.
_NAME_PAIR = re.compile(
    rf'<span class="tok">##Patients\.FirstName##</span>(?:\s|&nbsp;)*<span class="tok">##Patients\.LastName##</span>')

# token -> field name (the last segment of the dotted name).
FIELD_FOR_TOKEN = {
    "##Patients.FirstName##": "first_name",
    "##Patients.LastName##": "last_name",
    "##Patients.MiddleName##": "middle_initial",
    "##Patients.DateOfBirth##": "dob",
    "##Patients.SocialSecurityNumber##": "ssn",
    "##InjuryDetails.DateOfInjury##": "date_of_injury",
    "##InjuryDetails.ClaimNumber##": "claim_no",
    "##InjuryDetails.PrimaryInsuranceName##": "claims_administrator",
    "##InjuryDetails.ClaimExaminerName##": "claims_representative",
    "##EmployerDetails.EmployerName##": "employer_name",
    "##EmployerDetails.Street##": "employer_street",
    "##EmployerDetails.City##": "employer_city",
    "##EmployerDetails.State##": "employer_state",
    "##EmployerDetails.Zip##": "employer_zip",
    "##Appointments.RequestConfirmationNumber##": "account",
    "##Appointments.AvailableDate##": "exam_date",
    "##Appointments.EvaluationKind##-##Appointments.AppointmentTypeCode##": "type",
}
NAME_FIELD = "patient_name"

# Widths of the line a field sits on, by field. Anything else falls back to the default.
_WIDTH = {"patient_name": "2.4in", "account": "1.1in", "type": "1.3in", "dob": "1.2in", "ssn": "1.2in",
          "exam_date": "1.2in", "date_of_injury": "1.2in"}
_DEFAULT_WIDTH = "1.6in"

CSS = r"""
  /* blank variant only: the line the printed value used to sit on is a wrapper, because CSS on an
     input is lost in the PDF */
  .blk { display: inline-block; vertical-align: bottom; border-bottom: 1px solid #000; height: 1.15em; }
  .blk input[type="text"] { display: block; width: 100%; height: 1.15em; border: none; background: transparent; }
  .blk.blk-fill { display: block; width: 100%; }
"""


def _field_html(name, field, fill):
    style = "" if fill else f' style="width:{_WIDTH.get(field, _DEFAULT_WIDTH)}"'
    cls = "blk blk-fill" if fill else "blk"
    return f'<span class="{cls}"{style}><input type="text" name="{name}"></span>'


def blankify(html, prefix, overrides=None):
    """Replace every per-case token span in one page's HTML with an empty, uniquely named input.

    prefix     e.g. "packet.patient.release"; the field name is "<prefix>.<field>".
    overrides  token -> field name, for a page that names one differently (the return-to-work page's "employee_last").

    A token that is the whole content of a table cell (td.tok-cell) gets an input that fills the cell; any other
    token gets a line of fixed width, so the text around it does not move.

    A field that would repeat on the same page gets a numeric suffix, so every name stays unique.
    """
    overrides = overrides or {}
    seen = {}

    def unique(field):
        seen[field] = seen.get(field, 0) + 1
        return field if seen[field] == 1 else f"{field}_{seen[field]}"

    def pair(_match):
        field = unique(NAME_FIELD)
        return _field_html(f"{prefix}.{field}", NAME_FIELD, False)

    def single(match):
        cell, tokens = match.group(1), match.group(2)
        if tokens.startswith("##Office."):
            return match.group(0)
        base = overrides.get(tokens) or FIELD_FOR_TOKEN.get(tokens)
        if base is None:
            raise ValueError(f"blank variant: no field name for token {tokens}")
        return (cell or "") + _field_html(f"{prefix}.{unique(base)}", base, cell is not None)

    return _SPAN.sub(single, _NAME_PAIR.sub(pair, html))
