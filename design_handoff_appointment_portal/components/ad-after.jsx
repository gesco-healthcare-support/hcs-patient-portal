/* global React */
/* ============================================================
   Appointment Detail / View — AFTER (read-only for external roles).
   Status banner + timeline + sticky section nav + read-only summary
   sections + interactive Document Manager + request/upload modals.
   ============================================================ */
const { useState: useStateAD, useRef: useRefAD } = React;
function ADI({ name, size }) { return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size || 18)} />; }

function KV({ k, v, mono, full }) {
  return (
    <div className={'ad-field' + (full ? ' full' : '')}>
      <span className="k">{k}</span>
      <span className={'v' + (mono ? ' mono' : '') + (v ? '' : ' empty')}>{v || '—'}</span>
    </div>
  );
}

function ADCard({ id, icon, tint, title, right, children }) {
  return (
    <section className="ad-card" id={id}>
      <div className="ad-card__head">
        {icon && <span className={'ic ' + (tint || 'tint-blue')}><ADI name={icon} size={18} /></span>}
        <h3>{title}</h3>
        {right && <div className="right">{right}</div>}
      </div>
      <div className="ad-card__body">{children}</div>
    </section>
  );
}

/* address one-liner */
function addr(o, keys) { return keys.map(k => o[k]).filter(Boolean).join(', '); }

const AD_SECTIONS = [
  ['overview', 'Overview'], ['patient', 'Patient'], ['parties', 'Case contacts'],
  ['claim', 'Claim'], ['documents', 'Documents'], ['users', 'Access'],
];

