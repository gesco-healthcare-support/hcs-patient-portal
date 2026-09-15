/* global React */
/* ============================================================
   Group 4 — People hub. Patients (curated table + column chooser +
   full detail view + edit modal) · Applicant Attorneys · Defense
   Attorneys · Claim Examiners (table + modal). Portal-account status
   chips + Invite-to-portal. Renders inside InternalShell.
   ============================================================ */
const { useState: useStateP, useMemo: useMemoP } = React;
function PI({ name, size }) { return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size || 18)} />; }

/* ---------------- seed data ---------------- */
const P_PORTAL = ['linked', 'invited', 'none'];
const P_PATIENTS0 = window.MOCK.appointments.slice(0, 9).map((a, i) => ({
  id: 'p' + i,
  first: a.patient.firstName, last: a.patient.lastName, middle: '',
  email: a.patient.email, gender: i % 3 === 0 ? 'Male' : 'Female',
  dob: ['03/22/1984', '11/02/1979', '06/14/1990', '01/30/1968', '09/08/1985'][i % 5],
  phone: '(213) 555-01' + (20 + i), cell: '(213) 555-02' + (20 + i), phoneType: 'Cell',
  street: (120 + i * 7) + ' W 4th St', unit: i % 2 ? 'Apt ' + (i + 1) : '', city: ['Los Angeles', 'Sacramento', 'San Diego', 'Fresno'][i % 4], state: 'CA', zip: '900' + (10 + i),
  language: ['English', 'Spanish', 'Mandarin'][i % 3], otherLanguage: '', interpreter: i % 3 === 1, vendor: i % 3 === 1 ? 'LA Interpreting Services' : '',
  ssnMasked: '•••-••-' + (4100 + i * 37), apptNumber: 'A-' + (8200 + i * 11),
  portal: P_PORTAL[i % 3], appts: [5, 2, 1, 3, 1, 4, 2, 1, 2][i], booker: a.identityEmail,
}));
function mkParty(i, kind) {
  const firms = ['Brooks & Associates', 'Pelton Defense Group', 'Hale & Mercer LLP', 'Castillo Law', 'Sierra Claims Admin', 'Pacific Adjusters'];
  const names = [['Daniel', 'Brooks'], ['Laura', 'Pelton'], ['Miriam', 'Hale'], ['Victor', 'Castillo'], ['Karen', 'Whitfield'], ['Omar', 'Reyes']];
  const [first, last] = names[i % 6];
  return {
    id: kind + i, first, last, firm: firms[i % 6],
    firmAddress: (200 + i * 13) + ' S Grand Ave, Los Angeles, CA',
    email: (first[0] + last).toLowerCase() + '@' + firms[i % 6].split(' ')[0].toLowerCase() + '.com',
    phone: '(213) 555-03' + (10 + i), fax: i % 2 ? '(213) 555-04' + (10 + i) : '',
    portal: P_PORTAL[(i + 1) % 3], appts: [12, 8, 5, 3, 9, 4][i % 6],
  };
}
const P_LISTS0 = {
  aa: Array.from({ length: 4 }, (_, i) => mkParty(i, 'aa')),
  da: Array.from({ length: 3 }, (_, i) => mkParty(i + 1, 'da')),
  ce: Array.from({ length: 3 }, (_, i) => mkParty(i + 4, 'ce')),
};

const P_SECTIONS = [
  ['patients', 'Patients', 'users'], ['aa', 'Applicant Attorneys', 'user'],
  ['da', 'Defense Attorneys', 'user'], ['ce', 'Claim Examiners', 'user'],
];
const P_OPTCOLS = [['gender', 'Gender'], ['street', 'Street'], ['zip', 'Zip'], ['interpreter', 'Interpreter'], ['apptNumber', 'Appt #'], ['booker', 'Portal booker']];
const PORTAL_LBL = { linked: 'Linked', invited: 'Invited', none: 'No account' };

function PortalChip({ v }) { return <span className={'pp-portal ' + v}><span className="d" />{PORTAL_LBL[v]}</span>; }
function KV({ k, v, full, mono }) { return (<div className={'ad-field' + (full ? ' full' : '')}><span className="k">{k}</span><span className={'v' + (mono ? ' mono' : '') + (v ? '' : ' empty')}>{v || '—'}</span></div>); }

