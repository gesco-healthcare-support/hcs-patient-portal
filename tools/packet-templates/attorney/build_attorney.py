"""Attorney/Claim-Examiner packet templates -- single generator, two flat outputs.

Notices (NOT forms): no fillable controls -> the rendered PDF has zero AcroForm fields.
Pre-fill tokens are kept EXACT in <span class="tok">##Group.Field##</span> (incl. the
misspelled ##Appointments.AppointmenTime## from the original DOCX).

Outputs:
  - ame_ime.html : attorney/adjustor notice + patient notice            (AME, IME)
  - pqme.html    : the same two notices + DWC QME Appointment Notification Form  (Panel QME)

Two NEW derived tokens added per request (not in the legacy DOCX; need resolver wiring):
  ##Patients.InterpreterRequired##  -> "Yes"/"No" derived from language/vendor on the patient
  ##Patients.InterpreterLanguage##  -> AppointmentLanguage.Name (fallback OthersLanguageName)
"""

_SECT = "&#167;"   # section sign

CSS = r"""
  @page letterpage {
    size: Letter; margin: 0.7in 0.85in 1.1in 0.85in;
    @bottom-center { content: element(lfoot); }
  }
  @page { size: Letter; margin: 0.5in 0.6in; }   /* default: the QME form */
  * { box-sizing: border-box; }
  html, body { margin: 0; padding: 0; }
  body { font-family: "Carlito","Calibri","Liberation Sans","Arial",sans-serif;
         font-size: 11pt; color: #000; line-height: 1.3; }
  u { text-decoration: underline; }
  .page { break-after: page; }
  .page:last-child { break-after: auto; }
  .letterpage { page: letterpage; }
  .tok { font-style: italic; color: #444; }      /* pre-fill substitution point */

  /* ---- shared letterhead + running footer (the two cover letters) ---- */
  .lh { margin-bottom: 14px; }
  .lh .nm { font-size: 15pt; font-weight: bold; }
  .lh .fellow { font-size: 8.5pt; letter-spacing: .3px; }
  .lfoot { position: running(lfoot); text-align: center; font-size: 9pt; line-height: 1.35; }

  /* ---- cover-letter body (attorney notice is content-heavy -> compact, ~first cut) ---- */
  .ltr p { margin: 9px 0; }
  .ltr .recips { margin: 8px 0; line-height: 1.5; }
  .ltr .reblock { margin: 10px 0; line-height: 1.45; }
  .ltr .addr { text-align: center; margin: 10px 0; line-height: 1.4; }
  .ltr ul { margin: 8px 0; padding-left: 28px; }
  .ltr li { margin: 7px 0; }
  .ltr .signoff { margin-top: 16px; }
  /* patient letter has less content -> larger font + spacing to fill its page */
  .ltr.fill { font-size: 12pt; line-height: 1.5; }
  .ltr.fill p { margin: 16px 0; }
  .ltr.fill .addr { margin: 18px 0; line-height: 1.5; }
  .ltr.fill .signoff { margin-top: 22px; }

  /* ---- QME Appointment Notification Form (compact; long ##tokens## wrap in-cell) ---- */
  .qme { font-size: 9.5pt; line-height: 1.3; }
  .qme .title { text-align: center; line-height: 1.25; margin-bottom: 6px; }
  .qme .title .l1 { font-weight: bold; }
  .qme .title .l2 { font-weight: bold; }
  .qme .title .l3 { font-weight: bold; font-size: 12pt; }
  .qme .intro { font-size: 7.5pt; text-align: justify; line-height: 1.25; margin: 6px 0 7px; }
  .qme table { width: 100%; border-collapse: collapse; margin: 0; table-layout: fixed; }
  .qme td { border: 1px solid #000; padding: 6px 5px; vertical-align: top; }
  .qme .sect { background: #d9d9d9; font-weight: bold; text-align: center; padding: 4px; }
  .qme .req { font-weight: normal; font-style: italic; font-size: 8pt; }
  .qme .val { min-height: 16px; }                 /* token value sits on top */
  .qme .tok { word-break: break-all; }            /* long ##tokens## wrap inside their box */
  .qme .flbl { font-size: 7.5pt; font-style: italic; color: #222; }   /* field label beneath */
  .qme .appt { border: 1px solid #000; padding: 7px 6px; margin: 0; }
  .qme .appt .row { margin: 6px 0; }
  .qme .qline { margin: 8px 0; }
  .qme .sig { display: inline-block; border-bottom: 1px solid #000; min-width: 2.4in; }
  .qme .note { font-style: italic; font-size: 8pt; text-align: justify; margin-top: 13px; line-height: 1.3; }
"""


