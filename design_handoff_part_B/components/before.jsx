/* global React */
const { useState, useMemo } = React;

/* ---------- shared bits ---------- */
function BfIcon({ name, size }) {
  return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size)} />;
}
function bfBadge(tone) {
  switch (tone) {
    case 'approved': return 'ok';
    case 'pending': return 'warn';
    case 'rejected': return 'no';
    case 'info': case 'teal': return 'info';
    default: return 'gray';
  }
}
function fmtDateTime(iso) {
  const d = new Date(iso);
  return d.toLocaleDateString('en-US') + ' ' + d.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' });
}

/* ============================================================
   BEFORE — Patient home + appointment requests
   ============================================================ */
function BeforePatientHome() {
  const { appointments } = window.MOCK;
  const [q, setQ] = useState('');
  const [adv, setAdv] = useState(false);
  const rows = useMemo(() => {
    const t = q.trim().toLowerCase();
    if (!t) return appointments;
    return appointments.filter(a =>
      (a.patient.firstName + ' ' + a.patient.lastName).toLowerCase().includes(t) ||
      a.confirmation.toLowerCase().includes(t) ||
      a.claimNumber.toLowerCase().includes(t));
  }, [q]);

  return (
    <div className="bf">
      <div className="bf-topnav">
        <a href="#" onClick={e => e.preventDefault()}><img src="assets/header-logo.png" alt="logo" /></a>
        <div className="bf-topnav__meta">
          <span>Welcome, <strong>Maria Gonzalez</strong> (Patient)</span>
          <span className="div">|</span>
          <a href="#" onClick={e => e.preventDefault()}>My profile</a>
        </div>
      </div>

      <div className="bf-wrap">
        <div className="bf-grid" style={{ gridTemplateColumns: '1fr 1fr', marginBottom: '1.5rem' }}>
          <div className="cta">
            <p style={{ marginTop: 0, marginBottom: '.5rem' }}>Please click on the below button to book a new appointment</p>
            <button className="btn btn-primary btn-block">Book Appointment</button>
          </div>
          <div className="cta">
            <p style={{ marginTop: 0, marginBottom: '.5rem' }}>Please click on the below button to book re-evaluation</p>
            <button className="btn btn-primary btn-block">Book Re-evaluation</button>
          </div>
        </div>

        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '.5rem', marginBottom: '1rem' }}>
          <h3>My Appointments Requests ({appointments.length})</h3>
          <div style={{ display: 'flex', gap: '.5rem', alignItems: 'center' }}>
            <input className="form-control" style={{ width: 200 }} placeholder="Search" value={q} onChange={e => setQ(e.target.value)} />
            <button className="btn btn-link" title="Reset" onClick={() => setQ('')}><BfIcon name="refresh" size={15} /></button>
          </div>
        </div>

        <div className="card">
          <div className="card-header" role="button" onClick={() => setAdv(!adv)} style={{ cursor: 'pointer', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: '1.05rem', fontWeight: 600 }}>Advanced Search</span>
            <BfIcon name={adv ? 'chevDown' : 'chevRight'} size={16} />
          </div>
          {adv && (
            <div className="card-body">
              <div className="bf-filterbar" style={{ marginBottom: '1rem' }}>
                <div><label className="form-label">Appointment Type</label><select className="form-select"><option>Select</option></select></div>
                <div><label className="form-label">Confirmation Number</label><input className="form-control" placeholder="A0000" /></div>
                <div><label className="form-label">Location</label><select className="form-select"><option>Select</option></select></div>
                <div><label className="form-label">Appointment Status</label><select className="form-select"><option>Select</option></select></div>
                <div><label className="form-label">Claim #</label><input className="form-control" placeholder="Claim #" /></div>
                <div><label className="form-label">Date Of Injury</label><input className="form-control" type="date" /></div>
                <div><label className="form-label">Social Security #</label><input className="form-control" placeholder="Social Security #" /></div>
              </div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '.5rem' }}>
                <button className="btn btn-secondary">Reset <BfIcon name="refresh" size={13} /></button>
                <button className="btn btn-primary">Search <BfIcon name="search" size={13} /></button>
              </div>
            </div>
          )}
        </div>

        <div className="card">
          <div className="card-body" style={{ overflowX: 'auto' }}>
            <table className="bf-table">
              <thead>
                <tr>
                  <th>Type</th><th>Patient Name</th><th>Gender</th><th>Confirmation #</th>
                  <th>Appointment Date</th><th>SSN</th><th>Claim #</th><th>Date Of Injury</th>
                  <th>Location</th><th>Status</th><th>Action</th>
                </tr>
              </thead>
              <tbody>
                {rows.map(a => (
                  <tr key={a.id}>
                    <td>{a.type}</td>
                    <td>{a.patient.firstName} {a.patient.lastName}</td>
                    <td>{a.patient.gender}</td>
                    <td><button className="btn btn-link">{a.confirmation}</button></td>
                    <td>{fmtDateTime(a.appointmentDate)}</td>
                    <td>{a.patient.ssnMasked}</td>
                    <td>{a.claimNumber}</td>
                    <td>{new Date(a.dateOfInjury).toLocaleDateString('en-US')}</td>
                    <td>{a.location}</td>
                    <td><span className={'bf-badge ' + bfBadge(a.status.tone)}>{a.status.label}</span></td>
                    <td><button className="btn btn-primary btn-sm">Document Manager</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
}

/* ============================================================
   BEFORE — staff shell (sidebar + topbar)
   ============================================================ */
function BeforeShell({ active, children }) {
  const nav = [
    ['Dashboard', 'grid', 'dashboard'],
    ['Appointments', 'calendar', 'appts'],
  ];
  const admin = [
    ['Appointment Types', 'list'], ['Locations', 'map'], ['Doctors', 'stetho'],
    ['Patients', 'user'], ['Settings', 'settings'],
  ];
  return (
    <div className="bf">
      <div className="bf-app">
        <aside className="bf-side">
          <div className="bf-side__brand">
            <span className="b-logo">A</span><b>Appointment Portal</b>
          </div>
          <div className="bf-side__sect">Main</div>
          {nav.map(([t, ic, k]) => (
            <a key={t} href="#" onClick={e => e.preventDefault()} className={active === k ? 'active' : ''}>
              <BfIcon name={ic} size={17} /> {t}
            </a>
          ))}
          <div className="bf-side__sect">Administration</div>
          {admin.map(([t, ic]) => (
            <a key={t} href="#" onClick={e => e.preventDefault()}><BfIcon name={ic} size={17} /> {t}</a>
          ))}
        </aside>
        <div className="bf-main">
          <div className="bf-topbar">
            <div className="crumbs">Home / {active === 'dashboard' ? 'Dashboard' : 'Appointments'}</div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
              <BfIcon name="bell" size={18} />
              <div className="ava">SC</div>
            </div>
          </div>
          {children}
        </div>
      </div>
    </div>
  );
}

/* ============================================================
   BEFORE — staff dashboard (13 counter cards, flat)
   ============================================================ */
function BeforeDashboard() {
  const c = window.MOCK.counters;
  const cards = [
    ['Pending Requests', c.pendingRequests, false, true],
    ['Approved This Week', c.approvedThisWeek, false, true],
    ['Rejected This Week', c.rejectedThisWeek, false, true],
    ['Pending Change Requests', c.pendingChangeRequests, '(populated when W3 ships)'],
    ['Approaching Legal Deadline', c.requestsApproachingLegalDeadline, 'CCR Sec. 31.5 / 60 days'],
    ['Billed This Month', c.billedThisMonth, '', false, true],
    ['No-Show This Month', c.noShowThisMonth, '', false, true],
    ['Rescheduled This Month', c.rescheduledThisMonth, '', false, true],
    ['Cancelled This Week', c.cancelledThisWeek, '', false, true],
    ['Checked In Today', c.checkedInToday, '', false, true],
    ['Checked Out Today', c.checkedOutToday, '', false, true],
    ['Total Doctors', c.totalDoctors, '(host view only)', false, true],
    ['Total Tenants', c.totalTenants, '(host view only)', false, true],
  ];
  return (
    <BeforeShell active="dashboard">
      <div style={{ padding: '1.25rem' }}>
        <h3 style={{ marginBottom: '1rem' }}>Dashboard</h3>
        <div className="bf-grid">
          {cards.map(([lbl, num, sub, click, ph], i) => (
            <div key={i} className={'bf-counter' + (ph ? ' ph' : '') + (click ? ' click' : '')}>
              <div className="lbl">{lbl}</div>
              <div className="num">{num}</div>
              {sub && <div className="sub">{sub}</div>}
            </div>
          ))}
        </div>
      </div>
    </BeforeShell>
  );
}

/* ============================================================
   BEFORE — staff appointment list + filters
   ============================================================ */
function BeforeApptList() {
  const { appointments } = window.MOCK;
  return (
    <BeforeShell active="appts">
      <div style={{ padding: '1.25rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '.75rem' }}>
          <h2 style={{ fontSize: '1.4rem' }}>Appointments</h2>
          <button className="btn btn-primary btn-sm"><BfIcon name="plus" size={13} /> New Appointment</button>
        </div>

        <div className="card">
          <div className="card-body">
            <div className="mb3">
              <label className="form-label">Search</label>
              <div className="input-group">
                <span className="input-group-text"><BfIcon name="search" size={14} /></span>
                <input className="form-control" placeholder="Search by patient, confirmation #, claim #..." />
              </div>
            </div>
            <div className="bf-filterbar">
              <div><label className="form-label">Panel Number</label><input className="form-control" /></div>
              <div><label className="form-label">Min Appointment Date</label><input className="form-control" type="date" /></div>
              <div><label className="form-label">Max Appointment Date</label><input className="form-control" type="date" /></div>
              <div><label className="form-label">Identity User</label><select className="form-select"><option>Select</option></select></div>
              <div><label className="form-label">Appointment Type</label><select className="form-select"><option>Select</option></select></div>
              <div><label className="form-label">Location</label><select className="form-select"><option>Select</option></select></div>
              <div style={{ display: 'flex', gap: '.5rem' }}>
                <button className="btn btn-outline-primary">Clear</button>
                <button className="btn btn-primary">Refresh</button>
              </div>
            </div>
          </div>
        </div>

        <div className="card">
          <div className="card-body" style={{ overflowX: 'auto' }}>
            <table className="bf-table">
              <thead>
                <tr>
                  <th>Actions</th><th>Panel #</th><th>Appointment Date</th><th>Confirmation #</th>
                  <th>Due Date</th><th>Status</th><th>Patient</th><th>Identity User</th>
                  <th>Type</th><th>Location</th>
                </tr>
              </thead>
              <tbody>
                {appointments.map(a => (
                  <tr key={a.id}>
                    <td><button className="btn btn-primary btn-sm dropdown-toggle"><BfIcon name="settings" size={12} /> Actions</button></td>
                    <td>{a.panelNumber}</td>
                    <td>{new Date(a.appointmentDate).toLocaleDateString('en-US')}</td>
                    <td>{a.confirmation}</td>
                    <td>{a.dueDate}</td>
                    <td><span className={'bf-badge ' + bfBadge(a.status.tone)}>{a.status.label}</span></td>
                    <td>{a.patient.firstName} {a.patient.lastName}</td>
                    <td>{a.identityEmail}</td>
                    <td>{a.type}</td>
                    <td>{a.location}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="bf-pager">
              <span>Showing 1 to {appointments.length} of {appointments.length} entries</span>
              <div className="pages"><span>‹</span><span className="on">1</span><span>2</span><span>›</span></div>
            </div>
          </div>
        </div>
      </div>
    </BeforeShell>
  );
}

window.Before = { BeforePatientHome, BeforeDashboard, BeforeApptList };
