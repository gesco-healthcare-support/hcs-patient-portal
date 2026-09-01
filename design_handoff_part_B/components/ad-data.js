/* ============================================================
   Appointment Detail / View — mock detail record + status config.
   ============================================================ */
(function () {
  const DETAIL = {
    type: 'Panel QME', confirmation: 'PQ-24817', panelNumber: 'P-2204',
    location: 'Los Angeles — Wilshire', date: '2026-06-16T09:30', requestedOn: '2026-06-02T14:12',
    patient: { first: 'Maria', middle: '', last: 'Gonzalez', gender: 'Female', dob: '03/22/1984', email: 'mgonzalez@aol.com', cell: '(213) 555-0148', phone: '', ssnMasked: '•••-••-7841', street: '128 W 4th St', unit: 'Apt 5', city: 'Los Angeles', state: 'CA', zip: '90013', language: 'Spanish', interpreter: true, interpreterVendor: 'LA Interpreting Services', referredBy: 'Dr. Alan Reyes' },
    employer: { name: 'Acme Logistics', occupation: 'Warehouse Associate', phone: '(213) 555-0190', street: '900 Alameda St', city: 'Los Angeles', state: 'CA', zip: '90021' },
    applicant: { name: 'Daniel Brooks', firm: 'Brooks & Associates', email: 'dbrooks@brookslaw.com', phone: '(415) 555-0142', fax: '', web: 'brookslaw.com', street: '50 California St', city: 'San Francisco', state: 'CA', zip: '94104' },
    defense: { name: 'Laura Pelton', firm: 'Pelton Defense Group', email: 'lpelton@peltondg.com', phone: '(213) 555-0177', fax: '', web: 'peltondg.com', street: '801 S Figueroa St', city: 'Los Angeles', state: 'CA', zip: '90017' },
    insurance: { name: 'Pacific Mutual Insurance', suite: 'Ste 400', phone: '(800) 555-0123', fax: '', street: '500 Capitol Mall', city: 'Sacramento', state: 'CA', zip: '95814' },
    examiner: { name: 'Karen Whitfield', email: 'kwhitfield@sierraclaims.com', phone: '(916) 555-0190', suite: 'Ste 210', fax: '', street: '770 L St', city: 'Sacramento', state: 'CA', zip: '95814' },
    injuries: [
      { doi: '11/03/2025', cumulative: false, toDoi: '', claim: 'WC24-10480', wcab: 'Los Angeles (WCAB)', adj: 'ADJ-4471102', bodyParts: ['Lower back', 'Right shoulder'] },
      { doi: '01/10/2024', cumulative: true, toDoi: '06/30/2025', claim: 'WC24-10533', wcab: 'Anaheim (WCAB)', adj: 'ADJ-4471980', bodyParts: ['Neck', 'Both wrists'] },
    ],
    authUsers: [
      { first: 'Intake', last: 'Desk', email: 'intake@brookslaw.com', role: 'Applicant Attorney', access: 'edit' },
    ],
    documents: [
      { id: 'd1', name: 'Medical records 2025', file: 'medical_records_2025.pdf', size: '2.4 MB', type: 'Medical Records', status: 'accepted', when: 'Jun 3, 2026', strike: false },
      { id: 'd2', name: 'Panel strike list', file: 'panel_strike_list.pdf', size: '0.6 MB', type: 'Panel Strike List', status: 'pending', when: 'Jun 4, 2026', strike: true },
      { id: 'd3', name: 'Cover letter', file: 'cover_letter.pdf', size: '0.2 MB', type: 'Cover Letter', status: 'rejected', when: 'Jun 4, 2026', rejection: 'Illegible scan — please re-upload a clear, full-page copy.', strike: false },
    ],
    requiredDocs: [
      { name: 'Panel Strike List', received: true },
      { name: 'Medical Records', received: true },
      { name: 'Cover Letter', received: false },
    ],
  };

  // Per-status presentation config
  const STATUSES = {
    pending: {
      key: 'pending', label: 'Pending review', tone: 'pending', banner: 'ad-banner--pending',
      callout: { icon: 'clock', title: 'Awaiting clinic review', body: 'Your request was submitted and is pending staff approval. We’ll email you when it’s confirmed.' },
      actions: ['reschedule_disabled', 'upload', 'summary'], note: null, tlIndex: 0,
    },
    approved: {
      key: 'approved', label: 'Approved', tone: 'approved', banner: 'ad-banner--approved',
      callout: { icon: 'check', title: 'Appointment confirmed', body: 'Please arrive 15 minutes early with a photo ID. You can request a change below.' },
      actions: ['reschedule', 'cancel', 'upload', 'summary'],
      note: { kind: 'approved', title: 'Approval comments', body: 'Approved — please arrive 15 minutes early with photo ID. Bring any imaging on disc if available.' }, tlIndex: 1,
    },
    rejected: {
      key: 'rejected', label: 'Rejected', tone: 'rejected', banner: 'ad-banner--rejected',
      callout: { icon: 'alert', title: 'Request not approved', body: 'See the reason below. You can submit a new request addressing the issue.' },
      actions: ['rerequest', 'upload', 'summary'],
      note: { kind: 'rejected', title: 'Rejection reason', body: 'The selected panel number does not match the QME panel on file. Please re-request with the correct panel number.' }, tlIndex: -1,
    },
    cancelled: {
      key: 'cancelled', label: 'Cancelled', tone: 'rejected', banner: 'ad-banner--cancelled',
      callout: { icon: 'x', title: 'Appointment cancelled', body: 'This appointment was cancelled. You can submit a new request if it’s still needed.' },
      actions: ['rerequest', 'summary'],
      note: { kind: 'rejected', title: 'Cancellation note', body: 'Cancelled at the requester’s request. Contact the clinic if this was made in error.' }, tlIndex: -1,
    },
    rescheduled: {
      key: 'rescheduled', label: 'Rescheduled', tone: 'info', banner: 'ad-banner--rescheduled',
      callout: { icon: 'refresh', title: 'Appointment rescheduled', body: 'This appointment has been rescheduled — the new date and time are shown above.' },
      actions: ['reschedule', 'cancel', 'upload', 'summary'],
      note: { kind: 'approved', title: 'Reschedule note', body: 'Rescheduled to a new available slot. Please arrive 15 minutes early with a photo ID.' },
    },
  };

  const TIMELINE = [
    { key: 'requested', label: 'Requested' },
    { key: 'approved', label: 'Approved' },
    { key: 'checkedin', label: 'Checked in' },
    { key: 'checkedout', label: 'Checked out' },
    { key: 'billed', label: 'Billed' },
  ];

  window.AD = { DETAIL, STATUSES, TIMELINE };
})();
