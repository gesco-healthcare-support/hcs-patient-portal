/* global React */
/* ============================================================
   Send back / Request more information — three views:
   1) StaffSendBack — internal detail banner + "Request info" modal
      (field-picker tree grouped by form section, per-field hints,
      overall note, email preview note).
   2) ExternalFix — the focused fix-it page externals open from the
      email: staff note, flagged fields editable (red), everything
      else read-only, progress bar, Resubmit with confirm.
   3) StaffResubmitted — Pending + Resubmitted badge, what-changed
      diff vs. the send-back, note history.
   ============================================================ */
const { useState: useStateSB, useMemo: useMemoSB } = React;
function SBI({ name, size }) { return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size || 18)} />; }

/* fields staff can flag, grouped by booking-form section */
const SB_TREE = [
  ['Schedule', [['panelNumber', 'Panel number'], ['appointmentDate', 'Appointment date'], ['appointmentTime', 'Appointment time']]],
  ['Patient', [['dob', 'Date of birth'], ['ssn', 'Social Security #'], ['address', 'Address'], ['cell', 'Cell phone'], ['language', 'Appointment language']]],
  ['Attorneys', [['aaEmail', 'Applicant attorney email'], ['daFirm', 'Defense attorney firm']]],
  ['Insurance', [['insName', 'Insurance company'], ['insPhone', 'Insurance phone']]],
  ['Examiner', [['ceEmail', 'Claim examiner email']]],
  ['Claim', [['claim1', 'Claim WC24-10480 (Lower back, Right shoulder)'], ['claim2', 'Claim WC24-10533 (Neck, Both wrists)']]],
  ['Documents', [['docStrike', 'Panel Strike List — replace document'], ['docCover', 'Cover Letter — upload document']]],
];
const SB_LABEL = {}; SB_TREE.forEach(([g, f]) => f.forEach(([k, l]) => SB_LABEL[k] = l));

/* the demo scenario: what staff flagged + the note */
const SB_SCENARIO = {
  flagged: ['panelNumber', 'dob', 'docStrike'],
  hints: { panelNumber: 'Doesn\u2019t match the QME panel on file \u2014 should start with P-22', dob: 'DOB doesn\u2019t match the claim record', docStrike: 'The uploaded strike list is illegible \u2014 please re-scan' },
  note: 'We can\u2019t verify this request against the panel paperwork. Please correct the panel number and date of birth, and replace the panel strike list with a legible copy. Reply through the portal \u2014 don\u2019t email documents.',
  by: 'Sandra Cole', when: 'Jun 11, 2026 · 2:10 PM',
};

/* ---------------- shared bits ---------------- */
function ExtTopbar({ onToast }) {
  return (
    <header className="ext-nav">
      <div className="ext-nav__in">
        <a className="ext-brand" href="#" onClick={e => e.preventDefault()} aria-label="Home">
          <img src="assets/header-logo.png" alt="Clinic" />
          <span className="ext-brand__div" />
          <span className="ext-brand__tag"><b>Appointment Portal</b><span>Patient &amp; case portal</span></span>
        </a>
        <div className="ext-nav__spacer" />
        <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => onToast('Returning to home…')}><SBI name="chevLeft" size={15} />Back to home</button>
      </div>
    </header>
  );
}
function KV({ k, v, mono, full }) {
  return (<div className={'ad-field sb-locked' + (full ? ' full' : '')}><span className="k">{k}</span><span className={'v' + (mono ? ' mono' : '')}>{v || '—'}</span></div>);
}