def tok(t):
    return f'<span class="tok">{t}</span>'


def _letterhead():
    return ('<div class="lh"><div class="nm">Yuri Falkinstein, M.D., FAAOS</div>'
            '<div class="fellow">FELLOW, AMERICAN ACADEMY OF ORTHOPAEDIC SURGEONS</div></div>')


def _lfoot():
    return ('<div class="lfoot">SCHEDULING: (818) 582-2600<br>'
            'P.O. Box 261656, Encino, CA 91426<br>FAX: (818) 855-2466</div>')


def attorney_notice():
    """Page 1 -- notice to applicant/defense attorneys + adjustor."""
    return (
        '<div class="ltr">'
        + _letterhead()
        + f'<p>{tok("##Others.DateNow##")}</p>'
        + '<div class="recips">'
        + f'Applicant Attorney : {tok("##PatientAttorneys.AttorneyName##")}<br>'
        + f'Defense Attorney: {tok("##DefenseAttorneys.AttorneyName##")}<br>'
        + f'Adjustor Name : {tok("##InjuryDetails.ClaimExaminerName##")}</div>'
        + '<div class="reblock">'
        + f'Re: Applicant: {tok("##Patients.FirstName##")} {tok("##Patients.LastName##")}<br>'
        + f'Employer: {tok("##EmployerDetails.EmployerName##")}<br>'
        + f'DOI: {tok("##InjuryDetails.DateOfInjury##")}<br>'
        + f'Claim: {tok("##InjuryDetails.ClaimNumber##")}</div>'
        + f'<p>Please be advised that {tok("##Patients.FirstName##")} {tok("##Patients.LastName##")} '
        + f'has been scheduled with Yuri Falkinstein, M.D. on <u>{tok("##Appointments.AvailableDate##")}</u> at '
        + f'<u>{tok("##Appointments.AppointmenTime##")}</u>. The appointment will be held at:</p>'
        + '<div class="addr">West Coast Spine Institute<br>'
        + f'{tok("##Appointments.Location##")}<br>{tok("##Appointments.LocationAddress##")}<br>'
        + f'{tok("##Appointments.LocationCity##")}, {tok("##Appointments.LocationState##")},<br>'
        + f'{tok("##Appointments.LocationZipCode##")}</div>'
        + '<p>In order to assist you in a timely manner and comply with Labor Code section 4060 we are '
        + 'requesting the following:</p>'
        + '<ul>'
        + '<li>A Fully executed Joint/Advocacy Letter and all medical records <u>MUST</u> be received no '
        + 'later than 30 days before the patient\'s scheduled appointment. <u>If this does not occur, '
        + 'the above appointment may be RESCHEDULED</u>.</li>'
        + '<li><u>If the medical records are not received within the timeline given, it may become necessary '
        + 'to issue a supplemental report and/or the patient will be rescheduled for a second appointment.</u></li>'
        + '<li>All overnight packages must be sent to 16530 Ventura Blvd., Suite 130, Encino, CA 91436. '
        + 'All other correspondence must be sent to P.O. Box 261656 Encino, CA 91426.</li>'
        + f'<li>The Parking fee for WEST COAST SPINE {tok("##Appointments.Location##")} is $ '
        + f'{tok("##Appointments.LocationParkingFee##")} .Please be sure the patient is given a map to our location.</li>'
        + '<li><u>Missed Appointment</u> charge is $503.75. If the appointment is <u>cancelled within six (6) days</u> '
        + 'of the scheduled appointment, we will charge $503.75 plus the time spent reviewing the medical '
        + 'records per the labor code.</li>'
        + '</ul>'
        + '<div class="signoff"><p>Thank you,</p><p>APPOINTMENT DEPARTMENT</p></div>'
        + _lfoot()
        + '</div>')


