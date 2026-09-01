/* global React */
/* ============================================================
   External account pages — Notifications/Activity · My Documents ·
   Notification preferences. Role-aware. Reuses ad-* (doc rows/badges)
   + ra-switch (toggle). One file, harness switches page + role.
   ============================================================ */
const { useState: useStateB, useMemo: useMemoB } = React;
function BI({ name, size }) { return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size || 18)} />; }

const CAT_META = {
  approvals: { label: 'Approvals', icon: 'check', tint: 'tint-green' },
  changes: { label: 'Changes', icon: 'refresh', tint: 'tint-amber' },
  documents: { label: 'Documents', icon: 'doc', tint: 'tint-blue' },
  consent: { label: 'Consent', icon: 'users', tint: 'tint-purple' },
};

function notifsFor(role) {
  const np = !role.isNonPatient; // patient → "your"; else show client name
  const who = name => np ? 'your' : (name + '\u2019s');
  return [
    { id: 'n1', cat: 'approvals', tone: 'tint-green', date: 'Today', time: '12m ago', unread: true, conf: 'PQ-24817', title: 'Appointment approved', body: 'The Panel QME for ' + (np ? 'you' : 'Maria Gonzalez') + ' (PQ-24817) was approved for Jun 16, 9:30 AM.' },
    { id: 'n2', cat: 'documents', tone: 'tint-blue', date: 'Today', time: '3h ago', unread: true, conf: 'AM-24820', title: 'Documents requested', body: 'Please upload the medical records packet for ' + who('David Chen') + ' appointment AM-24820 before Jun 16.' },
    { id: 'n3', cat: 'changes', tone: 'tint-amber', date: 'Yesterday', time: '1d ago', unread: true, conf: 'FU-24779', title: 'Reschedule requested', body: 'A reschedule was requested on FU-24779 — awaiting clinic confirmation.' },
    { id: 'n4', cat: 'consent', tone: 'tint-purple', date: 'Yesterday', time: '1d ago', unread: false, conf: 'RE-24788', title: 'Consent required', body: 'Opposing counsel consent is pending for the change request on RE-24788.' },
    { id: 'n5', cat: 'approvals', tone: 'tint-red', date: 'Earlier', time: '3d ago', unread: false, conf: 'AM-24744', title: 'Appointment rejected', body: 'The request AM-24744 was not approved. See the appointment for the reason.' },
    { id: 'n6', cat: 'documents', tone: 'tint-blue', date: 'Earlier', time: '4d ago', unread: false, conf: 'SR-24690', title: 'Document accepted', body: 'The supplemental report for SR-24690 was reviewed and accepted.' },
    { id: 'n7', cat: 'changes', tone: 'tint-teal', date: 'Earlier', time: '5d ago', unread: false, conf: 'RE-24710', title: 'Cancellation approved', body: 'The cancellation request on RE-24710 was approved by clinic staff.' },
  ];
}

function docsFor(role) {
  const np = !role.isNonPatient;
  const mk = (conf, type, patient, date, docs, required) => ({ conf, type, patient: np ? null : patient, date, docs, required });
  return [
    mk('PQ-24817', 'Panel QME', 'Maria Gonzalez', 'Jun 16, 2026', [
      { id: 'a', name: 'Medical records 2025', file: 'medical_records.pdf', size: '2.4 MB', type: 'Medical Records', status: 'accepted', when: 'Jun 3', strike: false },
      { id: 'b', name: 'Panel strike list', file: 'panel_strike.pdf', size: '0.6 MB', type: 'Panel Strike List', status: 'pending', when: 'Jun 4', strike: true },
      { id: 'c', name: 'Cover letter', file: 'cover.pdf', size: '0.2 MB', type: 'Cover Letter', status: 'rejected', when: 'Jun 4', rejection: 'Illegible scan — please re-upload a clear copy.', strike: false },
    ], [{ name: 'Panel Strike List', received: true }, { name: 'Medical Records', received: true }, { name: 'Cover Letter', received: false }]),
    mk('AM-24820', 'AME Evaluation', 'David Chen', 'Jun 16, 2026', [
      { id: 'd', name: 'Deposition transcript', file: 'deposition.pdf', size: '1.1 MB', type: 'Deposition Transcript', status: 'accepted', when: 'Jun 2', strike: false },
    ], [{ name: 'Medical Records', received: false }]),
    mk('RE-24788', 'QME Re-Evaluation', 'Robert Williams', 'Jun 8, 2026', [
      { id: 'e', name: 'Prior report', file: 'prior_report.pdf', size: '0.9 MB', type: 'Correspondence', status: 'accepted', when: 'May 30', strike: false },
    ], []),
  ];
}

