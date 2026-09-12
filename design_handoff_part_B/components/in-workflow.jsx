/* global React */
/* ============================================================
   Group 1 — Change Requests inbox · Change Logs (global timeline) ·
   Reports. Renders inside InternalShell. Reuses ia-* table styles.
   ============================================================ */
const { useState: useStateW, useMemo: useMemoW } = React;
function WI({ name, size }) { return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size || 18)} />; }

/* ---------------- mock data ---------------- */
const CR_ITEMS = [
  { id: 'c1', kind: 'reschedule', conf: 'PQ-24817', patient: 'Maria Gonzalez', type: 'Panel QME', location: 'Los Angeles — Wilshire', requester: 'Daniel Brooks', requesterRole: 'Applicant Attorney', reason: 'The applicant has a conflicting medical appointment that morning and cannot attend.', requested: 'Jun 9, 2026 · 8:14 AM', days: 2, consent: 'agreed', cur: 'Jun 16, 2026 · 9:30 AM', next: 'Jun 18, 2026 · 1:00 PM' },
  { id: 'c2', kind: 'reschedule', conf: 'FU-24779', patient: 'Kevin Patel', type: 'QME Follow-up', location: 'Sacramento — Midtown', requester: 'Kevin Patel', requesterRole: 'Patient', reason: 'Out of town for a family emergency the week of the appointment.', requested: 'Jun 5, 2026 · 4:40 PM', days: 6, consent: 'pending', cur: 'Jun 22, 2026 · 11:00 AM', next: 'Jun 29, 2026 · 9:00 AM' },
  { id: 'c3', kind: 'cancel', conf: 'AM-24820', patient: 'David Chen', type: 'AME Evaluation', location: 'San Diego — Hillcrest', requester: 'Karen Whitfield', requesterRole: 'Claim Examiner', reason: 'Claim settled — evaluation no longer required.', requested: 'Jun 8, 2026 · 10:02 AM', days: 3, consent: 'agreed', cur: 'Jun 16, 2026 · 9:00 AM', next: null },
  { id: 'c4', kind: 'cancel', conf: 'RE-24788', patient: 'Robert Williams', type: 'QME Re-Evaluation', location: 'Fresno — Herndon', requester: 'Laura Pelton', requesterRole: 'Defense Attorney', reason: 'Parties stipulated to prior findings; re-evaluation withdrawn.', requested: 'Jun 1, 2026 · 1:25 PM', days: 10, consent: 'declined', cur: 'Jun 24, 2026 · 1:45 PM', next: null },
  { id: 'c5', kind: 'reschedule', conf: 'SR-24690', patient: 'Aisha Hassan', type: 'Supplemental Report', location: 'Sacramento — Midtown', requester: 'Daniel Brooks', requesterRole: 'Applicant Attorney', reason: 'Interpreter unavailable on the scheduled date.', requested: 'Jun 10, 2026 · 9:55 AM', days: 1, consent: 'pending', cur: 'Jun 19, 2026 · 8:30 AM', next: 'Jun 23, 2026 · 10:15 AM' },
];

const CLG_ENTRIES = [
  { id: 'l1', conf: 'PQ-24817', when: 'Jun 10, 2026 · 2:48 PM', who: 'Sandra Cole', role: 'Staff Supervisor', section: 'Appointment', kind: 'update', diffs: [['Status', 'Pending', 'Approved'], ['Approval comments', '—', 'Arrive 15 min early with photo ID.']] },
  { id: 'l2', conf: 'PQ-24817', when: 'Jun 9, 2026 · 11:21 AM', who: 'Maria Gonzalez', role: 'Patient', section: 'Patient', kind: 'update', diffs: [['Cell phone', '(213) 555-0102', '(213) 555-0148'], ['Street', '126 W 4th St', '128 W 4th St']] },
  { id: 'l3', conf: 'AM-24820', when: 'Jun 8, 2026 · 4:03 PM', who: 'Priya Shah', role: 'Intake Staff', section: 'Claim information', kind: 'add', diffs: [['Claim #', '—', 'WC24-10533'], ['ADJ #', '—', 'ADJ-4471980'], ['Body parts', '—', 'Neck, Both wrists']] },
  { id: 'l4', conf: 'FU-24779', when: 'Jun 7, 2026 · 9:12 AM', who: 'Sandra Cole', role: 'Staff Supervisor', section: 'Patient', kind: 'update', redacted: true, diffs: [['Social Security #', null, null]] },
  { id: 'l5', conf: 'RE-24788', when: 'Jun 5, 2026 · 3:30 PM', who: 'Laura Pelton', role: 'Defense Attorney', section: 'Documents', kind: 'add', diffs: [['Document', '—', 'prior_report.pdf (Correspondence)']] },
  { id: 'l6', conf: 'AM-24744', when: 'Jun 4, 2026 · 10:44 AM', who: 'Sandra Cole', role: 'Staff Supervisor', section: 'Appointment', kind: 'update', diffs: [['Status', 'Pending', 'Rejected'], ['Rejection reason', '—', 'Panel number does not match the QME panel on file.']] },
];