def patient_notice():
    """Page 2 -- notice to the patient."""
    return (
        '<div class="ltr fill">'
        + _letterhead()
        + f'<p>{tok("##Others.DateNow##")}</p>'
        + '<p>' + f'{tok("##Patients.FirstName##")} {tok("##Patients.LastName##")}<br>'
        + f'{tok("##Patients.Street##")}<br>'
        + f'{tok("##Patients.City##")}, {tok("##Patients.State##")} {tok("##Patients.ZipCode##")}</p>'
        + f'<p>Dear Mr. /Mrs.: {tok("##Patients.FirstName##")} {tok("##Patients.LastName##")}</p>'
        + f'<p>Please be advised that an appointment has been scheduled for you to see Yuri Falkinstein, M.D. on '
        + f'<u>{tok("##Appointments.AvailableDate##")}</u> at <u>{tok("##Appointments.AppointmenTime##")}</u>. '
        + 'Your appointment will be held at:</p>'
        + '<div class="addr">WEST COAST SPINE INSTITUTE<br>'
        + f'{tok("##Appointments.Location##")}<br>{tok("##Appointments.LocationAddress##")}<br>'
        + f'{tok("##Appointments.LocationCity##")}, {tok("##Appointments.LocationState##")} '
        + f'{tok("##Appointments.LocationZipCode##")}</div>'
        + f'<p>The parking fee for this location is $ {tok("##Appointments.LocationParkingFee##")}.</p>'
        + '<p>Please make sure you keep this appointment as it is the most important medical appointment for your '
        + 'case. Please allow ample time (minimum 3 hours) to be at our office.</p>'
        + '<p>Please review and compare this appointment with any other appointment letter you may have received. '
        + 'In case of any discrepancies, please contact our office immediately for clarification.</p>'
        + '<p>Kindly note that <u>you must check in at the above address 15 minutes prior</u> to your scheduled '
        + 'appointment time with proof of identification.</p>'
        + '<p>It is necessary that you contact our office at 818-582-2600, 10 days prior to your appointment, for a '
        + 'detailed history of your injury. This will save you time at your scheduled appointment.</p>'
        + '<p>If you have no knowledge of this appointment, please contact your attorney ASAP.</p>'
        + '<div class="signoff"><p>Thank you,</p><p>APPOINTMENT DEPARTMENT</p></div>'
        + _lfoot()
        + '</div>')


def _qcell(token, label, span=1):
    return (f'<td colspan="{span}"><div class="val">{tok(token)}</div>'
            f'<div class="flbl">{label}</div></td>')