const PREF_CATS = [
  { key: 'approvals', icon: 'check', tint: 'tint-green', title: 'Approvals & decisions', desc: 'When an appointment request is approved or rejected.' },
  { key: 'changes', icon: 'refresh', tint: 'tint-amber', title: 'Reschedules & cancellations', desc: 'Updates on reschedule and cancellation requests.' },
  { key: 'documents', icon: 'doc', tint: 'tint-blue', title: 'Document requests', desc: 'When the clinic needs a document, or reviews one you sent.' },
  { key: 'consent', icon: 'users', tint: 'tint-purple', title: 'Consent requests', desc: 'When another party needs your agreement on a change.' },
  { key: 'reminders', icon: 'clock', tint: 'tint-slate', title: 'Appointment reminders', desc: 'Reminders before an upcoming evaluation.' },
];

function BShell({ role, icon, tint, title, sub, right, children, onToast }) {
  return (
    <div className="b">
      <header className="ext-nav">
        <div className="ext-nav__in">
          <a className="ext-brand" href="#" onClick={e => e.preventDefault()} aria-label="Home">
            <img src="assets/header-logo.png" alt={role.org || 'Clinic'} />
            <span className="ext-brand__div" />
            <span className="ext-brand__tag"><b>Appointment Portal</b><span>Patient &amp; case portal</span></span>
          </a>
          <div className="ext-nav__spacer" />
          <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => onToast('Returning to home…')}><BI name="chevLeft" size={15} />Back to home</button>
          <div className="ext-acct" style={{ cursor: 'default' }}>
            <span className="ava" style={{ background: window.AfCommon.avaColor(role.name) }}>{window.AfCommon.initials(role.first, role.name.split(' ')[1] || role.label)}</span>
            <span className="who"><b>{role.name}</b><span>{role.label}</span></span>
          </div>
        </div>
      </header>
      <div className="b-head">
        <div className="b-head__in">
          <span className={'b-head__ic ' + tint}><BI name={icon} size={22} /></span>
          <div><h1>{title}</h1><p>{sub}</p></div>
          {right && <div className="right">{right}</div>}
        </div>
      </div>
      <div className="b-wrap">{children}</div>
    </div>
  );
}

/* ---------------- Notifications ---------------- */
function NotificationsPage({ role, onToast }) {
  const [items, setItems] = useStateB(() => notifsFor(role));
  const [filter, setFilter] = useStateB('all');
  React.useEffect(() => { setItems(notifsFor(role)); }, [role.key]);

  const counts = useMemoB(() => {
    const c = { all: items.length, unread: items.filter(i => i.unread).length };
    Object.keys(CAT_META).forEach(k => c[k] = items.filter(i => i.cat === k).length);
    return c;
  }, [items]);

  const shown = items.filter(i => filter === 'all' ? true : filter === 'unread' ? i.unread : i.cat === filter);
  const groups = ['Today', 'Yesterday', 'Earlier'].map(d => [d, shown.filter(i => i.date === d)]).filter(([, a]) => a.length);

  const rail = [['all', 'All', 'inbox'], ['unread', 'Unread', 'bell'], ...Object.entries(CAT_META).map(([k, m]) => [k, m.label, m.icon])];

  return (
    <BShell role={role} icon="bell" tint="tint-blue" title="Notifications" sub="Activity and updates across your appointments."
      right={counts.unread > 0 && <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => setItems(items.map(i => ({ ...i, unread: false })))}><BI name="check" size={14} />Mark all read</button>} onToast={onToast}>
      <div className="b-notif">
        <nav className="b-rail">
          {rail.map(([k, lbl, ic]) => (
            <button key={k} className="b-railitem" data-on={filter === k} onClick={() => setFilter(k)}>
              <span className="i"><BI name={ic} size={16} /></span>{lbl}<span className="cnt">{counts[k]}</span>
            </button>
          ))}
        </nav>
        <div className="b-feed">
          {groups.length === 0 ? (
            <div className="b-emptyfeed"><span className="i"><BI name="inbox" size={40} /></span><b>You’re all caught up</b><span>No notifications match this filter.</span></div>
          ) : groups.map(([label, arr]) => (
            <React.Fragment key={label}>
              <div className="b-datelabel">{label}</div>
              {arr.map(n => (
                <div key={n.id} className={'b-nitem' + (n.unread ? ' unread' : '')} onClick={() => { setItems(items.map(x => x.id === n.id ? { ...x, unread: false } : x)); onToast('Opening appointment ' + n.conf + '…'); }}>
                  <span className={'ic ' + n.tone}><BI name={CAT_META[n.cat].icon} size={17} /></span>
                  <div className="tx">
                    <b>{n.title}</b>
                    <p>{n.body}</p>
                    <div className="foot"><span>Appointment <span className="conf">{n.conf}</span></span><span>{n.time}</span></div>
                  </div>
                  <span className="go"><BI name="chevRight" size={18} /></span>
                  {n.unread && <span className="dot" />}
                </div>
              ))}
            </React.Fragment>
          ))}
        </div>
      </div>
    </BShell>
  );
}

