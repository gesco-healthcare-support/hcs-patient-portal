/* global React */
/* ============================================================
   Internal Appointment Detail (staff) — COMPLETE field parity with the
   booking form. Every section the form collects is shown and editable:
   Appointment details · Patient demographics (all fields) · Employer ·
   Applicant attorney · Defense attorney · Insurance · Claim examiner ·
   Claim information (add/edit/delete) · Documents · Authorized users ·
   plus office actions (Approve/Reject/Cancel/Reschedule) and the
   staff-only internal panel. Renders inside the shell.
   ============================================================ */
const { useState: useStateID } = React;
function IDI({ name, size }) { return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size || 18)} />; }

/* Staff can reschedule/cancel any non-terminal appointment — including
   Pending (before approval). Approve/Reject only apply while Pending. */
const ID_ACT = { pending: ['approve', 'reject', 'reschedule', 'cancel'], approved: ['reschedule', 'cancel'], rescheduled: ['reschedule', 'cancel'], rejected: [], cancelled: [] };

function KV({ k, v, full, mono }) {
  return (<div className={'ad-field' + (full ? ' full' : '')}><span className="k">{k}</span><span className={'v' + (mono ? ' mono' : '') + (v ? '' : ' empty')}>{v || '—'}</span></div>);
}

/* Generic editable section: ledger view <-> input grid */
function EditSection({ icon, tint, title, sub, fields, vals, onSave, onToast }) {
  const [editing, setEditing] = useStateID(false);
  const [draft, setDraft] = useStateID({});
  function start() { const d = {}; fields.forEach(f => d[f.k] = vals[f.k] || ''); setDraft(d); setEditing(true); }
  return (
    <section className="ad-card">
      <div className="ad-card__head"><span className={'ic ' + tint}><IDI name={icon} size={18} /></span><div><h3>{title}</h3>{sub && <p style={{ fontSize: 12, color: 'var(--n-500)', margin: '2px 0 0' }}>{sub}</p>}</div>
        {!editing && <div className="right"><button className="mp-editbtn" onClick={start}><IDI name="doc" size={14} />Edit</button></div>}
      </div>
      <div className="ad-card__body">
        {editing ? (<>
          <div className="ra-grid">
            {fields.map(f => (
              <div className={'ra-field col-' + (f.col || 3)} key={f.k}>
                <label>{f.label}</label>
                {f.options ? (
                  <select className="ra-select" value={draft[f.k]} onChange={e => setDraft({ ...draft, [f.k]: e.target.value })}>
                    {f.options.map(o => <option key={o}>{o}</option>)}
                  </select>
                ) : (
                  <input className="ra-input" value={draft[f.k]} onChange={e => setDraft({ ...draft, [f.k]: e.target.value })} />
                )}
              </div>
            ))}
          </div>
          <div className="mp-editfoot"><button className="af-btn af-btn--ghost" onClick={() => setEditing(false)}>Cancel</button><button className="af-btn af-btn--primary" onClick={() => { onSave(draft); setEditing(false); onToast('Changes saved.'); }}><IDI name="check" size={15} />Save</button></div>
        </>) : (
          <div className="ad-dl">{fields.map(f => <KV key={f.k} k={f.label} v={vals[f.k]} full={f.full} mono={f.mono} />)}</div>
        )}
      </div>
    </section>
  );
}

/* attorney/insurance/examiner field factories (full booking-form parity) */
function attyFields(p) {
  return [
    { k: p + 'First', label: 'First name' }, { k: p + 'Last', label: 'Last name' }, { k: p + 'Email', label: 'Email', col: 3 }, { k: p + 'Firm', label: 'Firm name' },
    { k: p + 'Web', label: 'Web address' }, { k: p + 'Phone', label: 'Phone', mono: true }, { k: p + 'Fax', label: 'Fax', mono: true }, { k: p + 'Street', label: 'Street' },
    { k: p + 'City', label: 'City' }, { k: p + 'State', label: 'State' }, { k: p + 'Zip', label: 'Zip', mono: true },
  ];
}