def qme_form():
    """DWC QME Appointment Notification Form (flat; values derived from appointment data)."""
    return (
        '<div class="qme">'
        + '<div class="title"><div class="l1">State of California</div>'
        + '<div class="l2">Division of Workers&#39; Compensation-Medical Unit</div>'
        + '<div class="l3">QME Appointment Notification Form</div></div>'
        + f'<p class="intro">Please complete this form in its entirety .The Administrative Director requires that '
        + 'you serve this appointment notification form on the employee and the claims administrator, or, if none '
        + 'the employer, and their attorneys in a represented case, if known, within five (5) business days after '
        + 'having scheduled the injured worker to be seen for a QME comprehensive medical-legal evaluation. You may '
        + 'not cancel the appointment less than six (6) calendar days prior to the appointment date, except for good '
        + f'cause (See, 8 Cal. Code Regs. {_SECT}34). If you reschedule an appointment, review regulation 34 and the '
        + f'ethical rules in regulation 41 (See, 8 Cal Code Regs. {_SECT}{_SECT} 34, 41(a) (7) and (a)</p>'
        # Employee Information
        + '<table>'
        + '<tr><td class="sect" colspan="4"><u>Employee Information '
        + '<span class="req">(Completion of this section is required)</span></u></td></tr>'
        + '<tr>' + _qcell("##Patients.FirstName## ##Patients.LastName##", "Employee Name", 3)
        + _qcell("##Patients.PhoneNumber##", "Ph Phone Number", 1) + '</tr>'
        + '<tr>' + _qcell("##Patients.Street##", "Employee Street Address", 1)
        + _qcell("##Patients.City##", "Employee City", 1) + _qcell("##Patients.State##", "State", 1)
        + _qcell("##Patients.ZipCode##", "Zip Code", 1) + '</tr>'
        + '<tr>' + _qcell("##InjuryDetails.DateOfInjury##", "Date of Injury", 2)
        + _qcell("##Appointments.PanelNumber##", "Panel Number", 1)
        + _qcell("##InjuryDetails.ClaimNumber##", "Claim or Case Number", 1) + '</tr>'
        # Employer Information
        + '<tr><td class="sect" colspan="4"><u>Employer Information</u></td></tr>'
        + '<tr>' + _qcell("##EmployerDetails.EmployerName##", "Employer Name", 4) + '</tr>'
        + '<tr>' + _qcell("##EmployerDetails.Street##", "Employer Street Address", 1)
        + _qcell("##EmployerDetails.City##", "Employer City", 1) + _qcell("##EmployerDetails.State##", "State", 1)
        + _qcell("##EmployerDetails.Zip##", "Zip code", 1) + '</tr>'
        # Claims Administrator Information
        + '<tr><td class="sect" colspan="4"><u>Claims Administrator Information</u> '
        + '<span class="req">(Completion of this section is required)</span></td></tr>'
        + '<tr>' + _qcell("##InjuryDetails.ClaimExaminerName##", "Claims Administrator Name (Insert the name of person handling the claim)", 3)
        + _qcell("##InjuryDetails.ClaimExaminerPhoneNumber##", "Phone Number", 1) + '</tr>'
        + '<tr>' + _qcell("##InjuryDetails.PrimaryInsuranceName##", "Claims Administrator Company (Insert the name of Company handling the claim)", 3)
        + _qcell("##InjuryDetails.PrimaryInsurancePhoneNumber##", "Phone Number", 1) + '</tr>'
        + '<tr>' + _qcell("##InjuryDetails.ClaimExaminerStreet##", "Claim Administrator Street Address", 1)
        + _qcell("##InjuryDetails.ClaimExaminerCity##", "Claim Administrator City", 1)
        + _qcell("##InjuryDetails.ClaimExaminerState##", "State", 1)
        + _qcell("##InjuryDetails.ClaimExaminerZip##", "Zip code", 1) + '</tr>'
        + '</table>'
        # Appointment Information (free-form block)
        + '<table><tr><td class="sect" colspan="4"><u>Appointment Information</u> '
        + '<span class="req">(Completion of this section is required)</span></td></tr></table>'
        + '<div class="appt">'
        + f'<div class="row">Date of Appointment call: {tok("##Appointments.AppointmentCreatedDate##")}'
        + f'&nbsp;&nbsp;&nbsp;&nbsp; Date of Appointment: {tok("##Appointments.AvailableDate##")}</div>'
        + f'<div class="row">Time of Appointment: {tok("##Appointments.AppointmenTime##")}</div>'
        + '</div>'
        + '<table>'
        + '<tr>' + _qcell("##Appointments.LocationAddress##", "Examination Address", 1)
        + _qcell("##Appointments.LocationCity##", "Examination City", 1)
        + _qcell("##Appointments.LocationState##", "State", 1)
        + _qcell("##Appointments.LocationZipCode##", "Zip code", 1) + '</tr>'
        + '</table>'
        + f'<div class="qline">If an interpreter is required? {tok("##Patients.InterpreterRequired##")} '
        + f'.If an interpreter required, indicate language: {tok("##Patients.InterpreterLanguage##")}</div>'
        + '<div class="qline">QME Name: <u>YURI FALKINSTEIN, MD</u>&nbsp;&nbsp; QME Street Address: <u>P.O. BOX 261656</u>'
        + '&nbsp;&nbsp; QME City: <u>Encino</u>&nbsp;&nbsp; Zip code: <u>91426</u></div>'
        + f'<div class="qline" style="margin-top:8px">Date Signed: {tok("##Others.DateNow##")}'
        + '&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; Signature of the QME: <span class="sig"></span></div>'
        + '<div class="note">Note to Claims Administrator: The Administrative Director\'s regulation 10160 requires '
        + 'you to forward a completed, DWC-AD form 101(DEU) (Request for Summary Rating Determination of Qualified '
        + f'Medical Evaluator\'s Report) (see, 8 Cal. Code Regs. {_SECT}{_SECT} 10160 and 10161) together with all '
        + 'medical reports and medical records prior to the scheduled examination with the QME. You must also provide '
        + 'the employee with a DWC-AD form 100 (DEU) (Employee\'s Disability Questionnaire)(See, 8 Cal. Code Regs. '
        + f'{_SECT}{_SECT} 10160 and 10161) prior to the examination.</div>'
        + '</div>')


def build(target):
    if target == "ame_ime":
        pages = [attorney_notice(), patient_notice()]
        cls = ["letterpage", "letterpage"]
    elif target == "pqme":
        pages = [attorney_notice(), patient_notice(), qme_form()]
        cls = ["letterpage", "letterpage", ""]
    else:
        raise SystemExit(f"unknown target {target}")
    body = "\n".join(f'<div class="page {c}">\n{p}\n</div>' for p, c in zip(pages, cls))
    html = ('<!DOCTYPE html>\n<html lang="en"><head><meta charset="utf-8">'
            f'<title>{target}</title>\n<style>' + CSS + '</style></head>\n<body>\n' + body + '\n</body></html>')
    with open(f"{target}.html", "w", encoding="utf-8") as f:
        f.write(html)
    print(f"wrote {target}.html")


if __name__ == "__main__":
    build("ame_ime")
    build("pqme")