function InPeople({ onToast }) {
  const [section, setSection] = useStateP('patients');
  const [patients, setPatients] = useStateP(P_PATIENTS0);
  const [lists, setLists] = useStateP(P_LISTS0);
  const [q, setQ] = useStateP('');
  const [showF, setShowF] = useStateP(false);
  const [colsOpen, setColsOpen] = useStateP(false);
  const [cols, setCols] = useStateP({});
  const [detail, setDetail] = useStateP(null);   // patient id
  const [edit, setEdit] = useStateP(null);       // 'new' | id (current section)
  const [d, setD] = useStateP({});

  const isPat = section === 'patients';
  const label = P_SECTIONS.find(s => s[0] === section)[1];
  const singular = { patients: 'patient', aa: 'applicant attorney', da: 'defense attorney', ce: 'claim examiner' }[section];

  const rows = useMemoP(() => {
    const src = isPat ? patients : lists[section];
    const t = q.trim().toLowerCase();
    if (!t) return src;
    return src.filter(r => (r.first + ' ' + r.last).toLowerCase().includes(t) || (r.email || '').toLowerCase().includes(t) || (r.phone || '').includes(t) || (r.firm || '').toLowerCase().includes(t));
  }, [section, q, patients, lists]);

  function openEdit(row) {
    setD(row ? { ...row } : (isPat ? { first: '', last: '', email: '', gender: 'Female', dob: '', phone: '', cell: '', phoneType: 'Cell', street: '', unit: '', city: '', state: 'CA', zip: '', language: 'English', interpreter: false, vendor: '', apptNumber: '', portal: 'none', appts: 0 } : { first: '', last: '', firm: '', firmAddress: '', email: '', phone: '', fax: '', portal: 'none', appts: 0 }));
    setEdit(row ? row.id : 'new');
  }
  function save() {
    if (!d.first.trim() || !d.last.trim()) { onToast('First and last name are required.'); return; }
    if (isPat) setPatients(ps => edit === 'new' ? [...ps, { ...d, id: 'p' + Date.now(), ssnMasked: '•••-••-0000' }] : ps.map(p => p.id === edit ? { ...p, ...d } : p));
    else setLists(ls => ({ ...ls, [section]: edit === 'new' ? [...ls[section], { ...d, id: section + Date.now() }] : ls[section].map(r => r.id === edit ? { ...r, ...d } : r) }));
    setEdit(null); onToast(singular[0].toUpperCase() + singular.slice(1) + ' saved.');
  }
  const pd = detail ? patients.find(p => p.id === detail) : null;
  const pdAppts = pd ? window.MOCK.appointments.slice(0, pd.appts) : [];
  const fmtD = iso => new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });

  /* ---------- patient detail view ---------- */
  if (pd) {
    return (
      <>
        <button className="pd-back" onClick={() => setDetail(null)}><PI name="chevLeft" size={15} />Back to patients</button>
        <div className="pd-head">
          <span className="ava" style={{ background: window.AfCommon.avaColor(pd.first + pd.last) }}>{window.AfCommon.initials(pd.first, pd.last)}</span>
          <div className="meta">
            <h2>{pd.first} {pd.last}</h2>
            <div className="sub"><span>{pd.email}</span><span>{pd.city}, {pd.state}</span><span>{pd.appts} appointment{pd.appts === 1 ? '' : 's'}</span></div>
          </div>
          <div className="acts">
            <PortalChip v={pd.portal} />
            {pd.portal === 'none' && <button className="af-btn af-btn--glass" onClick={() => onToast('Opening Invite External User…')}><PI name="user" size={15} />Invite to portal</button>}
            <button className="af-btn af-btn--green" onClick={() => openEdit(pd)}><PI name="doc" size={15} />Edit patient</button>
          </div>
        </div>

        <div className="pd-grid">
          <section className="ad-card"><div className="ad-card__head"><span className="ic tint-blue"><PI name="user" size={18} /></span><h3>Personal</h3></div>
            <div className="ad-card__body"><div className="ad-dl" style={{ gridTemplateColumns: '1fr' }}>
              <KV k="Name" v={[pd.first, pd.middle, pd.last].filter(Boolean).join(' ')} /><KV k="Gender" v={pd.gender} />
              <KV k="Date of birth" v={pd.dob} mono /><KV k="SSN" v={pd.ssnMasked} mono /><KV k="Appt #" v={pd.apptNumber} mono />
            </div></div>
          </section>
          <section className="ad-card"><div className="ad-card__head"><span className="ic tint-teal"><PI name="user" size={18} /></span><h3>Contact</h3></div>
            <div className="ad-card__body"><div className="ad-dl" style={{ gridTemplateColumns: '1fr' }}>
              <KV k="Email" v={pd.email} /><KV k="Phone" v={pd.phone} mono /><KV k="Cell" v={pd.cell} mono /><KV k="Phone type" v={pd.phoneType} />
              <KV k="Portal account" v={PORTAL_LBL[pd.portal] + (pd.portal === 'linked' ? ' — ' + pd.booker : '')} />
            </div></div>
          </section>
          <section className="ad-card"><div className="ad-card__head"><span className="ic tint-slate"><PI name="map" size={18} /></span><h3>Address</h3></div>
            <div className="ad-card__body"><div className="ad-dl" style={{ gridTemplateColumns: '1fr' }}>
              <KV k="Street" v={[pd.street, pd.unit].filter(Boolean).join(', ')} /><KV k="City" v={pd.city} /><KV k="State" v={pd.state} /><KV k="Zip" v={pd.zip} mono />
            </div></div>
          </section>
          <section className="ad-card"><div className="ad-card__head"><span className="ic tint-amber"><PI name="settings" size={18} /></span><h3>Preferences</h3></div>
            <div className="ad-card__body"><div className="ad-dl" style={{ gridTemplateColumns: '1fr' }}>
              <KV k="Appointment language" v={pd.language} /><KV k="Other language" v={pd.otherLanguage} />
              <KV k="Interpreter" v={pd.interpreter ? 'Yes — ' + pd.vendor : 'No'} />
            </div></div>
          </section>

          <section className="ad-card pd-full">
            <div className="ad-card__head"><span className="ic tint-purple"><PI name="calendar" size={18} /></span><h3>Appointments ({pdAppts.length})</h3>
              <div className="right"><button className="af-btn af-btn--ghost af-btn--sm" onClick={() => onToast('Opening filtered appointment list…')}><PI name="list" size={14} />View in appointments</button></div>
            </div>
            <div className="ad-card__body" style={{ padding: 0 }}>
              <table className="ad-table">
                <thead><tr><th>Confirmation #</th><th>Type</th><th>Date</th><th>Location</th><th>Status</th></tr></thead>
                <tbody>
                  {pdAppts.map(a => (
                    <tr key={a.id} style={{ cursor: 'pointer' }} onClick={() => onToast('Opening ' + a.confirmation + '…')}>
                      <td><span className="ia-conf">{a.confirmation}</span></td><td>{a.type}</td>
                      <td className="num">{fmtD(a.appointmentDate)}</td><td>{a.location}</td>
                      <td><window.AfCommon.AfPill tone={a.status.tone} label={a.status.label} /></td>
                    </tr>
                  ))}
                  {pdAppts.length === 0 && <tr><td colSpan="5" style={{ color: 'var(--n-400)', textAlign: 'center', padding: 22 }}>No appointments yet.</td></tr>}
                </tbody>
              </table>
            </div>
          </section>
        </div>
        {edit != null && <PeopleModal isPat={isPat} d={d} setD={setD} singular={singular} isNew={edit === 'new'} onClose={() => setEdit(null)} onSave={save} />}
      </>
    );
  }

  /* ---------- directory view ---------- */
  return (
    <>
      <div className="ia-head">
        <div><h1>People</h1><p>Patients and case parties for this practice.</p></div>
        <a className="af-btn af-btn--primary" onClick={() => openEdit(null)}><PI name="plus" size={16} />New {singular}</a>
      </div>

      <div className="cf">
        <nav className="cf-rail">
          {P_SECTIONS.map(([k, lbl, ic]) => (
            <button key={k} className="cf-railitem" data-on={section === k} onClick={() => { setSection(k); setQ(''); setEdit(null); }}>
              <span className="i"><PI name={ic} size={16} /></span>{lbl}<span className="cnt">{k === 'patients' ? patients.length : lists[k].length}</span>
            </button>
          ))}
        </nav>

        <div>
          <div className="ia-toolbar">
            <div className="ia-search"><PI name="search" size={16} /><input placeholder={'Search ' + label.toLowerCase() + ' by name, email, phone' + (isPat ? '' : ', firm') + '…'} value={q} onChange={e => setQ(e.target.value)} /></div>
            {isPat && (<>
              <button className="ia-fbtn" aria-expanded={showF} onClick={() => setShowF(!showF)}><PI name="filter" size={15} />Filters</button>
              <div className="rp-cols">
                <button className="ia-fbtn" aria-expanded={colsOpen} onClick={() => setColsOpen(!colsOpen)}><PI name="list" size={15} />Columns</button>
                {colsOpen && (<>
                  <div className="ia-clickaway" onClick={() => setColsOpen(false)} />
                  <div className="rp-colmenu">
                    {P_OPTCOLS.map(([k, lbl]) => <label key={k}><input type="checkbox" checked={!!cols[k]} onChange={e => setCols({ ...cols, [k]: e.target.checked })} />{lbl}</label>)}
                  </div>
                </>)}
              </div>
            </>)}
          </div>

          {isPat && showF && (
            <div className="ia-filters">
              <div className="ia-filters__grid">
                <div className="ia-field"><label>Gender</label><select className="ia-input"><option value="">All</option><option>Male</option><option>Female</option><option>Other</option></select></div>
                <div className="ia-field"><label>Min date of birth</label><input className="ia-input" type="date" /></div>
                <div className="ia-field"><label>Max date of birth</label><input className="ia-input" type="date" /></div>
                <div className="ia-field"><label>City</label><input className="ia-input" /></div>
                <div className="ia-field"><label>State</label><select className="ia-input"><option value="">All</option>{window.RA.STATES.map(s => <option key={s}>{s}</option>)}</select></div>
                <div className="ia-field"><label>Appointment language</label><select className="ia-input"><option value="">All</option>{window.RA.LANGUAGES.map(l => <option key={l}>{l}</option>)}</select></div>
                <div className="ia-field"><label>Portal account</label><select className="ia-input"><option value="">All</option><option>Linked</option><option>Invited</option><option>No account</option></select></div>
              </div>
              <div className="ia-filters__foot"><button className="af-btn af-btn--ghost" onClick={() => setShowF(false)}>Clear</button><button className="af-btn af-btn--primary" onClick={() => { setShowF(false); onToast('Filters applied.'); }}><PI name="search" size={15} />Apply</button></div>
            </div>
          )}

          <div className="ia-wrap">
            <div className="ia-scroll">
              <table className="ia-table" style={{ minWidth: isPat ? 860 : 720 }}>
                <thead><tr>
                  <th>{isPat ? 'Patient' : 'Name'}</th>
                  {!isPat && <th>Firm</th>}
                  <th>Email</th>
                  {isPat && <th>Date of birth</th>}
                  <th>Phone</th>
                  {isPat && <th>City / State</th>}
                  {isPat && <th>Language</th>}
                  {isPat && cols.gender && <th>Gender</th>}
                  {isPat && cols.street && <th>Street</th>}
                  {isPat && cols.zip && <th>Zip</th>}
                  {isPat && cols.interpreter && <th>Interpreter</th>}
                  {isPat && cols.apptNumber && <th>Appt #</th>}
                  {isPat && cols.booker && <th>Portal booker</th>}
                  <th>Portal</th>{!isPat && <th>Appointments</th>}<th style={{ textAlign: 'right' }}>Actions</th>
                </tr></thead>
                <tbody>
                  {rows.map(r => (
                    <tr key={r.id} onClick={() => isPat ? setDetail(r.id) : openEdit(r)}>
                      <td><span className="ia-pt"><span className="ava" style={{ background: window.AfCommon.avaColor(r.first + r.last) }}>{window.AfCommon.initials(r.first, r.last)}</span><b>{r.first} {r.last}</b></span></td>
                      {!isPat && <td>{r.firm}</td>}
                      <td className="ia-sub">{r.email}</td>
                      {isPat && <td className="ia-sub" style={{ fontFamily: 'var(--font-num)' }}>{r.dob}</td>}
                      <td className="ia-sub" style={{ fontFamily: 'var(--font-num)' }}>{r.phone}</td>
                      {isPat && <td>{r.city}, {r.state}</td>}
                      {isPat && <td>{r.language}</td>}
                      {isPat && cols.gender && <td>{r.gender}</td>}
                      {isPat && cols.street && <td className="ia-sub">{r.street}</td>}
                      {isPat && cols.zip && <td className="ia-sub" style={{ fontFamily: 'var(--font-num)' }}>{r.zip}</td>}
                      {isPat && cols.interpreter && <td>{r.interpreter ? 'Yes' : 'No'}</td>}
                      {isPat && cols.apptNumber && <td className="ia-sub" style={{ fontFamily: 'var(--font-num)' }}>{r.apptNumber}</td>}
                      {isPat && cols.booker && <td className="ia-sub">{r.booker}</td>}
                      <td><PortalChip v={r.portal} /></td>
                      {!isPat && <td className="num" style={{ fontFamily: 'var(--font-num)' }}>{r.appts}</td>}
                      <td style={{ textAlign: 'right' }} onClick={e => e.stopPropagation()}>
                        <span style={{ display: 'inline-flex', gap: 6 }}>
                          {r.portal === 'none' && <button className="ra-rowbtn" title="Invite to portal" onClick={() => onToast('Opening Invite External User for ' + r.first + '…')}><PI name="user" size={14} /></button>}
                          {isPat && <button className="ra-rowbtn" title="View" onClick={() => setDetail(r.id)}><PI name="eye" size={14} /></button>}
                          <button className="ra-rowbtn" title="Edit" onClick={() => openEdit(r)}><PI name="doc" size={14} /></button>
                          <button className="ra-rowbtn danger" title={r.appts > 0 ? 'Has appointments — locked' : 'Delete'}
                            style={r.appts > 0 ? { opacity: .35, cursor: 'not-allowed' } : null}
                            onClick={() => {
                              if (r.appts > 0) { onToast('Linked to ' + r.appts + ' appointments — can\u2019t delete.'); return; }
                              if (isPat) setPatients(ps => ps.filter(p => p.id !== r.id)); else setLists(ls => ({ ...ls, [section]: ls[section].filter(x => x.id !== r.id) }));
                              onToast('Deleted.');
                            }}><PI name="x" size={14} /></button>
                        </span>
                      </td>
                    </tr>
                  ))}
                  {rows.length === 0 && <tr><td colSpan="12"><div className="ia-empty"><PI name="inbox" size={36} /><b>No {label.toLowerCase()} match</b></div></td></tr>}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>

      {edit != null && <PeopleModal isPat={isPat} d={d} setD={setD} singular={singular} isNew={edit === 'new'} onClose={() => setEdit(null)} onSave={save} />}
    </>
  );
}