const RP_COLS = [
  ['conf', 'Confirmation #'], ['type', 'Type'], ['location', 'Location'], ['date', 'Appointment'],
  ['status', 'Status'], ['patient', 'Patient'], ['dob', 'Date of birth'], ['email', 'Email'], ['phone', 'Phone'],
];

/* ---------------- Change Requests inbox ---------------- */
function CrInbox({ onToast }) {
  const [tab, setTab] = useStateW('all');
  const [open, setOpen] = useStateW(null);
  const [modal, setModal] = useStateW(null); // {kind:'approve'|'reject', item}
  const [reason, setReason] = useStateW('');
  const [handled, setHandled] = useStateW({});

  const items = CR_ITEMS.filter(i => !handled[i.id]).filter(i => tab === 'all' ? true : i.kind === (tab === 'resched' ? 'reschedule' : 'cancel'));
  const counts = { all: CR_ITEMS.filter(i => !handled[i.id]).length, resched: CR_ITEMS.filter(i => !handled[i.id] && i.kind === 'reschedule').length, cancel: CR_ITEMS.filter(i => !handled[i.id] && i.kind === 'cancel').length };
  const ageCls = d => d >= 7 ? 'crit' : d >= 4 ? 'warn' : 'ok';
  const consentLbl = { pending: 'Consent pending', agreed: 'Consent received', declined: 'Consent declined' };

  function decide(it, ok) {
    setHandled(h => ({ ...h, [it.id]: true })); setModal(null); setReason('');
    onToast((it.kind === 'reschedule' ? 'Reschedule' : 'Cancellation') + ' request ' + (ok ? 'approved' : 'rejected') + ' — ' + it.conf);
  }

  return (
    <>
      <div className="ia-head">
        <div><h1>Change requests</h1><p>{counts.all} pending request{counts.all === 1 ? '' : 's'} awaiting review</p></div>
      </div>
      <div className="ia-chips">
        {[['all', 'All'], ['resched', 'Reschedules'], ['cancel', 'Cancellations']].map(([k, lbl]) => (
          <button key={k} className="ia-chip" data-on={tab === k} onClick={() => setTab(k)}>{lbl}<span className="cnt">{counts[k]}</span></button>
        ))}
      </div>

      {items.length === 0 && <div className="ia-empty" style={{ background: '#fff', border: '1px solid var(--border)', borderRadius: 'var(--r-md)' }}><WI name="inbox" size={36} /><b>Queue clear</b><span>No pending change requests.</span></div>}

      {items.map(it => (
        <div className="cr-row" key={it.id}>
          <div className="cr-row__main" data-open={open === it.id} onClick={() => setOpen(open === it.id ? null : it.id)}>
            <span className={'cr-row__type ' + (it.kind === 'reschedule' ? 'tint-amber' : 'tint-red')}><WI name={it.kind === 'reschedule' ? 'refresh' : 'x'} size={18} /></span>
            <div className="cr-row__tx">
              <div className="l1"><b>{it.kind === 'reschedule' ? 'Reschedule' : 'Cancellation'} · {it.patient}</b><span className="conf">{it.conf}</span></div>
              <div className="l2">{it.type} · {it.location} · requested by <b style={{ color: 'var(--n-700)' }}>{it.requester}</b> ({it.requesterRole}) · {it.requested}</div>
            </div>
            <div className="cr-row__meta">
              <span className={'cr-age ' + ageCls(it.days)}><WI name="clock" size={12} />{it.days}d waiting</span>
              <span className={'cr-consent ' + it.consent}><span className="d" />{consentLbl[it.consent]}</span>
            </div>
            <div className="cr-row__acts" onClick={e => e.stopPropagation()}>
              <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => onToast('Opening ' + it.conf + '…')}><WI name="eye" size={14} />View</button>
              <button className="af-btn af-btn--green af-btn--sm" onClick={() => setModal({ kind: 'approve', item: it })}><WI name="check" size={14} />Approve</button>
              <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => setModal({ kind: 'reject', item: it })}><WI name="x" size={14} />Reject</button>
            </div>
            <span className="cr-row__chev"><WI name="chevRight" size={17} /></span>
          </div>
          {open === it.id && (
            <div className="cr-detail">
              <div className="blk">
                <h4>{it.kind === 'reschedule' ? 'Requested change' : 'Appointment to cancel'}</h4>
                {it.kind === 'reschedule' ? (
                  <div className="cr-slots">
                    <div className="cr-slot"><div className="k">Current</div><div className="v strike">{it.cur}</div></div>
                    <span className="arr"><WI name="chevRight" size={18} /></span>
                    <div className="cr-slot new"><div className="k">Requested</div><div className="v">{it.next}</div></div>
                  </div>
                ) : (
                  <div className="cr-slots"><div className="cr-slot"><div className="k">Scheduled</div><div className="v">{it.cur}</div></div></div>
                )}
              </div>
              <div className="blk"><h4>Reason given</h4><p>{it.reason}</p></div>
            </div>
          )}
        </div>
      ))}

      {modal && modal.kind === 'approve' && (
        <WModal title={'Approve ' + (modal.item.kind === 'reschedule' ? 'reschedule' : 'cancellation')} submit="Approve" icon="check" green onClose={() => setModal(null)} onOk={() => decide(modal.item, true)}>
          <p style={{ margin: '0 0 14px', fontSize: 14, color: 'var(--n-600)', lineHeight: 1.5 }}>
            {modal.item.kind === 'reschedule'
              ? <>Move <b>{modal.item.conf}</b> ({modal.item.patient}) to the requested slot:</>
              : <>Cancel <b>{modal.item.conf}</b> ({modal.item.patient})? Both parties will be notified.</>}
          </p>
          {modal.item.kind === 'reschedule' && (
            <div className="cr-slots" style={{ marginBottom: 6 }}>
              <div className="cr-slot"><div className="k">Current</div><div className="v strike">{modal.item.cur}</div></div>
              <span className="arr"><WI name="chevRight" size={18} /></span>
              <div className="cr-slot new"><div className="k">New</div><div className="v">{modal.item.next}</div></div>
            </div>
          )}
          {modal.item.consent !== 'agreed' && (
            <div className="ra-note warn" style={{ marginTop: 10 }}><span className="i"><WI name="alert" size={15} /></span><span>Opposing-counsel consent is <b>{modal.item.consent === 'pending' ? 'still pending' : 'declined'}</b> — approving now overrides it.</span></div>
          )}
        </WModal>
      )}
      {modal && modal.kind === 'reject' && (
        <WModal title={'Reject ' + (modal.item.kind === 'reschedule' ? 'reschedule' : 'cancellation')} submit="Reject" icon="x" disabled={!reason.trim()} onClose={() => { setModal(null); setReason(''); }} onOk={() => decide(modal.item, false)}>
          <div className="ra-field col-12"><label>Rejection reason <span className="req">*</span></label>
            <textarea className="ra-input" rows={4} maxLength={500} value={reason} onChange={e => setReason(e.target.value)} placeholder="Explain why — the requester sees this message." />
            <div className="ra-hint" style={{ textAlign: 'right' }}>{reason.length}/500</div>
          </div>
        </WModal>
      )}
    </>
  );
}