/* ---------------- My Documents ---------------- */
function DocumentsPage({ role, onToast }) {
  const data = useMemoB(() => docsFor(role), [role.key]);
  const [q, setQ] = useStateB('');
  const [status, setStatus] = useStateB('all');
  const [open, setOpen] = useStateB(() => ({ 'PQ-24817': true }));
  const np = !role.isNonPatient;

  const allReq = data.flatMap(a => a.required.map(r => ({ ...r, conf: a.conf })));
  const missing = allReq.filter(r => !r.received);

  function filterDocs(docs) {
    return docs.filter(d => {
      if (status !== 'all' && d.status !== status) return false;
      const t = q.trim().toLowerCase();
      if (t && !(d.name.toLowerCase().includes(t) || d.type.toLowerCase().includes(t))) return false;
      return true;
    });
  }
  const badge = s => s === 'accepted' ? 'Accepted' : s === 'pending' ? 'Pending review' : 'Rejected';

  return (
    <BShell role={role} icon="doc" tint="tint-blue" title="My documents" sub="Every document across your appointments, in one place." onToast={onToast}>
      {missing.length > 0 && (
        <div className="ad-reqbox warn" style={{ marginBottom: 18 }}>
          <div className="ad-reqbox__h"><BI name="alert" size={15} />{missing.length} required document{missing.length === 1 ? '' : 's'} outstanding across your appointments</div>
          <div className="ad-reqchips">
            {missing.map((r, i) => <span key={i} className="ad-reqchip miss"><BI name="doc" size={12} />{r.name} · {r.conf}</span>)}
          </div>
        </div>
      )}

      <div className="b-docfilters">
        <div className="af-search">
          <BI name="search" size={16} />
          <input placeholder="Search documents by name or type…" value={q} onChange={e => setQ(e.target.value)} />
        </div>
        <select value={status} onChange={e => setStatus(e.target.value)}>
          <option value="all">All statuses</option><option value="accepted">Accepted</option><option value="pending">Pending review</option><option value="rejected">Rejected</option>
        </select>
      </div>

      {data.map(apt => {
        const docs = filterDocs(apt.docs);
        const isOpen = open[apt.conf];
        return (
          <div className="b-apt" key={apt.conf}>
            <div className="b-apt__head" data-open={isOpen} onClick={() => setOpen({ ...open, [apt.conf]: !isOpen })}>
              <span className="ic"><BI name="calendar" size={17} /></span>
              <div className="tx"><b>{apt.type} · {apt.conf}</b><span>{(apt.patient ? apt.patient + ' · ' : '') + apt.date}</span></div>
              <span className="b-apt__count">{docs.length} doc{docs.length === 1 ? '' : 's'}</span>
              <span className="chev"><BI name="chevRight" size={18} /></span>
            </div>
            {isOpen && (
              <div className="b-apt__body">
                {docs.length === 0 ? <div className="ad-empty">No documents match this filter.</div> : docs.map(doc => (
                  <div className="ad-doc" key={doc.id} style={{ marginTop: 12 }}>
                    <span className="fi"><BI name="doc" size={18} /></span>
                    <div className="meta">
                      <div className="nm"><b>{doc.name}</b>
                        <span className={'ad-docbadge ' + doc.status}><span className="d" />{badge(doc.status)}</span>
                        <span className={'ad-typebadge' + (doc.strike ? ' strike' : '')}>{doc.type}</span>
                      </div>
                      <div className="sub">{doc.file} · {doc.size} · {doc.when}</div>
                      {doc.status === 'rejected' && doc.rejection && <div className="rej"><BI name="alert" size={13} /><span><b>Reason:</b> {doc.rejection}</span></div>}
                    </div>
                    <div className="acts"><button className="ra-rowbtn" title="Download" onClick={() => onToast('Downloading ' + doc.file + '…')}><BI name="arrowDown" size={14} /></button></div>
                  </div>
                ))}
                <div style={{ marginTop: 14 }}>
                  <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => onToast('Opening ' + apt.conf + ' to upload…')}><BI name="arrowUp" size={14} />Upload to this appointment</button>
                </div>
              </div>
            )}
          </div>
        );
      })}
    </BShell>
  );
}

/* ---------------- Notification preferences ---------------- */
function PreferencesPage({ role, onToast }) {
  const [prefs, setPrefs] = useStateB({ approvals: true, changes: true, documents: true, consent: true, reminders: false });
  return (
    <BShell role={role} icon="settings" tint="tint-slate" title="Notification preferences" sub="Choose which email notifications you receive." onToast={onToast}>
      <div className="b-prefcard">
        {PREF_CATS.map(c => (
          <div className="b-prefrow" key={c.key}>
            <span className={'ic ' + c.tint}><BI name={c.icon} size={18} /></span>
            <div className="tx"><b>{c.title}</b><span>{c.desc}</span></div>
            <label className="ra-switch">
              <input type="checkbox" checked={prefs[c.key]} onChange={e => { setPrefs({ ...prefs, [c.key]: e.target.checked }); onToast('Preferences updated.'); }} />
              <span className="track" />
            </label>
          </div>
        ))}
      </div>
      <div className="b-prefnote"><BI name="alert" size={14} />Notifications are sent by email to <b style={{ marginLeft: 4 }}>{role.email}</b>. Critical approvals are always sent.</div>
    </BShell>
  );
}

window.BPages = { NotificationsPage, DocumentsPage, PreferencesPage };
