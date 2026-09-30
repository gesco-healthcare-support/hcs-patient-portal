/* global React */
/* ============================================================
   Public (logged-out) pages — Document Upload + Change-Request Consent.
   Each renders all states; the harness sets the initial state. Reassuring,
   minimal, one clear action. No app shell.
   ============================================================ */
const { useState: useStatePP, useEffect: useEffectPP } = React;
function PPI({ name, size }) { return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size || 18)} />; }

function PPShell({ children }) {
  return (
    <div className="pp">
      <div className="pp-top">
        <img src="assets/header-logo.png" alt="Clinic logo" />
        <div className="tag"><b>Appointment Portal</b> · secure document portal</div>
      </div>
      <div className="pp-main">{children}</div>
      <div className="pp-foot">Need help? Contact the clinic at <a href="#" onClick={e => e.preventDefault()}>support@clinic.example</a> · Do not reply to the notification email.</div>
    </div>
  );
}

/* ---------------- Public Document Upload ---------------- */
function PublicDocumentUpload({ initial }) {
  const [st, setSt] = useStatePP(initial || 'ready');
  const [file, setFile] = useStatePP(null);
  useEffectPP(() => { setSt(initial || 'ready'); setFile(null); }, [initial]);

  const REQ = { docName: 'Panel Strike List', conf: 'PQ-24817', patient: 'Maria Gonzalez', requestedBy: 'Falkinstein Orthopedics', due: 'Jun 16, 2026' };

  function pick() { setFile({ name: 'panel_strike_list.pdf', size: '0.6 MB' }); }
  function doUpload() { setSt('uploading'); setTimeout(() => setSt('success'), 1400); }

  if (st === 'invalid') return (
    <PPShell><div className="pp-card">
      <div className="pp-ic red"><PPI name="alert" size={30} /></div>
      <h1>This link isn’t valid</h1>
      <p className="lead">This upload link is missing required information or has expired. Please open the most recent link from your email, or contact the clinic.</p>
    </div></PPShell>
  );
  if (st === 'ratelimited') return (
    <PPShell><div className="pp-card">
      <div className="pp-ic amber"><PPI name="clock" size={30} /></div>
      <h1>Too many attempts</h1>
      <p className="lead">Please wait a few minutes and try again from your email link.</p>
    </div></PPShell>
  );
  if (st === 'error') return (
    <PPShell><div className="pp-card">
      <div className="pp-ic red"><PPI name="alert" size={30} /></div>
      <h1>Something went wrong</h1>
      <p className="lead">We couldn’t complete the upload. Check your connection and try again.</p>
      <div className="pp-actions pp-actions--single"><button className="af-btn af-btn--primary pp-btn-lg" onClick={() => setSt('ready')}><PPI name="refresh" size={16} />Try again</button></div>
    </div></PPShell>
  );
  if (st === 'success') return (
    <PPShell><div className="pp-card">
      <div className="pp-ic green"><PPI name="check" size={32} /></div>
      <h1>Document received</h1>
      <p className="lead">Thank you. <b>{REQ.docName}</b> has been uploaded for appointment <b>{REQ.conf}</b> and is now awaiting review by the clinic. You can close this page.</p>
      <div className="pp-actions pp-actions--single"><button className="af-btn af-btn--ghost pp-btn-lg" onClick={() => { setFile(null); setSt('ready'); }}><PPI name="doc" size={16} />Upload another document</button></div>
    </div></PPShell>
  );

  const uploading = st === 'uploading';
  return (
    <PPShell><div className="pp-card">
      <div className="pp-ic blue"><PPI name="doc" size={30} /></div>
      <h1>Upload your document</h1>
      <p className="lead">{REQ.requestedBy} has requested a document for an appointment. Please upload it below — it’s sent securely and only the clinic can see it.</p>

      <div className="pp-context">
        <div className="pp-crow"><span className="ic"><PPI name="doc" size={16} /></span><span className="tx"><span className="k">Requested document</span><span className="v">{REQ.docName}</span></span></div>
        <div className="pp-crow"><span className="ic"><PPI name="calendar" size={16} /></span><span className="tx"><span className="k">Appointment</span><span className="v mono">{REQ.conf}</span></span></div>
        <div className="pp-crow"><span className="ic"><PPI name="user" size={16} /></span><span className="tx"><span className="k">Patient</span><span className="v">{REQ.patient}</span></span></div>
      </div>

      {uploading ? (
        <>
          <div style={{ fontSize: 13.5, fontWeight: 600, color: 'var(--n-700)', marginBottom: 4 }}>Uploading {file?.name}…</div>
          <div className="pp-progress"><span style={{ width: '66%' }} /></div>
        </>
      ) : !file ? (
        <div className="pp-drop" onClick={pick}>
          <div className="ic"><PPI name="doc" size={30} /></div>
          <b>Drag &amp; drop your file here, or click to browse</b>
          <span>PDF, JPG, or PNG · up to 10 MB</span>
        </div>
      ) : (
        <div className="pp-file">
          <span className="fi"><PPI name="doc" size={18} /></span>
          <span className="nm"><b>{file.name}</b><span>{file.size}</span></span>
          <button className="ra-rowbtn danger" onClick={() => setFile(null)} title="Remove"><PPI name="x" size={14} /></button>
        </div>
      )}

      <div className="pp-note"><span className="i"><PPI name="alert" size={15} /></span><span>Don’t include Social Security numbers, dates of birth, or other personal health information in the file name.</span></div>

      <div className="pp-actions pp-actions--single">
        <button className="af-btn af-btn--primary pp-btn-lg" disabled={!file || uploading} style={(!file || uploading) ? { opacity: .5, cursor: 'not-allowed' } : null} onClick={doUpload}>
          <PPI name="arrowUp" size={16} />{uploading ? 'Uploading…' : 'Upload document'}
        </button>
      </div>
    </div></PPShell>
  );
}

