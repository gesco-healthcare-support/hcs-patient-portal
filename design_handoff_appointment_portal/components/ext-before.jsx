/* global React */
const { useState: useStateB, useMemo: useMemoB } = React;

function BIcon({ name, size }) { return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size)} />; }
function bBadge(tone) {
  switch (tone) { case 'approved': return 'ok'; case 'pending': return 'warn'; case 'rejected': return 'no'; case 'info': case 'teal': return 'info'; default: return 'gray'; }
}
function bDateTime(iso) { const d = new Date(iso); return d.toLocaleDateString('en-US') + ' ' + d.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' }); }

/* Faithful recreation of the CURRENT external-role home (home.component.html).
   Role-aware exactly as the real code: same layout for all 4 roles, DOB filter
   only shows for non-patient roles, Book buttons render for every external role. */
function BeforeExternalHome({ roleKey }) {
  const role = window.EXT.ROLES[roleKey];
  const rows0 = window.EXT.rowsFor(roleKey);
  const [q, setQ] = useStateB('');
  const [adv, setAdv] = useStateB(false);
  const isPatient = roleKey === 'patient';

  const rows = useMemoB(() => {
    const t = q.trim().toLowerCase();
    if (!t) return rows0;
    return rows0.filter(a => (a.patient.firstName + ' ' + a.patient.lastName).toLowerCase().includes(t) || a.confirmation.toLowerCase().includes(t) || a.claimNumber.toLowerCase().includes(t));
  }, [q, roleKey]);

  return (
    <div className="bf">
      <div className="bf-topnav">
        <a href="#" onClick={e => e.preventDefault()}><img src="assets/header-logo.png" alt="logo" /></a>
        <div className="bf-topnav__meta">
          <span>Welcome, <strong>{role.name}</strong> ({role.label})</span>
          <span className="div">|</span>
          <a href="#" onClick={e => e.preventDefault()}>My profile</a>
          <span className="div">|</span>
          <a href="#" onClick={e => e.preventDefault()}>Help</a>
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
          <h3>{isPatient ? 'My Appointments Requests' : 'Appointment Requests'} ({rows0.length})</h3>
          <div style={{ display: 'flex', gap: '.5rem', alignItems: 'center' }}>
            <input className="form-control" style={{ width: 200 }} placeholder="Search" value={q} onChange={e => setQ(e.target.value)} />
            <button className="btn btn-link" title="Reset" onClick={() => setQ('')}><BIcon name="refresh" size={15} /></button>
          </div>
        </div>

        <div className="card">
          <div className="card-header" role="button" onClick={() => setAdv(!adv)} style={{ cursor: 'pointer', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: '1.05rem', fontWeight: 600 }}>Advanced Search</span>
            <BIcon name={adv ? 'chevDown' : 'chevRight'} size={16} />
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
                {!isPatient && <div><label className="form-label">Date Of Birth</label><input className="form-control" type="date" /></div>}
                <div><label className="form-label">Social Security #</label><input className="form-control" placeholder="Social Security #" /></div>
              </div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '.5rem' }}>
                <button className="btn btn-secondary">Reset <BIcon name="refresh" size={13} /></button>
                <button className="btn btn-primary">Search <BIcon name="search" size={13} /></button>
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
                    <td>{bDateTime(a.appointmentDate)}</td>
                    <td>{a.patient.ssnMasked}</td>
                    <td>{a.claimNumber}</td>
                    <td>{new Date(a.dateOfInjury).toLocaleDateString('en-US')}</td>
                    <td>{a.location}</td>
                    <td><span className={'bf-badge ' + bBadge(a.status.tone)}>{a.status.label}</span></td>
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

window.BeforeExternalHome = BeforeExternalHome;