function InDetail({ roleKey, status, onToast }) {
  const D = window.AD.DETAIL;
  const st = window.AD.STATUSES[status] || window.AD.STATUSES.pending;
  const [vals, setVals] = useStateID(() => ({
    // appointment details
    type: D.type, panelNumber: D.panelNumber, location: D.location,
    apptDate: window.AfCommon.fmtDateShort(D.date), apptTime: window.AfCommon.fmtTime(D.date),
    // patient demographics — every booking-form field
    pLast: D.patient.last, pFirst: D.patient.first, pMiddle: D.patient.middle, pGender: D.patient.gender,
    pDob: D.patient.dob, pEmail: D.patient.email, pCell: D.patient.cell, pPhone: D.patient.phone,
    pSsn: D.patient.ssnMasked, pStreet: D.patient.street, pUnit: D.patient.unit, pCity: D.patient.city,
    pState: D.patient.state, pZip: D.patient.zip, pLanguage: D.patient.language,
    pInterpreter: D.patient.interpreter ? 'Yes' : 'No', pVendor: D.patient.interpreterVendor, pReferred: D.patient.referredBy,
    // employer
    eName: D.employer.name, eOcc: D.employer.occupation, ePhone: D.employer.phone,
    eStreet: D.employer.street, eCity: D.employer.city, eState: D.employer.state, eZip: D.employer.zip,
    // applicant attorney
    aFirst: D.applicant.name.split(' ')[0], aLast: D.applicant.name.split(' ').slice(1).join(' '), aEmail: D.applicant.email,
    aFirm: D.applicant.firm, aWeb: D.applicant.web, aPhone: D.applicant.phone, aFax: D.applicant.fax,
    aStreet: D.applicant.street, aCity: D.applicant.city, aState: D.applicant.state, aZip: D.applicant.zip,
    // defense attorney
    dFirst: D.defense.name.split(' ')[0], dLast: D.defense.name.split(' ').slice(1).join(' '), dEmail: D.defense.email,
    dFirm: D.defense.firm, dWeb: D.defense.web, dPhone: D.defense.phone, dFax: D.defense.fax,
    dStreet: D.defense.street, dCity: D.defense.city, dState: D.defense.state, dZip: D.defense.zip,
    // insurance
    iName: D.insurance.name, iSuite: D.insurance.suite, iPhone: D.insurance.phone, iFax: D.insurance.fax,
    iStreet: D.insurance.street, iCity: D.insurance.city, iState: D.insurance.state, iZip: D.insurance.zip,
    // claim examiner
    xName: D.examiner.name, xEmail: D.examiner.email, xSuite: D.examiner.suite, xPhone: D.examiner.phone, xFax: D.examiner.fax,
    xStreet: D.examiner.street, xCity: D.examiner.city, xState: D.examiner.state, xZip: D.examiner.zip,
  }));
  const [injuries, setInjuries] = useStateID(() => D.injuries.map(i => ({ ...i, bodyParts: i.bodyParts.slice() })));
  const [docs, setDocs] = useStateID(() => D.documents.slice());
  const [authUsers, setAuthUsers] = useStateID(() => D.authUsers.slice());
  const [modal, setModal] = useStateID(null);
  const [reason, setReason] = useStateID('');
  const [slot, setSlot] = useStateID('');
  // claim-info modal state
  const [cIdx, setCIdx] = useStateID(-1);
  const [cDraft, setCDraft] = useStateID(null);
  // auth-user modal state
  const [au, setAu] = useStateID({ first: '', last: '', email: '', role: '', access: 'view' });

  function save(p) { setVals(v => ({ ...v, ...p })); }
  function closeM() { setModal(null); setReason(''); setSlot(''); }
  const acts = ID_ACT[status] || [];

  function openClaim(i) {
    setCIdx(i);
    setCDraft(i >= 0 ? JSON.parse(JSON.stringify(injuries[i])) : { cumulative: false, doi: '', toDoi: '', claim: '', wcab: '', adj: '', bodyParts: [''] });
    setModal('claim');
  }
  function saveClaim() {
    const clean = { ...cDraft, bodyParts: cDraft.bodyParts.filter(b => b.trim()) };
    if (!clean.doi || !clean.claim.trim() || !clean.adj.trim() || clean.bodyParts.length === 0) { onToast('DOI, claim #, ADJ # and a body part are required.'); return; }
    setInjuries(prev => { const n = prev.slice(); if (cIdx >= 0) n[cIdx] = clean; else n.push(clean); return n; });
    closeM(); onToast(cIdx >= 0 ? 'Claim updated.' : 'Claim added.');
  }
  function addAuthUser() {
    if (!au.email.trim() || !au.role) return;
    setAuthUsers([...authUsers, au]); setAu({ first: '', last: '', email: '', role: '', access: 'view' }); closeM(); onToast('Authorized user added.');
  }
  const badge = s => s === 'accepted' ? 'Accepted' : s === 'pending' ? 'Pending review' : 'Rejected';

  return (
    <div className="ad" style={{ margin: '-26px -28px -60px', minHeight: 'auto' }}>
      {/* status banner */}
      <div className={'ad-banner ' + st.banner}>
        <div className="ad-banner__in">
          <div className="ad-banner__top">
            <div style={{ minWidth: 0 }}>
              <div className="ad-banner__crumb"><a href="#" onClick={e => e.preventDefault()}>Appointments</a><IDI name="chevRight" size={12} />{D.confirmation}</div>
              <h1><span className="ttl">{vals.type} <span style={{ fontWeight: 500, opacity: .85 }}>· {vals.pFirst} {vals.pLast}</span></span><span className="ad-statepill"><span className="d" />{st.label}</span></h1>
              <div className="ad-banner__meta">
                <div className="it"><div className="k">Confirmation</div><div className="v mono">{D.confirmation}</div></div>
                <div className="it"><div className="k">Panel #</div><div className="v mono">{vals.panelNumber}</div></div>
                <div className="it"><div className="k">Location</div><div className="v">{vals.location}</div></div>
                <div className="it"><div className="k">Date &amp; time</div><div className="v">{vals.apptDate} · {vals.apptTime}</div></div>
                <div className="it"><div className="k">Requested on</div><div className="v">{window.AfCommon.fmtDateShort(D.requestedOn)}</div></div>
              </div>
            </div>
          </div>
          <div className="ad-actions" style={{ marginTop: 16 }}>
            {acts.includes('approve') && <button className="af-btn af-btn--green" onClick={() => setModal('approve')}><IDI name="check" size={15} />Approve</button>}
            {acts.includes('reject') && <button className="af-btn af-btn--glass" onClick={() => setModal('reject')}><IDI name="x" size={15} />Reject</button>}
            {acts.includes('reschedule') && <button className="af-btn af-btn--glass" onClick={() => setModal('reschedule')}><IDI name="refresh" size={15} />Reschedule</button>}
            {acts.includes('cancel') && <button className="af-btn af-btn--glass" onClick={() => setModal('cancel')}><IDI name="x" size={15} />Cancel</button>}
            <button className="af-btn af-btn--glass" onClick={() => onToast('Opening change log…')}><IDI name="clock" size={15} />Change log</button>
            <button className="af-btn af-btn--glass" onClick={() => onToast('Downloading demographics…')}><IDI name="arrowDown" size={15} />Demographics</button>
          </div>
        </div>
      </div>

      <div className="ad-wrap">
        {st.note && (<div className={'ad-note ad-note--' + st.note.kind}><span className="ic"><IDI name={st.note.kind === 'rejected' ? 'alert' : 'check'} size={18} /></span><div><b>{st.note.title}</b><p>{st.note.body}</p></div></div>)}

        {/* 1 · Appointment details — full schedule fields */}
        <EditSection icon="calendar" tint="tint-blue" title="Appointment details" vals={vals} onSave={save} onToast={onToast}
          fields={[
            { k: 'type', label: 'Appointment type', options: window.MOCK.TYPES },
            { k: 'panelNumber', label: 'Panel number', mono: true },
            { k: 'location', label: 'Location', options: window.MOCK.LOCATIONS },
            { k: 'apptDate', label: 'Appointment date', mono: true },
            { k: 'apptTime', label: 'Appointment time', mono: true },
          ]} />

        {/* 2 · Patient demographics — every form field */}
        <EditSection icon="user" tint="tint-blue" title="Patient demographics" vals={vals} onSave={save} onToast={onToast}
          fields={[
            { k: 'pLast', label: 'Last name' }, { k: 'pFirst', label: 'First name' }, { k: 'pMiddle', label: 'Middle name' }, { k: 'pGender', label: 'Gender', options: ['Male', 'Female', 'Other'] },
            { k: 'pDob', label: 'Date of birth', mono: true }, { k: 'pEmail', label: 'Email' }, { k: 'pCell', label: 'Cell phone', mono: true }, { k: 'pPhone', label: 'Phone number', mono: true },
            { k: 'pSsn', label: 'Social Security #', mono: true }, { k: 'pStreet', label: 'Street' }, { k: 'pUnit', label: 'Unit #' }, { k: 'pCity', label: 'City' },
            { k: 'pState', label: 'State' }, { k: 'pZip', label: 'Zip code', mono: true }, { k: 'pLanguage', label: 'Appointment language' }, { k: 'pReferred', label: 'Referred by' },
            { k: 'pInterpreter', label: 'Needs interpreter', options: ['Yes', 'No'] }, { k: 'pVendor', label: 'Interpreter vendor', col: 6 },
          ]} />

        {/* 3 · Employer details */}
        <EditSection icon="map" tint="tint-slate" title="Employer details" vals={vals} onSave={save} onToast={onToast}
          fields={[
            { k: 'eName', label: 'Employer name', col: 4 }, { k: 'eOcc', label: 'Occupation', col: 4 }, { k: 'ePhone', label: 'Phone number', col: 4, mono: true },
            { k: 'eStreet', label: 'Street' }, { k: 'eCity', label: 'City' }, { k: 'eState', label: 'State' }, { k: 'eZip', label: 'Zip code', mono: true },
          ]} />

        {/* 4–5 · Attorneys — all fields incl. firm, web, fax */}
        <EditSection icon="user" tint="tint-blue" title="Applicant attorney" vals={vals} onSave={save} onToast={onToast} fields={attyFields('a')} />
        <EditSection icon="user" tint="tint-slate" title="Defense attorney" vals={vals} onSave={save} onToast={onToast} fields={attyFields('d')} />

        {/* 6 · Insurance — full */}
        <EditSection icon="doc" tint="tint-teal" title="Insurance" vals={vals} onSave={save} onToast={onToast}
          fields={[
            { k: 'iName', label: 'Insurance company', col: 6 }, { k: 'iSuite', label: 'Suite' }, { k: 'iPhone', label: 'Phone', mono: true }, { k: 'iFax', label: 'Fax', mono: true },
            { k: 'iStreet', label: 'Street' }, { k: 'iCity', label: 'City' }, { k: 'iState', label: 'State' }, { k: 'iZip', label: 'Zip', mono: true },
          ]} />

        {/* 7 · Claim examiner — full */}
        <EditSection icon="user" tint="tint-amber" title="Claim examiner" vals={vals} onSave={save} onToast={onToast}
          fields={[
            { k: 'xName', label: 'Name', col: 6 }, { k: 'xEmail', label: 'Email', col: 6 }, { k: 'xSuite', label: 'Suite' }, { k: 'xPhone', label: 'Phone', mono: true },
            { k: 'xFax', label: 'Fax', mono: true }, { k: 'xStreet', label: 'Street' }, { k: 'xCity', label: 'City' }, { k: 'xState', label: 'State' }, { k: 'xZip', label: 'Zip', mono: true },
          ]} />

        {/* 8 · Claim information — add/edit/delete */}
        <section className="ad-card">
          <div className="ad-card__head"><span className="ic tint-purple"><IDI name="doc" size={18} /></span><h3>Claim information</h3>
            <div className="right"><button className="af-btn af-btn--primary af-btn--sm" onClick={() => openClaim(-1)}><IDI name="plus" size={14} />Add claim</button></div>
          </div>
          <div className="ad-card__body" style={{ padding: 0 }}>
            <table className="ad-table">
              <thead><tr><th>Date of injury</th><th>Claim #</th><th>ADJ #</th><th>WCAB office</th><th>Body parts</th><th style={{ textAlign: 'right' }}>Actions</th></tr></thead>
              <tbody>
                {injuries.map((inj, i) => (
                  <tr key={i}>
                    <td className="num">{inj.cumulative ? inj.doi + ' → ' + (inj.toDoi || '…') : inj.doi}</td>
                    <td className="num">{inj.claim}</td><td className="num">{inj.adj}</td><td>{inj.wcab}</td><td>{inj.bodyParts.join(', ')}</td>
                    <td style={{ textAlign: 'right', whiteSpace: 'nowrap' }}>
                      <span style={{ display: 'inline-flex', gap: 6 }}>
                        <button className="ra-rowbtn" onClick={() => openClaim(i)} title="Edit"><IDI name="doc" size={14} /></button>
                        <button className="ra-rowbtn danger" onClick={() => setInjuries(injuries.filter((_, x) => x !== i))} title="Delete"><IDI name="x" size={14} /></button>
                      </span>
                    </td>
                  </tr>
                ))}
                {injuries.length === 0 && <tr><td colSpan="6" style={{ color: 'var(--n-400)', textAlign: 'center', padding: 24 }}>No claim information.</td></tr>}
              </tbody>
            </table>
          </div>
        </section>

        {/* 9 · Documents — manage */}
        <section className="ad-card">
          <div className="ad-card__head"><span className="ic tint-blue"><IDI name="doc" size={18} /></span><h3>Documents</h3>
            <div className="right"><button className="af-btn af-btn--primary af-btn--sm" onClick={() => { setDocs([...docs, { id: 'd' + Date.now(), name: 'New upload', file: 'upload_' + (docs.length + 1) + '.pdf', size: '1.0 MB', type: 'Correspondence', status: 'pending', when: 'Just now', strike: false }]); onToast('Document uploaded — pending review.'); }}><IDI name="plus" size={14} />Upload</button></div>
          </div>
          <div className="ad-card__body">
            {docs.map(doc => (
              <div className="ad-doc" key={doc.id}>
                <span className="fi"><IDI name="doc" size={18} /></span>
                <div className="meta">
                  <div className="nm"><b>{doc.name}</b>
                    <span className={'ad-docbadge ' + doc.status}><span className="d" />{badge(doc.status)}</span>
                    <span className={'ad-typebadge' + (doc.strike ? ' strike' : '')}>{doc.type}</span>
                  </div>
                  <div className="sub">{doc.file} · {doc.size} · {doc.when}</div>
                  {doc.status === 'rejected' && doc.rejection && <div className="rej"><IDI name="alert" size={13} /><span><b>Reason:</b> {doc.rejection}</span></div>}
                </div>
                <div className="acts">
                  {doc.status === 'pending' && <button className="ra-rowbtn" title="Accept" onClick={() => { setDocs(docs.map(d => d.id === doc.id ? { ...d, status: 'accepted' } : d)); onToast('Document accepted.'); }}><IDI name="check" size={14} /></button>}
                  <button className="ra-rowbtn" title="Download" onClick={() => onToast('Downloading ' + doc.file + '…')}><IDI name="arrowDown" size={14} /></button>
                  <button className="ra-rowbtn danger" title="Delete" onClick={() => setDocs(docs.filter(d => d.id !== doc.id))}><IDI name="x" size={14} /></button>
                </div>
              </div>
            ))}
            {docs.length === 0 && <div className="ad-empty">No documents uploaded.</div>}
          </div>
        </section>

        {/* 10 · Authorized users — manage */}
        <section className="ad-card">
          <div className="ad-card__head"><span className="ic tint-slate"><IDI name="users" size={18} /></span><h3>Additional authorized users</h3>
            <div className="right"><button className="af-btn af-btn--primary af-btn--sm" onClick={() => setModal('adduser')}><IDI name="plus" size={14} />Add user</button></div>
          </div>
          <div className="ad-card__body" style={{ padding: 0 }}>
            <table className="ad-table">
              <thead><tr><th>Name</th><th>Email</th><th>Role</th><th>Rights</th><th style={{ textAlign: 'right' }}>Action</th></tr></thead>
              <tbody>
                {authUsers.map((u, i) => (
                  <tr key={i}>
                    <td>{[u.first, u.last].filter(Boolean).join(' ') || '—'}</td><td>{u.email}</td><td>{u.role}</td>
                    <td>{u.access === 'edit' ? 'View & edit' : 'View only'}</td>
                    <td style={{ textAlign: 'right' }}><button className="ra-rowbtn danger" onClick={() => setAuthUsers(authUsers.filter((_, x) => x !== i))}><IDI name="x" size={14} /></button></td>
                  </tr>
                ))}
                {authUsers.length === 0 && <tr><td colSpan="5" style={{ color: 'var(--n-400)', textAlign: 'center', padding: 24 }}>No additional authorized users.</td></tr>}
              </tbody>
            </table>
          </div>
        </section>

        {/* 11 · Internal-only staff panel */}
        <section className="ad-card" style={{ borderColor: 'var(--blue-200)', background: 'linear-gradient(180deg, var(--blue-50), #fff 60%)' }}>
          <div className="ad-card__head" style={{ borderColor: 'var(--blue-100)' }}><span className="ic tint-blue"><IDI name="settings" size={18} /></span><h3>Internal — staff only</h3></div>
          <div className="ad-card__body"><div className="ad-dl">
            <KV k="Booker (identity)" v="mgonzalez@aol.com" />
            <KV k="Assigned doctor" v="Dr. Alan Reyes" />
            {status === 'pending' && <KV k="Decision due" v="Jun 5, 2026 — 3 days from request" mono />}
            <KV k="Created" v="Jun 2, 2026 · 2:12 PM by Maria Gonzalez" />
            <KV k="Last modified" v="Jun 4, 2026 · 9:40 AM by Sandra Cole" />
            {status === 'approved' && <KV k="Approval comments" v="Arrive 15 min early with photo ID." full />}
            {status === 'rejected' && <KV k="Rejection reason" v={st.note ? st.note.body : ''} full />}
          </div></div>
        </section>
      </div>

      {/* office-action modals */}
      {modal === 'approve' && <IDModal title="Approve appointment" green submit="Approve" icon="check" onClose={closeM} onOk={() => { closeM(); onToast('Appointment approved.'); }}>
        <div className="ra-field col-12"><label>Comments to the requester <span className="opt">(optional)</span></label><textarea className="ra-input" rows={3} value={reason} onChange={e => setReason(e.target.value)} placeholder="e.g. Arrive 15 minutes early with a photo ID." /></div>
      </IDModal>}
      {modal === 'reject' && <IDModal title="Reject appointment" submit="Reject" icon="x" disabled={!reason.trim()} onClose={closeM} onOk={() => { closeM(); onToast('Appointment rejected.'); }}>
        <div className="ra-field col-12"><label>Rejection reason <span className="req">*</span></label><textarea className="ra-input" rows={4} maxLength={500} value={reason} onChange={e => setReason(e.target.value)} placeholder="Explain why so the requester can correct and re-submit." /><div className="ra-hint" style={{ textAlign: 'right' }}>{reason.length}/500</div></div>
      </IDModal>}
      {modal === 'cancel' && <IDModal title="Cancel appointment" submit="Cancel appointment" icon="x" disabled={!reason.trim()} onClose={closeM} onOk={() => { closeM(); onToast('Appointment cancelled.'); }}>
        <div className="ra-field col-12"><label>Cancellation reason <span className="req">*</span></label><textarea className="ra-input" rows={4} maxLength={500} value={reason} onChange={e => setReason(e.target.value)} /></div>
      </IDModal>}
      {modal === 'reschedule' && <IDModal title="Reschedule appointment" submit="Reschedule" icon="refresh" disabled={!slot} onClose={closeM} onOk={() => { closeM(); onToast('Appointment rescheduled.'); }}>
        <div className="ra-field col-12" style={{ marginBottom: 14 }}><label>New slot <span className="req">*</span></label><select className="ra-select" value={slot} onChange={e => setSlot(e.target.value)}><option value="">Select an available slot</option>{window.RA.TIME_SLOTS.map(s => <option key={s}>Jun 18, 2026 · {s}</option>)}</select></div>
        <div className="ra-field col-12"><label>Reason</label><textarea className="ra-input" rows={3} value={reason} onChange={e => setReason(e.target.value)} /></div>
      </IDModal>}

      {/* claim-info modal */}
      {modal === 'claim' && cDraft && <IDModal title={cIdx >= 0 ? 'Edit claim' : 'Add claim'} submit={cIdx >= 0 ? 'Save claim' : 'Add claim'} icon="check" onClose={closeM} onOk={saveClaim} wide>
        <div className="ra-field col-3"><label>Cumulative trauma</label>
          <div className="ra-radios">
            <label className="ra-radio"><input type="radio" checked={cDraft.cumulative === true} onChange={() => setCDraft({ ...cDraft, cumulative: true })} />Yes</label>
            <label className="ra-radio"><input type="radio" checked={cDraft.cumulative === false} onChange={() => setCDraft({ ...cDraft, cumulative: false })} />No</label>
          </div>
        </div>
        <div className="ra-field col-3"><label>{cDraft.cumulative ? 'From date' : 'Date of injury'} <span className="req">*</span></label><input className="ra-input" value={cDraft.doi} onChange={e => setCDraft({ ...cDraft, doi: e.target.value })} placeholder="MM/DD/YYYY" /></div>
        {cDraft.cumulative && <div className="ra-field col-3"><label>To date</label><input className="ra-input" value={cDraft.toDoi} onChange={e => setCDraft({ ...cDraft, toDoi: e.target.value })} placeholder="MM/DD/YYYY" /></div>}
        <div className="ra-field col-3"><label>Claim number <span className="req">*</span></label><input className="ra-input" value={cDraft.claim} onChange={e => setCDraft({ ...cDraft, claim: e.target.value })} placeholder="e.g. WC24-10480" /></div>
        <div className="ra-field col-3"><label>ADJ #</label><input className="ra-input" value={cDraft.adj} onChange={e => setCDraft({ ...cDraft, adj: e.target.value })} placeholder="e.g. ADJ-4471102" /></div>
        <div className="ra-field col-6"><label>WCAB office (venue)</label>
          <select className="ra-select" value={cDraft.wcab} onChange={e => setCDraft({ ...cDraft, wcab: e.target.value })}>
            <option value="">Select office</option>
            {window.RA.WCAB_OFFICES.map(o => <option key={o.id} value={o.displayName}>{o.displayName}</option>)}
          </select>
        </div>
        <div className="ra-field col-12"><label>Body parts <span className="req">*</span></label>
          {cDraft.bodyParts.map((bp, i) => (
            <div key={i} style={{ display: 'flex', gap: 8, marginBottom: 8 }}>
              <input className="ra-input" value={bp} placeholder="e.g. Lower back" onChange={e => { const a = cDraft.bodyParts.slice(); a[i] = e.target.value; setCDraft({ ...cDraft, bodyParts: a }); }} />
              <button className="ra-rowbtn danger" disabled={cDraft.bodyParts.length <= 1} onClick={() => setCDraft({ ...cDraft, bodyParts: cDraft.bodyParts.filter((_, x) => x !== i) })}><IDI name="x" size={14} /></button>
            </div>
          ))}
          <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => setCDraft({ ...cDraft, bodyParts: [...cDraft.bodyParts, ''] })}><IDI name="plus" size={13} />Add body part</button>
        </div>
      </IDModal>}

      {/* add-authorized-user modal */}
      {modal === 'adduser' && <IDModal title="Add authorized user" submit="Save" icon="check" disabled={!au.email.trim() || !au.role} onClose={closeM} onOk={addAuthUser}>
        <div className="ra-field col-6"><label>First name</label><input className="ra-input" value={au.first} onChange={e => setAu({ ...au, first: e.target.value })} /></div>
        <div className="ra-field col-6"><label>Last name</label><input className="ra-input" value={au.last} onChange={e => setAu({ ...au, last: e.target.value })} /></div>
        <div className="ra-field col-6"><label>Email <span className="req">*</span></label><input className="ra-input" type="email" value={au.email} onChange={e => setAu({ ...au, email: e.target.value })} placeholder="name@example.com" /></div>
        <div className="ra-field col-6"><label>User role <span className="req">*</span></label>
          <select className="ra-select" value={au.role} onChange={e => setAu({ ...au, role: e.target.value })}><option value="">Select role</option>{window.RA.AUTH_ROLES.map(r => <option key={r}>{r}</option>)}</select>
        </div>
        <div className="ra-field col-6"><label>Rights</label>
          <select className="ra-select" value={au.access} onChange={e => setAu({ ...au, access: e.target.value })}>{window.RA.ACCESS_TYPES.map(a => <option key={a.value} value={a.value}>{a.label}</option>)}</select>
        </div>
      </IDModal>}
    </div>
  );
}

function IDModal({ title, submit, icon, green, disabled, wide, onClose, onOk, children }) {
  return (
    <div className="ra-scrim" onClick={onClose}>
      <div className={'ra-modal ' + (wide ? 'ra-modal--lg' : 'ra-modal--md')} onClick={e => e.stopPropagation()}>
        <div className="ra-modal__head"><h3>{title}</h3><button className="ext-iconbtn x" onClick={onClose}><IDI name="x" size={17} /></button></div>
        <div className="ra-modal__body"><div className="ra-grid">{children}</div></div>
        <div className="ra-modal__foot"><button className="af-btn af-btn--ghost" onClick={onClose}>Close</button><button className={'af-btn ' + (green ? 'af-btn--green' : 'af-btn--primary')} disabled={disabled} style={disabled ? { opacity: .5, cursor: 'not-allowed' } : null} onClick={onOk}><IDI name={icon} size={15} />{submit}</button></div>
      </div>
    </div>
  );
}

window.InDetail = InDetail;