function AfterAppointmentDetail({ roleKey, status }) {
  const D = window.AD.DETAIL;
  const st = window.AD.STATUSES[status] || window.AD.STATUSES.approved;
  const role = window.RA.BOOKERS[roleKey];
  const [toast, setToast] = useStateAD(null);
  const [active, setActive] = useStateAD('overview');
  const [modal, setModal] = useStateAD(null); // 'reschedule' | 'cancel' | 'upload' | 'adduser'
  const [docs, setDocs] = useStateAD(D.documents);
  const [authUsers, setAuthUsers] = useStateAD(D.authUsers);
  const [filter, setFilter] = useStateAD('all');
  const [reason, setReason] = useStateAD('');
  const [slot, setSlot] = useStateAD('');
  const [up, setUp] = useStateAD({ name: '', type: '', other: '', file: '' });
  const [au, setAu] = useStateAD({ first: '', last: '', email: '', role: '', access: 'view' });

  function showToast(m) { setToast(m); clearTimeout(window.__adT); window.__adT = setTimeout(() => setToast(null), 3000); }
  function closeModal() { setModal(null); setReason(''); setSlot(''); setUp({ name: '', type: '', other: '', file: '' }); setAu({ first: '', last: '', email: '', role: '', access: 'view' }); }

  function jump(id) {
    setActive(id);
    const el = document.getElementById('ad-sec-' + id);
    const vp = document.querySelector('.viewport');
    if (el && vp) vp.scrollTo({ top: vp.scrollTop + el.getBoundingClientRect().top - vp.getBoundingClientRect().top - 60, behavior: 'smooth' });
    else if (el) window.scrollTo({ top: el.getBoundingClientRect().top + window.scrollY - 70, behavior: 'smooth' });
  }

  // document required tracker (recompute received from current docs by label)
  const reqDocs = D.requiredDocs.map(r => ({ ...r, received: docs.some(d => (d.type === r.name) ) }));
  const missing = reqDocs.filter(r => !r.received);
  const filterTypes = [...new Set(docs.map(d => d.type))];
  const shownDocs = filter === 'all' ? docs : docs.filter(d => d.type === filter);

  const acts = st.actions;
  const can = a => acts.includes(a);

  function submitReschedule() { closeModal(); showToast('Reschedule request submitted — pending staff approval.'); }
  function submitCancel() { closeModal(); showToast('Cancellation request submitted — pending staff approval.'); }
  function submitUpload() {
    if (!up.file) return;
    const label = up.type === '__other' ? (up.other || 'Other') : up.type;
    setDocs([...docs, { id: 'd' + Date.now(), name: up.name || up.file, file: up.file, size: '1.2 MB', type: label, status: 'pending', when: 'Just now', strike: label === 'Panel Strike List' }]);
    closeModal(); showToast('Document uploaded — pending staff review.');
  }
  function addUser() { if (!au.email.trim() || !au.role) return; setAuthUsers([...authUsers, au]); closeModal(); showToast('Authorized user added.'); }

  const patientName = D.patient.first + ' ' + D.patient.last;

  return (
    <div className="ad">
      {/* top navbar */}
      <header className="ext-nav">
        <div className="ext-nav__in">
          <a className="ext-brand" href="#" onClick={e => e.preventDefault()} aria-label="Home">
            <img src="assets/header-logo.png" alt={role.org || 'Clinic'} />
            <span className="ext-brand__div" />
            <span className="ext-brand__tag"><b>Appointment Portal</b><span>Patient &amp; case portal</span></span>
          </a>
          <div className="ext-nav__spacer" />
          <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => showToast('Returning to home…')}><ADI name="chevLeft" size={15} />Back to home</button>
          <div className="ext-acct" style={{ cursor: 'default' }}>
            <span className="ava" style={{ background: window.AfCommon.avaColor(role.name) }}>{window.AfCommon.initials(role.first, role.name.split(' ')[1] || role.label)}</span>
            <span className="who"><b>{role.name}</b><span>{role.label}</span></span>
          </div>
        </div>
      </header>

      {/* status banner */}
      <div className={'ad-banner ' + st.banner}>
        <div className="ad-banner__in">
          <div className="ad-banner__top">
            <div style={{ minWidth: 0 }}>
              <div className="ad-banner__crumb"><a href="#" onClick={e => { e.preventDefault(); showToast('Returning to home…'); }}>My appointments</a><ADI name="chevRight" size={12} />{D.confirmation}</div>
              <h1>
                <span className="ttl">{D.type} <span style={{ fontWeight: 500, opacity: .85 }}>· {patientName}</span></span>
                <span className="ad-statepill"><span className="d" />{st.label}</span>
              </h1>
              <div className="ad-banner__meta">
                <div className="it"><div className="k">Confirmation</div><div className="v mono">{D.confirmation}</div></div>
                <div className="it"><div className="k">Panel #</div><div className="v mono">{D.panelNumber}</div></div>
                <div className="it"><div className="k">Location</div><div className="v">{D.location}</div></div>
                <div className="it"><div className="k">Date &amp; time</div><div className="v">{window.AfCommon.fmtDateShort(D.date)} · {window.AfCommon.fmtTime(D.date)}</div></div>
              </div>
            </div>
          </div>

          <div className="ad-callout">
            <span className="ic"><ADI name={st.callout.icon} size={18} /></span>
            <div><b>{st.callout.title}</b><span>{st.callout.body}</span></div>
          </div>
        </div>
      </div>

      {/* sticky section nav + actions */}
      <nav className="ad-nav">
        <div className="ad-nav__in">
          <div className="ad-nav__links">
            {AD_SECTIONS.map(([id, lbl]) => (
              <a key={id} href="#" className={active === id ? 'on' : ''} onClick={e => { e.preventDefault(); jump(id); }}>{lbl}</a>
            ))}
          </div>
          <div className="ad-nav__actions">
            {can('reschedule') && <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => setModal('reschedule')}><ADI name="refresh" size={14} />Reschedule</button>}
            {can('cancel') && <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => setModal('cancel')}><ADI name="x" size={14} />Cancel</button>}
            {can('rerequest') && <button className="af-btn af-btn--primary af-btn--sm" onClick={() => showToast('Opening a new request…')}><ADI name="refresh" size={14} />Re-request</button>}
            {can('summary') && <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => showToast('Preparing appointment summary…')}><ADI name="doc" size={14} />Summary</button>}
            {can('upload') && <button className="af-btn af-btn--primary af-btn--sm" onClick={() => setModal('upload')}><ADI name="doc" size={14} />Upload document</button>}
          </div>
        </div>
      </nav>

      <div className="ad-wrap">
        {st.note && (
          <div className={'ad-note ad-note--' + st.note.kind}>
            <span className="ic"><ADI name={st.note.kind === 'rejected' ? 'alert' : 'check'} size={18} /></span>
            <div><b>{st.note.title}</b><p>{st.note.body}</p></div>
          </div>
        )}

        {/* Overview / Appointment details */}
        <div id="ad-sec-overview">
          <ADCard icon="calendar" tint="tint-blue" title="Appointment details">
            <div className="ad-dl">
              <KV k="Appointment type" v={D.type} />
              <KV k="Confirmation #" v={D.confirmation} mono />
              <KV k="Panel #" v={D.panelNumber} mono />
              <KV k="Status" v={st.label} />
              <KV k="Location" v={D.location} />
              <KV k="Appointment date" v={window.AfCommon.fmtDateShort(D.date)} />
              <KV k="Appointment time" v={window.AfCommon.fmtTime(D.date)} />
              <KV k="Requested on" v={window.AfCommon.fmtDateShort(D.requestedOn) + ' · ' + window.AfCommon.fmtTime(D.requestedOn)} />
            </div>
          </ADCard>
        </div>

        {/* Patient + Employer */}
        <div id="ad-sec-patient">
          <ADCard icon="user" tint="tint-blue" title="Patient demographics">
            <div className="ad-dl">
              <KV k="Name" v={[D.patient.first, D.patient.middle, D.patient.last].filter(Boolean).join(' ')} />
              <KV k="Gender" v={D.patient.gender} />
              <KV k="Date of birth" v={D.patient.dob} mono />
              <KV k="SSN" v={D.patient.ssnMasked} mono />
              <KV k="Email" v={D.patient.email} />
              <KV k="Cell phone" v={D.patient.cell} mono />
              <KV k="Phone" v={D.patient.phone} mono />
              <KV k="Language" v={D.patient.language} />
              <KV k="Interpreter" v={D.patient.interpreter ? 'Yes — ' + D.patient.interpreterVendor : 'No'} />
              <KV k="Referred by" v={D.patient.referredBy} />
              <KV k="Address" v={addr(D.patient, ['street', 'unit', 'city', 'state', 'zip'])} full />
            </div>
          </ADCard>
          <ADCard icon="map" tint="tint-slate" title="Employer details">
            <div className="ad-dl">
              <KV k="Employer" v={D.employer.name} />
              <KV k="Occupation" v={D.employer.occupation} />
              <KV k="Phone" v={D.employer.phone} mono />
              <KV k="Address" v={addr(D.employer, ['street', 'city', 'state', 'zip'])} full />
            </div>
          </ADCard>
        </div>

        {/* Parties */}
        <div id="ad-sec-parties">
          {[['Applicant attorney', D.applicant, 'tint-blue'], ['Defense attorney', D.defense, 'tint-slate']].map(([t, p, tint]) => (
            <ADCard key={t} icon="user" tint={tint} title={t}>
              <div className="ad-dl">
                <KV k="Name" v={p.name} /><KV k="Firm" v={p.firm} /><KV k="Email" v={p.email} /><KV k="Phone" v={p.phone} mono />
                <KV k="Fax" v={p.fax} mono /><KV k="Web" v={p.web} /><KV k="Address" v={addr(p, ['street', 'city', 'state', 'zip'])} full />
              </div>
            </ADCard>
          ))}
          <ADCard icon="doc" tint="tint-teal" title="Insurance">
            <div className="ad-dl">
              <KV k="Company" v={D.insurance.name} /><KV k="Suite" v={D.insurance.suite} /><KV k="Phone" v={D.insurance.phone} mono /><KV k="Fax" v={D.insurance.fax} mono />
              <KV k="Address" v={addr(D.insurance, ['street', 'city', 'state', 'zip'])} full />
            </div>
          </ADCard>
          <ADCard icon="user" tint="tint-amber" title="Claim examiner">
            <div className="ad-dl">
              <KV k="Name" v={D.examiner.name} /><KV k="Email" v={D.examiner.email} /><KV k="Phone" v={D.examiner.phone} mono /><KV k="Suite" v={D.examiner.suite} /><KV k="Fax" v={D.examiner.fax} mono />
              <KV k="Address" v={addr(D.examiner, ['street', 'city', 'state', 'zip'])} full />
            </div>
          </ADCard>
        </div>

        {/* Claim information */}
        <div id="ad-sec-claim">
          <ADCard icon="doc" tint="tint-purple" title="Claim information">
            <table className="ad-table">
              <thead><tr><th>Date of injury</th><th>Claim #</th><th>ADJ #</th><th>WCAB office</th><th>Body parts</th></tr></thead>
              <tbody>
                {D.injuries.map((inj, i) => (
                  <tr key={i}>
                    <td className="num">{inj.cumulative ? inj.doi + ' → ' + inj.toDoi : inj.doi}</td>
                    <td className="num">{inj.claim}</td><td className="num">{inj.adj}</td><td>{inj.wcab}</td><td>{inj.bodyParts.join(', ')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </ADCard>
        </div>

        {/* Documents */}
        <div id="ad-sec-documents">
          <ADCard icon="doc" tint="tint-blue" title="Documents"
            right={can('upload') && <button className="af-btn af-btn--primary af-btn--sm" onClick={() => setModal('upload')}><ADI name="plus" size={14} />Upload</button>}>
            <div className={'ad-reqbox ' + (missing.length === 0 ? 'ok' : 'warn')}>
              <div className="ad-reqbox__h">
                <ADI name={missing.length === 0 ? 'check' : 'alert'} size={15} />
                {missing.length === 0 ? 'All required documents received.' : (missing.length + ' of ' + reqDocs.length + ' required documents outstanding')}
              </div>
              <div className="ad-reqchips">
                {reqDocs.map(r => <span key={r.name} className={'ad-reqchip ' + (r.received ? 'got' : 'miss')}><ADI name={r.received ? 'check' : 'doc'} size={12} />{r.name}</span>)}
              </div>
            </div>

            {filterTypes.length > 1 && (
              <div className="ad-docfilter">
                <label>Filter by type</label>
                <select value={filter} onChange={e => setFilter(e.target.value)}>
                  <option value="all">All types</option>
                  {filterTypes.map(t => <option key={t} value={t}>{t}</option>)}
                </select>
              </div>
            )}

            {shownDocs.map(doc => (
              <div className="ad-doc" key={doc.id}>
                <span className="fi"><ADI name="doc" size={18} /></span>
                <div className="meta">
                  <div className="nm">
                    <b>{doc.name}</b>
                    <span className={'ad-docbadge ' + doc.status}><span className="d" />{doc.status === 'accepted' ? 'Accepted' : doc.status === 'pending' ? 'Pending review' : 'Rejected'}</span>
                    <span className={'ad-typebadge' + (doc.strike ? ' strike' : '')}>{doc.type}</span>
                  </div>
                  <div className="sub">{doc.file} · {doc.size} · {doc.when}</div>
                  {doc.status === 'rejected' && doc.rejection && <div className="rej"><ADI name="alert" size={13} /><span><b>Reason:</b> {doc.rejection}</span></div>}
                </div>
                <div className="acts">
                  <button className="ra-rowbtn" onClick={() => showToast('Downloading ' + doc.file + '…')} title="Download"><ADI name="arrowDown" size={14} /></button>
                  <button className="ra-rowbtn danger" onClick={() => setDocs(docs.filter(d => d.id !== doc.id))} title="Delete"><ADI name="x" size={14} /></button>
                </div>
              </div>
            ))}
            {shownDocs.length === 0 && <div className="ad-empty">No documents uploaded yet.</div>}
          </ADCard>

          <ADCard icon="doc" tint="tint-purple" title="Appointment packet">
            <div className="ad-packet">
              <span className="ic"><ADI name="doc" size={22} /></span>
              <div className="tx"><b>Download appointment packet</b><span>A combined PDF of the appointment details and all accepted documents.</span></div>
              <button className="af-btn af-btn--ghost" onClick={() => showToast('Generating packet PDF…')}><ADI name="arrowDown" size={15} />Download</button>
            </div>
          </ADCard>
        </div>

        {/* Authorized users */}
        <div id="ad-sec-users">
          <ADCard icon="users" tint="tint-slate" title="Additional authorized users"
            right={<button className="af-btn af-btn--primary af-btn--sm" onClick={() => setModal('adduser')}><ADI name="plus" size={14} />Add user</button>}>
            {authUsers.length === 0 ? <div className="ad-empty">No additional authorized users.</div> : (
              <table className="ad-table">
                <thead><tr><th>Name</th><th>Email</th><th>Role</th><th>Rights</th><th style={{ textAlign: 'right' }}>Action</th></tr></thead>
                <tbody>
                  {authUsers.map((u, i) => (
                    <tr key={i}>
                      <td>{[u.first, u.last].filter(Boolean).join(' ') || '—'}</td><td>{u.email}</td><td>{u.role}</td>
                      <td>{u.access === 'edit' ? 'View & edit' : 'View only'}</td>
                      <td style={{ textAlign: 'right' }}><button className="ra-rowbtn danger" onClick={() => setAuthUsers(authUsers.filter((_, x) => x !== i))}><ADI name="x" size={14} /></button></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </ADCard>
        </div>
      </div>

      {/* ---------- Modals ---------- */}
      {modal === 'reschedule' && (
        <ADModal title="Request a reschedule" onClose={closeModal} onSubmit={submitReschedule} submitLabel="Submit request" submitIcon="refresh" disabled={!slot || !reason.trim()}>
          <div className="ra-field col-12" style={{ marginBottom: 16 }}>
            <label>New slot <span className="req">*</span></label>
            <select className="ra-select" value={slot} onChange={e => setSlot(e.target.value)}>
              <option value="">Select an available slot</option>
              {window.RA.TIME_SLOTS.map(s => <option key={s} value={s}>Jun 18, 2026 · {s}</option>)}
            </select>
          </div>
          <div className="ra-field col-12">
            <label>Reason <span className="req">*</span></label>
            <textarea className="ra-input" rows={4} maxLength={500} value={reason} onChange={e => setReason(e.target.value)} placeholder="Briefly explain why you need to reschedule…" />
            <div className="ra-hint" style={{ textAlign: 'right' }}>{reason.length} / 500</div>
          </div>
          <div className="ra-note" style={{ marginTop: 4 }}><span className="i"><ADI name="alert" size={15} /></span><span>This is a <b>request</b> — the appointment stays as-is until clinic staff approve the change.</span></div>
        </ADModal>
      )}
      {modal === 'cancel' && (
        <ADModal title="Request a cancellation" onClose={closeModal} onSubmit={submitCancel} submitLabel="Submit request" submitIcon="x" submitGreen disabled={!reason.trim()}>
          <div className="ra-field col-12">
            <label>Reason <span className="req">*</span></label>
            <textarea className="ra-input" rows={4} maxLength={500} value={reason} onChange={e => setReason(e.target.value)} placeholder="Briefly explain why you need to cancel…" />
            <div className="ra-hint" style={{ textAlign: 'right' }}>{reason.length} / 500</div>
          </div>
          <div className="ra-note warn" style={{ marginTop: 14 }}><span className="i"><ADI name="alert" size={15} /></span><span>This is a <b>request</b> — the appointment stays scheduled until clinic staff approve the cancellation.</span></div>
        </ADModal>
      )}
      {modal === 'upload' && (
        <ADModal title="Upload a document" onClose={closeModal} onSubmit={submitUpload} submitLabel="Upload" submitIcon="arrowUp" disabled={!up.file}>
          <div className="ra-grid">
            <div className="ra-field col-6"><label>Document name</label><input className="ra-input" value={up.name} onChange={e => setUp({ ...up, name: e.target.value })} placeholder="e.g. Medical records 2026" maxLength={200} /></div>
            <div className="ra-field col-6"><label>Document type</label>
              <select className="ra-select" value={up.type} onChange={e => setUp({ ...up, type: e.target.value })}>
                <option value="">— None —</option>
                {window.RA.DOC_TYPES.map(t => <option key={t.id} value={t.displayName}>{t.displayName}</option>)}
                <option value="__other">Other…</option>
              </select>
            </div>
            {up.type === '__other' && <div className="ra-field col-12"><label>Other document type</label><input className="ra-input" value={up.other} onChange={e => setUp({ ...up, other: e.target.value })} maxLength={100} placeholder="Type the document category" /></div>}
          </div>
          <div className="ad-drop" style={{ marginTop: 16 }} onClick={() => setUp({ ...up, file: up.file ? up.file : 'uploaded_document.pdf' })}>
            <div className="ic"><ADI name="doc" size={26} /></div>
            <b>{up.file ? up.file : 'Drag & drop a file here, or click to browse'}</b>
            <span>PDF, JPG, PNG · up to 10 MB</span>
          </div>
        </ADModal>
      )}
      {modal === 'adduser' && (
        <ADModal title="Add authorized user" onClose={closeModal} onSubmit={addUser} submitLabel="Save" disabled={!au.email.trim() || !au.role}>
          <div className="ra-grid">
            <div className="ra-field col-6"><label>First name</label><input className="ra-input" value={au.first} onChange={e => setAu({ ...au, first: e.target.value })} maxLength={64} /></div>
            <div className="ra-field col-6"><label>Last name</label><input className="ra-input" value={au.last} onChange={e => setAu({ ...au, last: e.target.value })} maxLength={64} /></div>
            <div className="ra-field col-6"><label>Email <span className="req">*</span></label><input className="ra-input" type="email" value={au.email} onChange={e => setAu({ ...au, email: e.target.value })} placeholder="name@example.com" /></div>
            <div className="ra-field col-6"><label>User role <span className="req">*</span></label>
              <select className="ra-select" value={au.role} onChange={e => setAu({ ...au, role: e.target.value })}><option value="">Select role</option>{window.RA.AUTH_ROLES.map(r => <option key={r} value={r}>{r}</option>)}</select>
            </div>
            <div className="ra-field col-6"><label>Rights</label>
              <select className="ra-select" value={au.access} onChange={e => setAu({ ...au, access: e.target.value })}>{window.RA.ACCESS_TYPES.map(a => <option key={a.value} value={a.value}>{a.label}</option>)}</select>
            </div>
          </div>
        </ADModal>
      )}

      {toast && <div className="af-toast"><ADI name="check" size={17} />{toast}</div>}
    </div>
  );
}

function ADModal({ title, onClose, onSubmit, submitLabel, submitIcon, submitGreen, disabled, children }) {
  return (
    <div className="ra-scrim" onClick={onClose}>
      <div className="ra-modal ra-modal--md" onClick={e => e.stopPropagation()}>
        <div className="ra-modal__head"><h3>{title}</h3><button className="ext-iconbtn x" onClick={onClose} aria-label="Close"><ADI name="x" size={17} /></button></div>
        <div className="ra-modal__body">{children}</div>
        <div className="ra-modal__foot">
          <button className="af-btn af-btn--ghost" onClick={onClose}>Cancel</button>
          <button className={'af-btn ' + (submitGreen ? 'af-btn--green' : 'af-btn--primary')} disabled={disabled} style={disabled ? { opacity: .5, cursor: 'not-allowed' } : null} onClick={onSubmit}>
            {submitIcon && <ADI name={submitIcon} size={15} />}{submitLabel}
          </button>
        </div>
      </div>
    </div>
  );
}

window.AfterAppointmentDetail = AfterAppointmentDetail;