/* ---------------- Change Logs (global, grouped timeline) ---------------- */
function ClgPage({ onToast }) {
  const [q, setQ] = useStateW('');
  const [section, setSection] = useStateW('');
  const [open, setOpen] = useStateW({ l1: true });
  const sections = [...new Set(CLG_ENTRIES.map(e => e.section))];
  const shown = CLG_ENTRIES.filter(e => {
    if (section && e.section !== section) return false;
    const t = q.trim().toLowerCase();
    if (t && !(e.conf.toLowerCase().includes(t) || e.who.toLowerCase().includes(t))) return false;
    return true;
  });
  const kindIc = { update: ['refresh', 'tint-blue'], add: ['plus', 'tint-green'], delete: ['x', 'tint-red'] };
  return (
    <>
      <div className="ia-head"><div><h1>Change logs</h1><p>Every change across appointments — one entry per save, expandable to field-level diffs.</p></div></div>
      <div className="ia-toolbar">
        <div className="ia-search"><WI name="search" size={16} /><input placeholder="Search confirmation # or user…" value={q} onChange={e => setQ(e.target.value)} /></div>
        <select className="ia-input" style={{ width: 200 }} value={section} onChange={e => setSection(e.target.value)}>
          <option value="">All sections</option>
          {sections.map(s => <option key={s}>{s}</option>)}
        </select>
      </div>
      <div className="clg">
        {shown.map(e => {
          const [ic, tint] = kindIc[e.kind] || kindIc.update;
          return (
            <div className="clg-entry" key={e.id}>
              <div className="clg-head" data-open={!!open[e.id]} onClick={() => setOpen({ ...open, [e.id]: !open[e.id] })}>
                <span className={'ic ' + tint}><WI name={ic} size={16} /></span>
                <span className="tx">
                  <span className="t1">
                    <b>{e.section} {e.kind === 'add' ? 'added' : 'updated'}</b>
                    <a className="conf" href="#" onClick={ev => { ev.preventDefault(); ev.stopPropagation(); onToast('Opening ' + e.conf + '…'); }}>{e.conf}</a>
                  </span>
                  <span className="t2">{e.when + ' · ' + e.who + ' (' + e.role + ')'}</span>
                </span>
                <span className="cnt">{e.diffs.length} field{e.diffs.length === 1 ? '' : 's'}</span>
                <span className="chev"><WI name="chevRight" size={16} /></span>
              </div>
              {open[e.id] && (
                <div className="clg-diffs">
                  {e.diffs.map((d, i) => (
                    <div className="clg-diff" key={i}>
                      <span className="f">{d[0]}</span>
                      {e.redacted ? <span className="red">updated (value hidden — sensitive field)</span> : (<>
                        <span className="old">{d[1] || '—'}</span><span className="arr"><WI name="chevRight" size={13} /></span><span className="new">{d[2] || '—'}</span>
                      </>)}
                    </div>
                  ))}
                </div>
              )}
            </div>
          );
        })}
        {shown.length === 0 && <div className="ia-empty"><WI name="inbox" size={36} /><b>No changes match</b></div>}
      </div>
    </>
  );
}

