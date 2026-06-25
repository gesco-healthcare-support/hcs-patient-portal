/* ============================================================
   External-role home — role config + notifications + per-role data.
   Shared base for all 4 external roles. Per-role flags toggle which
   components show (canBook, patient column, DOB filter, etc).
   ============================================================ */
(function () {
  const A = window.MOCK.appointments;

  // ---- Per-role configuration -------------------------------------------
  // Drives the SAME base home; flags add/remove components per role.
  const ROLES = {
    patient: {
      key: 'patient', label: 'Patient',
      name: 'Maria Gonzalez', first: 'Maria', email: 'mgonzalez@aol.com', org: null,
      listTitle: 'My appointment requests',
      listSub: 'Track the status of your evaluation requests',
      heroSub: 'Book a new evaluation or check the status of your existing requests.',
      canBook: true, canReeval: true,
      showPatientCol: false, showDob: false,
      defaultView: 'cards',
      // "subject" framing for the involvement column
      relationLabel: null,
    },
    applicantAttorney: {
      key: 'applicantAttorney', label: 'Applicant Attorney',
      name: 'Daniel Brooks', first: 'Daniel', email: 'dbrooks@brookslaw.com', org: 'Brooks & Associates',
      listTitle: 'Case appointments',
      listSub: 'Evaluations across the applicants you represent',
      heroSub: 'Book evaluations for your applicants and track every request in one place.',
      canBook: true, canReeval: true,
      showPatientCol: true, showDob: true,
      defaultView: 'table',
      relationLabel: 'Applicant',
    },
    defenseAttorney: {
      key: 'defenseAttorney', label: 'Defense Attorney',
      name: 'Laura Pelton', first: 'Laura', email: 'lpelton@peltondg.com', org: 'Pelton Defense Group',
      listTitle: 'Case appointments',
      listSub: 'Evaluations on the claims you are defending',
      heroSub: 'Review evaluation requests, respond to changes, and manage documents for your claims.',
      canBook: false, canReeval: false,
      showPatientCol: true, showDob: true,
      defaultView: 'table',
      relationLabel: 'Applicant',
    },
    claimExaminer: {
      key: 'claimExaminer', label: 'Claim Examiner',
      name: 'Karen Whitfield', first: 'Karen', email: 'kwhitfield@sierraclaims.com', org: 'Sierra Claims Admin',
      listTitle: 'Claim appointments',
      listSub: 'Evaluations on the claims you administer',
      heroSub: 'Oversee evaluation requests and respond to change and document requests on your claims.',
      canBook: false, canReeval: false,
      showPatientCol: true, showDob: true,
      defaultView: 'table',
      relationLabel: 'Applicant',
    },
  };

  // ---- Per-role appointment slices --------------------------------------
  // Patient sees only THEIR requests (same person, a few evaluations).
  // Attorneys / examiner see the full multi-patient caseload.
  function patientRows() {
    const base = A.slice(0, 4).map((a, i) => ({
      ...a,
      patient: { ...A[0].patient }, // all the patient's own
    }));
    // vary types/statuses for realism
    const types = ['Panel QME', 'QME Re-Evaluation', 'QME Follow-up', 'Supplemental Report'];
    const sids = [2, 1, 12, 5];
    return base.map((a, i) => ({
      ...a,
      type: types[i], statusId: sids[i], status: window.MOCK.STATUS[sids[i]],
    }));
  }

  function rowsFor(roleKey) {
    if (roleKey === 'patient') return patientRows();
    return A; // attorneys + examiner: full caseload
  }

  // ---- Notifications (domain-accurate event types) ----------------------
  const NOTIFS = [
    { id: 'n1', tone: 'approved', icon: 'check', unread: true,
      title: 'Appointment approved',
      body: 'Panel QME for Maria Gonzalez (PQ-24817) was approved for Jun 16, 9:30 AM.',
      time: '12m ago' },
    { id: 'n2', tone: 'pending', icon: 'refresh', unread: true,
      title: 'Reschedule requested',
      body: 'A reschedule was requested on FU-24779 — awaiting clinic confirmation.',
      time: '1h ago' },
    { id: 'n3', tone: 'info', icon: 'doc', unread: true,
      title: 'Documents requested',
      body: 'Upload the medical records packet for AM-24820 before Jun 16.',
      time: '3h ago' },
    { id: 'n4', tone: 'purple', icon: 'user', unread: false,
      title: 'Consent required',
      body: 'Opposing counsel consent is pending for the change request on RE-24788.',
      time: 'Yesterday' },
    { id: 'n5', tone: 'teal', icon: 'check', unread: false,
      title: 'Checked out',
      body: 'Anthony Russo was checked out of SR-24690. Report is being prepared.',
      time: '2d ago' },
  ];

  window.EXT = { ROLES, rowsFor, NOTIFS };
})();