/* ---------------- Public Change-Request Consent ---------------- */
function PublicConsent({ initial }) {
  const [st, setSt] = useStatePP(initial || 'ready');
  useEffectPP(() => { setSt(initial || 'ready'); }, [initial]);

  const INFO = {
    type: 'Panel QME', conf: 'PQ-24817', current: 'Jun 16, 2026 · 9:30 AM',
    requestedNew: 'Jun 18, 2026 · 1:00 PM', requestedBy: 'Daniel Brooks (Applicant Attorney)',
    reason: 'The applicant has a conflicting medical appointment that morning and cannot attend.',
    isReschedule: true,
  };
  const actionWord = INFO.isReschedule ? 'reschedule' : 'cancellation';

  if (st === 'loading') return (
    <PPShell><div className="pp-card">
      <div className="pp-ic blue"><PPI name="clock" size={30} /></div>
      <h1>Loading request…</h1>
      <div className="pp-spin" />
    </div></PPShell>
  );
  if (st === 'error') return (
    <PPShell><div className="pp-card">
      <div className="pp-ic red"><PPI name="alert" size={30} /></div>
      <h1>This link isn’t valid</h1>
      <p className="lead">This consent link is invalid or has expired. If you still need to respond, contact the clinic.</p>
    </div></PPShell>
  );
  if (st === 'ratelimited') return (
    <PPShell><div className="pp-card">
      <div className="pp-ic amber"><PPI name="clock" size={30} /></div>
      <h1>Too many attempts</h1>
      <p className="lead">Please wait a while and try again from your email link.</p>
    </div></PPShell>
  );
  if (st === 'agreed') return (
    <PPShell><div className="pp-card">
      <div className="pp-ic green"><PPI name="check" size={32} /></div>
      <h1>Thank you — you agreed</h1>
      <p className="lead">You agreed to the {actionWord} of appointment <b>{INFO.conf}</b>. Our clinic staff will finalize it and notify both parties. You can close this page.</p>
    </div></PPShell>
  );
  if (st === 'declined') return (
    <PPShell><div className="pp-card">
      <div className="pp-ic red"><PPI name="x" size={30} /></div>
      <h1>You declined the request</h1>
      <p className="lead">You declined the {actionWord} of appointment <b>{INFO.conf}</b>. Our clinic staff has been notified and the appointment stays as scheduled.</p>
    </div></PPShell>
  );
  if (st === 'expired') return (
    <PPShell><div className="pp-card">
      <div className="pp-ic amber"><PPI name="clock" size={30} /></div>
      <h1>This link has expired</h1>
      <p className="lead">The response window has closed, so the request was referred to our clinic staff to handle directly.</p>
    </div></PPShell>
  );

  const submitting = st === 'submitting';
  return (
    <PPShell><div className="pp-card">
      <div className="pp-ic blue"><PPI name="refresh" size={28} /></div>
      <h1>Appointment {actionWord} request</h1>
      <p className="lead"><b>{INFO.requestedBy}</b> has requested to {actionWord} the appointment below. Because you’re a party to this claim, your agreement is needed before the clinic can finalize it.</p>

      <div className="pp-context">
        <div className="pp-crow"><span className="ic"><PPI name="calendar" size={16} /></span><span className="tx"><span className="k">Appointment</span><span className="v">{INFO.type} · <span className="mono">{INFO.conf}</span></span></span></div>
        <div className="pp-crow"><span className="ic"><PPI name="user" size={16} /></span><span className="tx"><span className="k">Requested by</span><span className="v">{INFO.requestedBy}</span></span></div>
      </div>

      {INFO.isReschedule && (
        <div className="pp-change">
          <div className="pp-change__slot old"><div className="k">Current</div><div className="v strike">{INFO.current}</div></div>
          <div className="pp-change__arrow"><PPI name="chevRight" size={20} /></div>
          <div className="pp-change__slot new"><div className="k">Requested</div><div className="v">{INFO.requestedNew}</div></div>
        </div>
      )}

      <div className="pp-context">
        <div className="pp-crow"><span className="ic"><PPI name="doc" size={16} /></span><span className="tx"><span className="k">Reason given</span><span className="v" style={{ fontWeight: 500, fontSize: 13.5 }}>{INFO.reason}</span></span></div>
      </div>

      <div className="pp-note" style={{ background: 'var(--blue-50)', borderColor: 'var(--blue-200)', color: 'var(--blue-800)' }}>
        <span className="i"><PPI name="alert" size={15} /></span>
        <span><b>Agreeing</b> lets the clinic finalize the {actionWord}. <b>Declining</b> keeps the appointment as scheduled and notifies staff. You can only respond once.</span>
      </div>

      <div className="pp-actions">
        <button className="af-btn af-btn--ghost pp-btn-lg" disabled={submitting} onClick={() => setSt('declined')}><PPI name="x" size={16} />No, I don’t agree</button>
        <button className="af-btn af-btn--green pp-btn-lg" disabled={submitting} onClick={() => { setSt('submitting'); setTimeout(() => setSt('agreed'), 900); }}><PPI name="check" size={16} />{submitting ? 'Submitting…' : 'Yes, I agree'}</button>
      </div>
    </div></PPShell>
  );
}

window.PublicDocumentUpload = PublicDocumentUpload;
window.PublicConsent = PublicConsent;