/* shared edit modal */
function PeopleModal({ isPat, d, setD, singular, isNew, onClose, onSave }) {
  const F = (k, label, col, props) => (
    <div className={'ra-field col-' + (col || 4)}>
      <label>{label}</label>
      <input className="ra-input" value={d[k] || ''} onChange={e => setD({ ...d, [k]: e.target.value })} {...(props || {})} />
    </div>
  );
  return (
    <div className="ra-scrim" onClick={onClose}>
      <div className="ra-modal ra-modal--lg" onClick={e => e.stopPropagation()}>
        <div className="ra-modal__head"><h3>{(isNew ? 'New ' : 'Edit ') + singular}</h3><button className="ext-iconbtn x" onClick={onClose}><span className="i" dangerouslySetInnerHTML={window.Ico('x', 17)} /></button></div>
        <div className="ra-modal__body">
          <div className="ra-grid">
            {F('first', 'First name *')}{F('last', 'Last name *')}
            {isPat ? (<>
              {F('middle', 'Middle name')}
              <div className="ra-field col-4"><label>Gender</label><select className="ra-select" value={d.gender || ''} onChange={e => setD({ ...d, gender: e.target.value })}><option>Male</option><option>Female</option><option>Other</option></select></div>
              {F('dob', 'Date of birth', 4, { placeholder: 'MM/DD/YYYY' })}
              {F('email', 'Email', 4, { type: 'email' })}
              {F('phone', 'Phone')}{F('cell', 'Cell phone')}
              <div className="ra-field col-4"><label>Phone type</label><select className="ra-select" value={d.phoneType || ''} onChange={e => setD({ ...d, phoneType: e.target.value })}><option>Cell</option><option>Home</option><option>Work</option></select></div>
              <div className="ra-field col-12"><label>Social Security #</label><input className="ra-input" placeholder="•••-••-•••• — enter to replace (stored securely, shown masked)" onCopy={e => e.preventDefault()} onChange={() => {}} /></div>
              {F('street', 'Street')}{F('unit', 'Unit #')}{F('city', 'City')}
              <div className="ra-field col-4"><label>State</label><select className="ra-select" value={d.state || ''} onChange={e => setD({ ...d, state: e.target.value })}>{window.RA.STATES.map(s => <option key={s}>{s}</option>)}</select></div>
              {F('zip', 'Zip code')}
              <div className="ra-field col-4"><label>Appointment language</label><select className="ra-select" value={d.language || ''} onChange={e => setD({ ...d, language: e.target.value })}>{window.RA.LANGUAGES.map(l => <option key={l}>{l}</option>)}</select></div>
              {F('otherLanguage', 'Other language')}{F('vendor', 'Interpreter vendor')}{F('apptNumber', 'Appt #')}
            </>) : (<>
              {F('firm', 'Firm name')}
              {F('firmAddress', 'Firm address', 8)}
              {F('email', 'Email', 4, { type: 'email' })}
              {F('phone', 'Phone')}{F('fax', 'Fax')}
            </>)}
          </div>
        </div>
        <div className="ra-modal__foot"><button className="af-btn af-btn--ghost" onClick={onClose}>Cancel</button><button className="af-btn af-btn--primary" onClick={onSave}><span className="i" dangerouslySetInnerHTML={window.Ico('check', 15)} />Save</button></div>
      </div>
    </div>
  );
}

window.InPeople = InPeople;
