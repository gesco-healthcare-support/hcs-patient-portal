/* ============================================================
   Request an Appointment / Re-evaluation — config + lookups.
   Reuses window.MOCK + window.EXT. Defines booker scenarios for all
   four external roles (Patient / AA / DA / CE), each of which CAN book.
   ============================================================ */
(function () {
  // ---- Booker scenarios (who is filling the form) -----------------------
  // All four external roles can book. The booker drives which sections are
  // pre-filled / locked / hidden, mirroring the Angular appointment-add logic.
  const BOOKERS = {
    patient: {
      key: 'patient', label: 'Patient', name: 'Maria Gonzalez', first: 'Maria',
      email: 'mgonzalez@aol.com', org: null,
      isPatient: true, isNonPatient: false,
      // self-booking → demographics are the booker's own; no existing-patient picker
      patientPicker: false,
      // attorney sections optional (toggle), nothing pre-filled
      lockAttorney: null,
      hideAttorneys: false, hideAuthorizedUsers: false,
      interpreterPrompt: 'Do you need an interpreter?',
    },
    applicantAttorney: {
      key: 'applicantAttorney', label: 'Applicant Attorney', name: 'Daniel Brooks', first: 'Daniel',
      email: 'dbrooks@brookslaw.com', org: 'Brooks & Associates',
      isPatient: false, isNonPatient: true,
      patientPicker: true,
      lockAttorney: 'applicant',   // AA card mandatory + email locked (themselves)
      hideAttorneys: false, hideAuthorizedUsers: false,
      interpreterPrompt: 'Does the patient need an interpreter?',
      prefillAttorney: {
        applicantFirstName: 'Daniel', applicantLastName: 'Brooks', applicantEmail: 'dbrooks@brookslaw.com',
        applicantFirmName: 'Brooks & Associates', applicantPhoneNumber: '(415) 555-0142',
        applicantCity: 'San Francisco', applicantStateId: 'CA', applicantZipCode: '94104',
      },
    },
    defenseAttorney: {
      key: 'defenseAttorney', label: 'Defense Attorney', name: 'Laura Pelton', first: 'Laura',
      email: 'lpelton@peltondg.com', org: 'Pelton Defense Group',
      isPatient: false, isNonPatient: true,
      patientPicker: true,
      lockAttorney: 'defense',     // DA card mandatory + email locked (themselves)
      hideAttorneys: false, hideAuthorizedUsers: false,
      interpreterPrompt: 'Does the patient need an interpreter?',
      prefillAttorney: {
        defenseFirstName: 'Laura', defenseLastName: 'Pelton', defenseEmail: 'lpelton@peltondg.com',
        defenseFirmName: 'Pelton Defense Group', defensePhoneNumber: '(213) 555-0177',
        defenseCity: 'Los Angeles', defenseStateId: 'CA', defenseZipCode: '90017',
      },
    },
    claimExaminer: {
      key: 'claimExaminer', label: 'Claim Examiner', name: 'Karen Whitfield', first: 'Karen',
      email: 'kwhitfield@sierraclaims.com', org: 'Sierra Claims Admin',
      isPatient: false, isNonPatient: true,
      patientPicker: true,
      lockAttorney: null,
      // B11 parity: Claim Examiner (=Adjuster) bookers hide attorney + authorized-user sections
      hideAttorneys: true, hideAuthorizedUsers: true,
      interpreterPrompt: 'Does the patient need an interpreter?',
      prefillExaminer: {
        appointmentClaimExaminerName: 'Karen Whitfield', appointmentClaimExaminerEmail: 'kwhitfield@sierraclaims.com',
        appointmentClaimExaminerPhoneNumber: '(916) 555-0190', appointmentClaimExaminerCity: 'Sacramento',
        appointmentClaimExaminerStateId: 'CA', appointmentClaimExaminerZip: '95814',
      },
    },
    // Internal staff booking on behalf of a patient (renders inside the staff shell).
    staff: {
      key: 'staff', label: 'Staff Supervisor', name: 'Sandra Cole', first: 'Sandra',
      email: 'scole@falkinstein.com', org: 'Falkinstein Orthopedics',
      isPatient: false, isNonPatient: true, internal: true,
      patientPicker: true,
      lockAttorney: null,
      hideAttorneys: false, hideAuthorizedUsers: false,
      interpreterPrompt: 'Does the patient need an interpreter?',
    },
  };

  // ---- Lookups ----------------------------------------------------------
  const APPT_TYPES = [
    { id: 't-pqme', name: 'Panel QME', pqme: true },
    { id: 't-ame', name: 'AME Evaluation', pqme: false },
    { id: 't-reval', name: 'QME Re-Evaluation', pqme: false },
    { id: 't-follow', name: 'QME Follow-up', pqme: false },
    { id: 't-supp', name: 'Supplemental Report', pqme: false },
  ];
  const LOCATIONS = window.MOCK.LOCATIONS.map((n, i) => ({ id: 'loc-' + i, name: n }));
  const STATES = ['CA', 'NV', 'AZ', 'OR', 'WA', 'TX', 'NY', 'FL'];
  const LANGUAGES = ['English', 'Spanish', 'Mandarin', 'Vietnamese', 'Tagalog', 'Korean', 'Armenian'];
  const WCAB_OFFICES = [
    { id: 'w1', displayName: 'Los Angeles (WCAB)' },
    { id: 'w2', displayName: 'Anaheim (WCAB)' },
    { id: 'w3', displayName: 'San Diego (WCAB)' },
    { id: 'w4', displayName: 'Sacramento (WCAB)' },
    { id: 'w5', displayName: 'Oakland (WCAB)' },
    { id: 'w6', displayName: 'Fresno (WCAB)' },
  ];
  const DOC_TYPES = [
    { id: 'd1', displayName: 'Medical Records' },
    { id: 'd2', displayName: 'Cover Letter' },
    { id: 'd3', displayName: 'Panel Strike List' },
    { id: 'd4', displayName: 'Deposition Transcript' },
    { id: 'd5', displayName: 'Correspondence' },
  ];
  const ACCESS_TYPES = [
    { value: 'view', label: 'View only' },
    { value: 'edit', label: 'View & edit' },
  ];
  const AUTH_ROLES = ['Patient', 'Applicant Attorney', 'Defense Attorney', 'Claim Examiner'];

  const TIME_SLOTS = ['8:30 AM', '9:00 AM', '9:30 AM', '10:15 AM', '11:00 AM', '1:00 PM', '1:45 PM', '2:30 PM', '3:15 PM'];

  // Existing patients (for non-patient bookers' picker)
  const EXISTING_PATIENTS = window.MOCK.appointments.slice(0, 8).map(a => ({
    id: a.id, displayName: a.patient.firstName + ' ' + a.patient.lastName + ' · DOB ' +
      new Date(a.dateOfInjury).toLocaleDateString('en-US'),
    first: a.patient.firstName, last: a.patient.lastName, email: a.patient.email,
  }));

  window.RA = { BOOKERS, APPT_TYPES, LOCATIONS, STATES, LANGUAGES, WCAB_OFFICES, DOC_TYPES, ACCESS_TYPES, AUTH_ROLES, TIME_SLOTS, EXISTING_PATIENTS };
})();
