/* ============================================================
   Mock data for the Appointment Portal prototype
   Workers'-comp QME / medical-legal domain.
   ============================================================ */
(function () {
  // AppointmentStatusType — 5-status model (Pending · Approved · Rejected ·
  // Cancelled · Rescheduled). Checked-in/out, Billed, No-show are deprecated.
  const STATUS = {
    1: { key: 'Pending',                label: 'Pending',               tone: 'pending'  },
    2: { key: 'Approved',               label: 'Approved',              tone: 'approved' },
    3: { key: 'Rejected',               label: 'Rejected',              tone: 'rejected' },
    5: { key: 'CancelledNoBill',        label: 'Cancelled',             tone: 'neutral'  },
    6: { key: 'CancelledLate',          label: 'Cancelled (Late)',      tone: 'neutral'  },
    12:{ key: 'RescheduleRequested',    label: 'Reschedule Requested',  tone: 'pending'  },
    13:{ key: 'CancellationRequested',  label: 'Cancellation Requested',tone: 'pending'  },
    14:{ key: 'InfoRequested',          label: 'Info Requested',        tone: 'purple'   },
  };

  const TYPES = ['Panel QME', 'AME Evaluation', 'QME Re-Evaluation', 'QME Follow-up', 'Supplemental Report'];
  const LOCATIONS = ['Los Angeles — Wilshire', 'San Diego — Hillcrest', 'Sacramento — Midtown', 'Fresno — Herndon', 'Oakland — Lakeside'];
  const GENDER = { 1: 'Male', 2: 'Female' };

  function mask(ssn) { return '•••-••-' + ssn.slice(-4); }

  // 16 appointment rows
  const raw = [
    ['Panel QME',          'Maria',    'Gonzalez',  2, '7841', 'PQ-24817', '2026-06-09T09:30', 1,  'Los Angeles — Wilshire', 'ADJ-4471102', '2025-11-03', 'mgonzalez@aol.com',      'P-2204', '06/12/2026'],
    ['AME Evaluation',     'David',    'Chen',      1, '2093', 'AM-24820', '2026-06-09T11:00', 1,  'San Diego — Hillcrest',  'ADJ-3389027', '2025-08-21', 'dchen.case@gmail.com',   'P-2207', '06/13/2026'],
    ['QME Re-Evaluation',  'Robert',   'Williams',  1, '5512', 'RE-24788', '2026-06-08T14:15', 2,  'Los Angeles — Wilshire', 'ADJ-4471980', '2024-12-15', 'rwilliams@yahoo.com',    'P-2198', '06/12/2026'],
    ['Panel QME',          'Aisha',    'Hassan',    2, '6634', 'PQ-24795', '2026-06-10T08:45', 1,  'Sacramento — Midtown',   'ADJ-5520114', '2025-09-30', 'aisha.h@outlook.com',    'P-2201', '06/12/2026'],
    ['QME Follow-up',      'James',    'O\u2019Brien',  1, '1180', 'FU-24762', '2026-06-08T10:30', 2,  'Oakland — Lakeside',     'ADJ-2210445', '2025-02-11', 'jobrien@gmail.com',      'P-2189', '06/11/2026'],
    ['Panel QME',          'Sofia',    'Rossi',     2, '8890', 'PQ-24830', '2026-06-11T13:00', 14, 'Fresno — Herndon',       'ADJ-6612900', '2025-10-19', 'sofia.rossi@gmail.com',  'P-2211', '06/11/2026'],
    ['AME Evaluation',     'Marcus',   'Johnson',   1, '4421', 'AM-24744', '2026-06-05T15:30', 3,  'Los Angeles — Wilshire', 'ADJ-4470088', '2025-01-08', 'mjohnson@icloud.com',    'P-2180', '06/05/2026'],
    ['QME Re-Evaluation',  'Linda',    'Tran',      2, '3357', 'RE-24710', '2026-06-04T09:00', 5, 'San Diego — Hillcrest',  'ADJ-3380771', '2024-07-22', 'ltran.wc@gmail.com',     'P-2171', '05/30/2026'],
    ['Panel QME',          'Kevin',    'Patel',     1, '9902', 'PQ-24802', '2026-06-10T11:45', 2,  'Sacramento — Midtown',   'ADJ-5521208', '2025-06-14', 'kpatel@gmail.com',       'P-2203', '06/17/2026'],
    ['QME Follow-up',      'Grace',    'Kim',       2, '7028', 'FU-24779', '2026-06-09T16:00', 12, 'Oakland — Lakeside',     'ADJ-2211390', '2025-03-27', 'grace.kim@yahoo.com',    'P-2195', '06/14/2026'],
    ['Supplemental Report','Anthony',  'Russo',     1, '6145', 'SR-24690', '2026-06-03T10:00', 2, 'Fresno — Herndon',       'ADJ-6610233', '2024-11-30', 'arusso@gmail.com',       'P-2166', '05/28/2026'],
    ['Panel QME',          'Nicole',   'Adams',     2, '2271', 'PQ-24811', '2026-06-11T08:30', 1,  'Los Angeles — Wilshire', 'ADJ-4472665', '2025-12-01', 'nadams@outlook.com',     'P-2209', '06/13/2026'],
    ['AME Evaluation',     'Daniel',   'Murphy',    1, '5583', 'AM-24756', '2026-06-06T14:00', 5,  'San Diego — Hillcrest',  'ADJ-3381902', '2025-05-09', 'dmurphy@gmail.com',      'P-2185', '06/06/2026'],
    ['QME Re-Evaluation',  'Emily',    'Nguyen',    2, '8814', 'RE-24823', '2026-06-12T13:30', 1,  'Sacramento — Midtown',   'ADJ-5522017', '2025-07-18', 'enguyen@gmail.com',      'P-2212', '06/14/2026'],
    ['QME Follow-up',      'Carlos',   'Mendoza',   1, '3390', 'FU-24733', '2026-06-05T09:15', 6,  'Oakland — Lakeside',     'ADJ-2212004', '2024-09-05', 'cmendoza@yahoo.com',     'P-2176', '06/05/2026'],
    ['Panel QME',          'Hannah',   'Brooks',    2, '1109', 'PQ-24808', '2026-06-10T15:00', 2,  'Fresno — Herndon',       'ADJ-6613411', '2025-08-02', 'hbrooks@gmail.com',      'P-2206', '06/17/2026'],
  ];

  const appointments = raw.map((r, i) => ({
    id: 'apt-' + (i + 1),
    type: r[0],
    patient: { firstName: r[1], lastName: r[2], genderId: r[3], gender: GENDER[r[3]], ssn: r[4], ssnMasked: mask(r[4]), email: r[11] },
    confirmation: r[5],
    appointmentDate: r[6],
    statusId: r[7],
    status: STATUS[r[7]],
    location: r[8],
    claimNumber: 'WC' + (i % 2 ? '25' : '24') + '-' + (10480 + i * 53),
    adjNumber: r[9],
    dateOfInjury: r[10],
    identityEmail: r[11],
    panelNumber: r[12],
    dueDate: r[13],
  }));

  // Dashboard counters (tenant dashboard)
  const counters = {
    pendingRequests: 18,
    approvedThisWeek: 42,
    rejectedThisWeek: 5,
    pendingChangeRequests: 7,
    requestsApproachingLegalDeadline: 3,
    billedThisMonth: 128,
    noShowThisMonth: 9,
    rescheduledThisMonth: 14,
    cancelledThisWeek: 6,
    checkedInToday: 11,
    checkedOutToday: 8,
    totalDoctors: 24,
    totalTenants: 6,
  };

  window.MOCK = { STATUS, TYPES, LOCATIONS, GENDER, appointments, counters };
})();