/* ---------------- 1 · Staff: send back ---------------- */
function StaffSendBack({ onToast, onSent }) {
  const [open, setOpen] = useStateSB(false);
  const [sel, setSel] = useStateSB({});
  const [hints, setHints] = useStateSB({});
  const [note, setNote] = useStateSB('');
  const count = Object.keys(sel).filter(k => sel[k]).length;

  function toggle(k) { setSel(s => ({ ...s, [k]: !s[k] })); }
  function send() {
    setOpen(false);
    onToast('Sent back to the requester — email queued with your note.');
    onSent && onSent({ sel, hints, note });
  }

  return (
    <div className="ad" style={{ margin: '-26px -28px -60px', minHeight: 'auto' }}>
      <div className="ad-banner ad-banner--pending">
        <div className="ad-banner__in">
          <div className="ad-banner__top">
            <div style={{ minWidth: 0 }}>
              <div className="ad-banner__crumb"><a href="#" onClick={e => e.preventDefault()}>Appointments</a><SBI name="chevRight" size={12} />PQ-24817</div>
              <h1><span className="ttl">Panel QME <span style={{ fontWeight: 500, opacity: .85 }}>· Maria Gonzalez</span></span><span className="ad-statepill"><span className="d" />Pending review</span></h1>
              <div className="ad-banner__meta">
                <div className="it"><div className="k">Confirmation</div><div className="v mono">PQ-24817</div></div>
                <div className="it"><div className="k">Requested</div><div className="v">Jun 9, 2026</div></div>
                <div className="it"><div className="k">Decide by</div><div className="v mono">Jun 12 · 1d left</div></div>
              </div>
            </div>
          </div>
          <div className="ad-actions" style={{ marginTop: 16 }}>
            <button className="af-btn af-btn--green" onClick={() => onToast('Approve modal…')}><SBI name="check" size={15} />Approve</button>
            <button className="af-btn af-btn--glass" onClick={() => onToast('Reject modal…')}><SBI name="x" size={15} />Reject</button>
            <button className="af-btn af-btn--glass" style={{ borderColor: 'rgba(255,255,255,.5)' }} onClick={() => setOpen(true)}><SBI name="help" size={15} />Request info</button>
            <button className="af-btn af-btn--glass" onClick={() => onToast('Reschedule…')}><SBI name="refresh" size={15} />Reschedule</button>
            <button className="af-btn af-btn--glass" onClick={() => onToast('Cancel…')}><SBI name="x" size={15} />Cancel</button>
          </div>
        </div>
      </div>

      <div className="ad-wrap">
        <div className="ra-note" style={{ marginBottom: 18 }}><span className="i"><SBI name="help" size={15} /></span><span><b>Request info</b> sends the form back to the requester: pick the fields that need fixing, add a note, and the appointment moves to <b>Info Requested</b> until they resubmit. Any internal role can do this.</span></div>
        <section className="ad-card"><div className="ad-card__head"><span className="ic tint-blue"><SBI name="calendar" size={18} /></span><h3>Appointment details</h3></div>
          <div className="ad-card__body"><div className="ad-dl">
            <KV k="Appointment type" v="Panel QME" /><KV k="Panel number" v="P-1104" mono />
            <KV k="Location" v="Los Angeles — Wilshire" /><KV k="Date & time" v="Jun 16, 2026 · 9:30 AM" />
          </div></div>
        </section>
        <section className="ad-card"><div className="ad-card__head"><span className="ic tint-blue"><SBI name="user" size={18} /></span><h3>Patient demographics</h3></div>
          <div className="ad-card__body"><div className="ad-dl">
            <KV k="Name" v="Maria Gonzalez" /><KV k="Date of birth" v="03/22/1985" mono />
            <KV k="Email" v="mgonzalez@aol.com" /><KV k="Cell" v="(213) 555-0148" mono />
          </div></div>
        </section>
      </div>

      {open && (
        <div className="ra-scrim" onClick={() => setOpen(false)}>
          <div className="ra-modal ra-modal--lg" onClick={e => e.stopPropagation()}>
            <div className="ra-modal__head">
              <span className="ic tint-purple" style={{ width: 40, height: 40, borderRadius: 11, display: 'flex', alignItems: 'center', justifyContent: 'center' }}><SBI name="help" size={19} /></span>
              <h3>Request more information</h3>
              <button className="ext-iconbtn x" onClick={() => setOpen(false)}><SBI name="x" size={17} /></button>
            </div>
            <div className="ra-modal__body">
              <div className="ra-field col-12" style={{ marginBottom: 14 }}>
                <label>Fields that need to be fixed <span className="req">*</span></label>
                <div className="sb-tree">
                  {SB_TREE.map(([group, fields]) => {
                    const gCount = fields.filter(([k]) => sel[k]).length;
                    return (
                      <div className="sb-tree__group" key={group}>
                        <div className="sb-tree__ghead">{group}{gCount > 0 && <span className="cnt">{gCount}</span>}</div>
                        {fields.map(([k, lbl]) => (
                          <div className="sb-tree__row" key={k}>
                            <label className="main"><input type="checkbox" checked={!!sel[k]} onChange={() => toggle(k)} /><b>{lbl}</b></label>
                            {sel[k] && <div className="hint-in"><input placeholder="Optional hint shown next to this field (e.g. \u201cdoesn\u2019t match the panel on file\u201d)" maxLength={150} value={hints[k] || ''} onChange={e => setHints(h => ({ ...h, [k]: e.target.value }))} /></div>}
                          </div>
                        ))}
                      </div>
                    );
                  })}
                </div>
              </div>
              {count > 0 && (
                <div className="ra-field col-12" style={{ marginBottom: 14 }}>
                  <label>Selected ({count})</label>
                  <div className="sb-chips">{Object.keys(sel).filter(k => sel[k]).map(k => <span className="sb-chip" key={k}>{SB_LABEL[k]}<button onClick={() => toggle(k)}><SBI name="x" size={12} /></button></span>)}</div>
                </div>
              )}
              <div className="ra-field col-12">
                <label>Note to the requester <span className="req">*</span></label>
                <textarea className="ra-input" rows={4} maxLength={500} value={note} onChange={e => setNote(e.target.value)} placeholder="Explain what's needed and why — this goes in the email and on their fix-it page." />
                <div className="ra-hint" style={{ textAlign: 'right' }}>{note.length}/500</div>
              </div>
              <div className="ra-note" style={{ marginTop: 4 }}><span className="i"><SBI name="bell" size={15} /></span><span>The requester gets an <b>email with your note and a direct link</b>; the appointment shows as <b>Info Requested</b> until they resubmit. A reminder goes out if they don't respond.</span></div>
            </div>
            <div className="ra-modal__foot">
              <button className="af-btn af-btn--ghost" onClick={() => setOpen(false)}>Cancel</button>
              <button className="af-btn af-btn--primary" disabled={count === 0 || !note.trim()} style={count === 0 || !note.trim() ? { opacity: .5, cursor: 'not-allowed' } : null} onClick={send}><SBI name="arrowUp" size={15} />Send back ({count} field{count === 1 ? '' : 's'})</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

/* ---------------- 2 · External: fix-it page ---------------- */
function ExternalFix({ onToast, onResubmit }) {
  const S = SB_SCENARIO;
  const [vals, setVals] = useStateSB({ panelNumber: 'P-1104', dob: '1985-03-22' });
  const [fixedDoc, setFixedDoc] = useStateSB(false);
  const [touched, setTouched] = useStateSB({});
  const [confirm, setConfirm] = useStateSB(false);
  const fixedCount = (touched.panelNumber ? 1 : 0) + (touched.dob ? 1 : 0) + (fixedDoc ? 1 : 0);
  const total = S.flagged.length;
  function set(k, v) { setVals(s => ({ ...s, [k]: v })); setTouched(t => ({ ...t, [k]: true })); }

  return (
    <div className="ad" style={{ minHeight: 'auto' }}>
      <ExtTopbar onToast={onToast} />
      <div className="ad-banner" style={{ background: 'linear-gradient(135deg, #3b2a5e, #54398a 62%, #6b4aa8)' }}>
        <div className="ad-banner__in">
          <div className="ad-banner__crumb"><a href="#" onClick={e => { e.preventDefault(); onToast('Back to my appointments…'); }}>My appointments</a><SBI name="chevRight" size={12} />PQ-24817</div>
          <h1><span className="ttl">Panel QME <span style={{ fontWeight: 500, opacity: .85 }}>· Maria Gonzalez</span></span><span className="ad-statepill"><span className="d" />Info requested</span></h1>
          <div className="ad-callout" style={{ marginTop: 16 }}>
            <span className="ic"><SBI name="help" size={18} /></span>
            <div><b>The clinic needs {total} thing{total === 1 ? '' : 's'} fixed before they can review your request</b><span>Only the highlighted fields below can be changed — everything else is locked.</span></div>
          </div>
        </div>
      </div>

      <div className="ad-wrap" style={{ maxWidth: 880 }}>
        <div className="sb-note">
          <span className="ic"><SBI name="help" size={18} /></span>
          <div>
            <b>Note from the clinic</b>
            <p>{S.note}</p>
            <div className="meta">{S.by} (Staff) · {S.when}</div>
          </div>
        </div>

        <div className="ad-card" style={{ padding: '13px 20px', marginBottom: 18 }}>
          <div className="sb-progress">
            <SBI name="check" size={15} />{fixedCount} of {total} fixed
            <div className="bar"><span style={{ width: (fixedCount / total * 100) + '%' }} /></div>
          </div>
        </div>

        <section className="ad-card">
          <div className="ad-card__head"><span className="ic tint-blue"><SBI name="calendar" size={18} /></span><h3>Appointment details</h3></div>
          <div className="ad-card__body">
            <div className="ra-grid">
              <div className="ra-field col-4">
                <label>Panel number <span className="req">*</span></label>
                <input className={'ra-input' + (touched.panelNumber ? '' : ' sb-flag')} value={vals.panelNumber} onChange={e => set('panelNumber', e.target.value)} />
                {!touched.panelNumber && <div className="sb-flaghint"><SBI name="alert" size={13} />{S.hints.panelNumber}</div>}
              </div>
            </div>
            <div className="ad-dl" style={{ marginTop: 10 }}>
              <KV k="Appointment type" v="Panel QME" /><KV k="Location" v="Los Angeles — Wilshire" />
              <KV k="Date & time" v="Jun 16, 2026 · 9:30 AM" /><KV k="Confirmation #" v="PQ-24817" mono />
            </div>
          </div>
        </section>

        <section className="ad-card">
          <div className="ad-card__head"><span className="ic tint-blue"><SBI name="user" size={18} /></span><h3>Patient demographics</h3></div>
          <div className="ad-card__body">
            <div className="ra-grid">
              <div className="ra-field col-4">
                <label>Date of birth <span className="req">*</span></label>
                <input className={'ra-input' + (touched.dob ? '' : ' sb-flag')} type="date" value={vals.dob} onChange={e => set('dob', e.target.value)} />
                {!touched.dob && <div className="sb-flaghint"><SBI name="alert" size={13} />{S.hints.dob}</div>}
              </div>
            </div>
            <div className="ad-dl" style={{ marginTop: 10 }}>
              <KV k="Name" v="Maria Gonzalez" /><KV k="Email" v="mgonzalez@aol.com" />
              <KV k="Cell" v="(213) 555-0148" mono /><KV k="Address" v="128 W 4th St, Apt 5, Los Angeles, CA 90013" full />
            </div>
          </div>
        </section>

        <section className="ad-card">
          <div className="ad-card__head"><span className="ic tint-purple"><SBI name="doc" size={18} /></span><h3>Documents</h3></div>
          <div className="ad-card__body">
            <div className="ad-doc" style={!fixedDoc ? { borderColor: 'var(--st-rejected-dot)', background: '#fff8f8' } : null}>
              <span className="fi"><SBI name="doc" size={18} /></span>
              <div className="meta">
                <div className="nm"><b>{fixedDoc ? 'panel_strike_list_rescan.pdf' : 'Panel strike list'}</b><span className={'ad-docbadge ' + (fixedDoc ? 'pending' : 'rejected')}><span className="d" />{fixedDoc ? 'Pending review' : 'Replace requested'}</span><span className="ad-typebadge strike">Panel Strike List</span></div>
                {!fixedDoc && <div className="rej"><SBI name="alert" size={13} /><span>{S.hints.docStrike}</span></div>}
              </div>
              <div className="acts">
                <button className="af-btn af-btn--primary af-btn--sm" onClick={() => { setFixedDoc(true); onToast('Replacement uploaded.'); }}><SBI name="arrowUp" size={14} />{fixedDoc ? 'Replace again' : 'Upload replacement'}</button>
              </div>
            </div>
            <div className="ad-doc">
              <span className="fi"><SBI name="doc" size={18} /></span>
              <div className="meta"><div className="nm"><b>Medical records 2025</b><span className="ad-docbadge accepted"><span className="d" />Accepted</span><span className="ad-typebadge">Medical Records</span></div></div>
            </div>
          </div>
        </section>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 4 }}>
          <button className="af-btn af-btn--ghost" onClick={() => onToast('Saved — finish anytime from your home page.')}>Save &amp; finish later</button>
          <button className="af-btn af-btn--green af-btn--lg" disabled={fixedCount < total} style={fixedCount < total ? { opacity: .5, cursor: 'not-allowed' } : null} onClick={() => setConfirm(true)}><SBI name="check" size={16} />Resubmit to clinic</button>
        </div>
      </div>

      {confirm && (
        <div className="ra-scrim" onClick={() => setConfirm(false)}>
          <div className="ra-modal ra-modal--md" onClick={e => e.stopPropagation()}>
            <div className="ra-modal__head"><h3>Resubmit this request?</h3><button className="ext-iconbtn x" onClick={() => setConfirm(false)}><SBI name="x" size={17} /></button></div>
            <div className="ra-modal__body"><p style={{ margin: 0, fontSize: 14, color: 'var(--n-600)', lineHeight: 1.55 }}>Your corrections go back to the clinic for review and the request returns to <b>Pending</b>. You won't be able to edit again unless they ask.</p></div>
            <div className="ra-modal__foot">
              <button className="af-btn af-btn--ghost" onClick={() => setConfirm(false)}>Keep editing</button>
              <button className="af-btn af-btn--green" onClick={() => { setConfirm(false); onToast('Resubmitted — the clinic has been notified.'); onResubmit && onResubmit(); }}><SBI name="check" size={15} />Resubmit</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

/* ---------------- 3 · Staff: resubmitted review ---------------- */
function StaffResubmitted({ onToast }) {
  const S = SB_SCENARIO;
  return (
    <div className="ad" style={{ margin: '-26px -28px -60px', minHeight: 'auto' }}>
      <div className="ad-banner ad-banner--pending">
        <div className="ad-banner__in">
          <div className="ad-banner__crumb"><a href="#" onClick={e => e.preventDefault()}>Appointments</a><SBI name="chevRight" size={12} />PQ-24817</div>
          <h1><span className="ttl">Panel QME <span style={{ fontWeight: 500, opacity: .85 }}>· Maria Gonzalez</span></span><span className="ad-statepill"><span className="d" />Pending review</span><span className="sb-resub"><SBI name="check" size={12} />Resubmitted</span></h1>
          <div className="ad-banner__meta">
            <div className="it"><div className="k">Resubmitted</div><div className="v">Jun 12, 2026 · 9:05 AM</div></div>
            <div className="it"><div className="k">Round</div><div className="v mono">1</div></div>
            <div className="it"><div className="k">Decide by</div><div className="v mono">Jun 12 · today</div></div>
          </div>
          <div className="ad-actions" style={{ marginTop: 16 }}>
            <button className="af-btn af-btn--green" onClick={() => onToast('Approve modal…')}><SBI name="check" size={15} />Approve</button>
            <button className="af-btn af-btn--glass" onClick={() => onToast('Reject modal…')}><SBI name="x" size={15} />Reject</button>
            <button className="af-btn af-btn--glass" onClick={() => onToast('Request info modal…')}><SBI name="help" size={15} />Request info</button>
          </div>
        </div>
      </div>

      <div className="ad-wrap">
        <section className="ad-card" style={{ borderColor: 'var(--green-100)' }}>
          <div className="ad-card__head"><span className="ic tint-green"><SBI name="check" size={18} /></span><h3>What changed since your request</h3></div>
          <div className="ad-card__body">
            <div className="sb-diff"><span className="f">Panel number</span><span className="old">P-1104</span><span className="arr"><SBI name="chevRight" size={13} /></span><span className="new">P-2204</span></div>
            <div className="sb-diff"><span className="f">Date of birth</span><span className="old">03/22/1985</span><span className="arr"><SBI name="chevRight" size={13} /></span><span className="new">03/22/1984</span></div>
            <div className="sb-diff"><span className="f">Panel Strike List</span><span className="old">panel_strike_list.pdf (illegible)</span><span className="arr"><SBI name="chevRight" size={13} /></span><span className="new">panel_strike_list_rescan.pdf</span></div>
          </div>
        </section>

        <section className="ad-card">
          <div className="ad-card__head"><span className="ic tint-purple"><SBI name="help" size={18} /></span><h3>Request history</h3></div>
          <div className="ad-card__body" style={{ padding: 0 }}>
            <div className="clg-entry">
              <div className="clg-head" style={{ cursor: 'default' }}>
                <span className="ic tint-green"><SBI name="check" size={16} /></span>
                <span className="tx"><span className="t1"><b>Resubmitted by Maria Gonzalez</b></span><span className="t2">Jun 12, 2026 · 9:05 AM · 3 of 3 flagged items fixed</span></span>
              </div>
            </div>
            <div className="clg-entry">
              <div className="clg-head" style={{ cursor: 'default' }}>
                <span className="ic tint-purple"><SBI name="help" size={16} /></span>
                <span className="tx"><span className="t1"><b>Info requested by {S.by}</b></span><span className="t2">{S.when} · 3 fields flagged · “{S.note.slice(0, 64)}…”</span></span>
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  );
}

window.SbFeature = { StaffSendBack, ExternalFix, StaffResubmitted };