/* ---------------- Reports ---------------- */
function RpPage({ onToast }) {
  const rows0 = window.MOCK.appointments;
  const [q, setQ] = useStateW('');
  const [status, setStatus] = useStateW('');
  const [type, setType] = useStateW('');
  const [colsOpen, setColsOpen] = useStateW(false);
  const [cols, setCols] = useStateW(() => Object.fromEntries(RP_COLS.map(([k]) => [k, true])));

  const catOf = id => id === 2 ? 'Approved' : id === 3 ? 'Rejected' : (id === 5 || id === 6 || id === 13) ? 'Cancelled' : id === 12 ? 'Rescheduled' : 'Pending';
  const rows = rows0.filter(a => {
    if (status && catOf(a.statusId) !== status) return false;
    if (type && a.type !== type) return false;
    const t = q.trim().toLowerCase();
    if (t && !((a.patient.firstName + ' ' + a.patient.lastName).toLowerCase().includes(t) || a.confirmation.toLowerCase().includes(t) || (a.panelNumber || '').toLowerCase().includes(t))) return false;
    return true;
  });
  const statCounts = ['Pending', 'Approved', 'Rejected', 'Cancelled', 'Rescheduled'].map(s => [s, rows.filter(a => catOf(a.statusId) === s).length]);
  const swColor = { Pending: 'var(--st-pending-dot)', Approved: 'var(--green-500)', Rejected: 'var(--st-rejected-dot)', Cancelled: 'var(--st-rejected-dot)', Rescheduled: 'var(--blue-500)' };
  const fmtD = iso => new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });

  return (
    <>
      <div className="ia-head">
        <div><h1>Reports</h1><p>Appointment request report · {rows.length} result{rows.length === 1 ? '' : 's'}</p></div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="af-btn af-btn--ghost" onClick={() => onToast('Exporting CSV…')}><WI name="arrowDown" size={15} />CSV</button>
          <button className="af-btn af-btn--primary" onClick={() => onToast('Exporting PDF…')}><WI name="arrowDown" size={15} />Export PDF</button>
        </div>
      </div>

      <div className="ia-toolbar">
        <div className="ia-search"><WI name="search" size={16} /><input placeholder="Confirmation #, panel #, or patient name…" value={q} onChange={e => setQ(e.target.value)} /></div>
        <select className="ia-input" style={{ width: 170 }} value={type} onChange={e => setType(e.target.value)}><option value="">All types</option>{window.MOCK.TYPES.map(t => <option key={t}>{t}</option>)}</select>
        <select className="ia-input" style={{ width: 150 }} value={status} onChange={e => setStatus(e.target.value)}><option value="">All statuses</option>{['Pending', 'Approved', 'Rejected', 'Cancelled', 'Rescheduled'].map(s => <option key={s}>{s}</option>)}</select>
        <div className="rp-cols">
          <button className="ia-fbtn" aria-expanded={colsOpen} onClick={() => setColsOpen(!colsOpen)}><WI name="list" size={15} />Columns</button>
          {colsOpen && (<>
            <div className="ia-clickaway" onClick={() => setColsOpen(false)} />
            <div className="rp-colmenu">
              {RP_COLS.map(([k, lbl]) => (
                <label key={k}><input type="checkbox" checked={cols[k]} onChange={e => setCols({ ...cols, [k]: e.target.checked })} />{lbl}</label>
              ))}
            </div>
          </>)}
        </div>
      </div>

      <div className="rp-stats">
        {statCounts.map(([s, n]) => <div className="rp-stat" key={s}><span className="sw" style={{ background: swColor[s] }} /><span className="n">{n}</span><span className="l">{s}</span></div>)}
      </div>

      <div className="ia-wrap">
        <div className="ia-scroll">
          <table className="ia-table">
            <thead><tr>{RP_COLS.filter(([k]) => cols[k]).map(([k, lbl]) => <th key={k}>{lbl}</th>)}</tr></thead>
            <tbody>
              {rows.map(a => (
                <tr key={a.id} onClick={() => onToast('Opening ' + a.confirmation + '…')}>
                  {cols.conf && <td><span className="ia-conf">{a.confirmation}</span></td>}
                  {cols.type && <td>{a.type}</td>}
                  {cols.location && <td>{a.location}</td>}
                  {cols.date && <td>{fmtD(a.appointmentDate)}</td>}
                  {cols.status && <td><window.AfCommon.AfPill tone={a.status.tone} label={a.status.label} /></td>}
                  {cols.patient && <td><b style={{ color: 'var(--n-900)' }}>{a.patient.firstName} {a.patient.lastName}</b></td>}
                  {cols.dob && <td className="ia-sub" style={{ fontFamily: 'var(--font-num)' }}>{fmtD(a.dateOfInjury)}</td>}
                  {cols.email && <td className="ia-sub">{a.patient.email}</td>}
                  {cols.phone && <td className="ia-sub" style={{ fontFamily: 'var(--font-num)' }}>(213) 555-01{String(10 + (a.id.length % 80))}</td>}
                </tr>
              ))}
              {rows.length === 0 && <tr><td colSpan={RP_COLS.filter(([k]) => cols[k]).length}><div className="ia-empty"><WI name="inbox" size={36} /><b>No results</b></div></td></tr>}
            </tbody>
          </table>
        </div>
      </div>
    </>
  );
}

function WModal({ title, submit, icon, green, disabled, onClose, onOk, children }) {
  return (
    <div className="ra-scrim" onClick={onClose}>
      <div className="ra-modal ra-modal--md" onClick={e => e.stopPropagation()}>
        <div className="ra-modal__head"><h3>{title}</h3><button className="ext-iconbtn x" onClick={onClose}><WI name="x" size={17} /></button></div>
        <div className="ra-modal__body">{children}</div>
        <div className="ra-modal__foot"><button className="af-btn af-btn--ghost" onClick={onClose}>Cancel</button><button className={'af-btn ' + (green ? 'af-btn--green' : 'af-btn--primary')} disabled={disabled} style={disabled ? { opacity: .5, cursor: 'not-allowed' } : null} onClick={onOk}><WI name={icon} size={15} />{submit}</button></div>
      </div>
    </div>
  );
}

window.InWorkflow = { CrInbox, ClgPage, RpPage };
