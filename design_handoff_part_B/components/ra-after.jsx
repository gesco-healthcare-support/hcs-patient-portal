/* global React */
/* ============================================================
   Request-an-Appointment — AFTER wizard shell. Dynamic per-role steps
   (Claim Examiner skips the attorney steps), per-step validation gate,
   draft autosave, reval lookup, submit-confirmation modal, sticky footer.
   ============================================================ */
const { useState: useStateAfR, useEffect: useEffectAfR, useRef: useRefAfR, useMemo: useMemoAfR } = React;
const { RcIcon: RIA, RaStepper: Stepper } = window.RAC;

function buildSteps(booker) {
  return [
    { key: 'schedule', title: 'Schedule', sub: 'Type & slot' },
    { key: 'patient', title: 'Patient', sub: 'Demographics' },
    ...(booker.hideAttorneys ? [] : [
      { key: 'applicant', title: 'Applicant', sub: 'Attorney' },
      { key: 'defense', title: 'Defense', sub: 'Attorney' },
    ]),
    { key: 'insurance', title: 'Insurance', sub: 'Carrier' },
    { key: 'examiner', title: 'Examiner', sub: 'Adjuster' },
    { key: 'claim', title: 'Claim', sub: 'Injuries' },
    { key: 'docs', title: 'Docs', sub: 'Uploads' },
    { key: 'review', title: 'Review', sub: 'Confirm' },
  ];
}

function reqFields(key, f, booker, toggles, isPqme) {
  if (key === 'schedule') { const r = ['appointmentTypeId', 'locationId', 'appointmentDate', 'appointmentTime']; if (isPqme) r.push('panelNumber'); return r; }
  if (key === 'patient') return ['lastName', 'firstName', 'dateOfBirth', 'email', 'employerName', 'employerOccupation'];
  if (key === 'applicant') return (booker.lockAttorney === 'applicant' || toggles.applicant) ? ['applicantFirstName', 'applicantLastName', 'applicantEmail', 'applicantFirmName', 'applicantPhoneNumber', 'applicantStreet', 'applicantCity', 'applicantStateId', 'applicantZipCode'] : [];
  if (key === 'defense') return (booker.lockAttorney === 'defense' || toggles.defense) ? ['defenseFirstName', 'defenseLastName', 'defenseEmail', 'defenseFirmName', 'defensePhoneNumber', 'defenseStreet', 'defenseCity', 'defenseStateId', 'defenseZipCode'] : [];
  if (key === 'insurance') return toggles.insurance ? ['appointmentInsuranceName'] : [];
  if (key === 'examiner') return ['appointmentClaimExaminerName', 'appointmentClaimExaminerEmail', 'appointmentClaimExaminerPhoneNumber', 'appointmentClaimExaminerStreet', 'appointmentClaimExaminerCity', 'appointmentClaimExaminerStateId', 'appointmentClaimExaminerZip'];
  return [];
}

function AfterRequestAppointment({ bookerKey, mode, embedded }) {
  const booker = window.RA.BOOKERS[bookerKey];
  const storeKey = 'ra-draft-' + bookerKey + '-' + mode;
  const steps = useMemoAfR(() => buildSteps(booker), [bookerKey]);

  const initial = useMemoAfR(() => {
    const base = {};
    if (!booker.isNonPatient) { base.firstName = booker.first; base.lastName = booker.name.split(' ')[1] || ''; base.email = booker.email; }
    if (booker.prefillAttorney) Object.assign(base, booker.prefillAttorney);
    if (booker.prefillExaminer) Object.assign(base, booker.prefillExaminer);
    return base;
  }, [bookerKey]);

  // Hydrate any saved draft SYNCHRONOUSLY so the first render already shows it
  // (restoring in an effect was unreliable on initial page load).
  const draft0 = useMemoAfR(() => {
    try { const raw = localStorage.getItem(storeKey); return raw ? JSON.parse(raw) : null; } catch (e) { return null; }
  }, []);
  const [f, setF] = useStateAfR(() => (draft0 && draft0.f) || initial);
  const [errs, setErrs] = useStateAfR({});
  const [toggles, setToggles] = useStateAfR(() => (draft0 && draft0.toggles) || { applicant: true, defense: true, insurance: true });
  const [injuries, setInjuries] = useStateAfR(() => (draft0 && draft0.injuries) || []);
  const [docs, setDocs] = useStateAfR(() => (draft0 && draft0.docs) || []);
  const [hasStrike, setHasStrike] = useStateAfR(() => !!(draft0 && draft0.hasStrike));
  const [authUsers, setAuthUsers] = useStateAfR(() => (draft0 && draft0.authUsers) || []);
  const [step, setStep] = useStateAfR(() => draft0 ? Math.min(draft0.step || 0, steps.length - 1) : 0);
  const [furthest, setFurthest] = useStateAfR(() => draft0 ? Math.min(draft0.furthest || 0, steps.length - 1) : 0);
  const [errorSteps, setErrorSteps] = useStateAfR(new Set());
  const [saved, setSaved] = useStateAfR(false);
  const [toast, setToast] = useStateAfR(null);
  const [revalMsg, setRevalMsg] = useStateAfR('');
  const [submitOpen, setSubmitOpen] = useStateAfR(false);
  const [done, setDone] = useStateAfR(false);
  const revalRef = useRefAfR(null);
  const firstLoad = useRefAfR(true);

  const isPqme = window.RA.APPT_TYPES.find(t => t.id === f.appointmentTypeId)?.pqme;
  const curKey = steps[step]?.key;

  const mounted = useRefAfR(false);
  useEffectAfR(() => {
    // initial mount is hydrated synchronously above; this only handles
    // in-place booker/flow switches (harness keys usually remount instead).
    if (!mounted.current) { mounted.current = true; return; }
    try {
      const raw = localStorage.getItem(storeKey);
      if (raw) {
        const d = JSON.parse(raw);
        setF(d.f || initial); setToggles(d.toggles || { applicant: true, defense: true, insurance: true });
        setInjuries(d.injuries || []); setDocs(d.docs || []); setHasStrike(!!d.hasStrike); setAuthUsers(d.authUsers || []);
        setStep(Math.min(d.step || 0, steps.length - 1)); setFurthest(Math.min(d.furthest || 0, steps.length - 1));
      } else { setF(initial); setToggles({ applicant: true, defense: true, insurance: true }); }
    } catch (e) { setF(initial); }
    setErrs({}); setErrorSteps(new Set()); setDone(false); setSubmitOpen(false);
    firstLoad.current = true;
  }, [bookerKey, mode]);

  useEffectAfR(() => {
    if (firstLoad.current) { firstLoad.current = false; return; }
    const t = setTimeout(() => {
      try { localStorage.setItem(storeKey, JSON.stringify({ f, toggles, injuries, docs, hasStrike, authUsers, step, furthest })); } catch (e) {}
      setSaved(true); clearTimeout(window.__raSaved); window.__raSaved = setTimeout(() => setSaved(false), 1800);
    }, 500);
    return () => clearTimeout(t);
  }, [f, toggles, injuries, docs, hasStrike, authUsers, step, furthest]);

  const ctx = {
    f, errs,
    set: (name, val) => { setF(prev => ({ ...prev, [name]: val })); if (errs[name]) setErrs(e => { const n = { ...e }; delete n[name]; return n; }); },
    touch: () => {},
  };
  const setToggle = (k, v) => setToggles(t => ({ ...t, [k]: v }));

  function scrollToError() {
    setTimeout(() => {
      const bad = document.querySelector('.ra-input.bad, .ra-select.bad');
      if (!bad) return;
      let p = bad.parentElement;
      while (p && p !== document.body) {
        const oy = getComputedStyle(p).overflowY;
        if ((oy === 'auto' || oy === 'scroll') && p.scrollHeight > p.clientHeight) {
          p.scrollTop = Math.max(0, bad.getBoundingClientRect().top - p.getBoundingClientRect().top + p.scrollTop - 120); return;
        }
        p = p.parentElement;
      }
    }, 40);
  }

  function validateKey(key) {
    const e = {};
    reqFields(key, f, booker, toggles, isPqme).forEach(k => { if (!String(f[k] ?? '').trim()) e[k] = 'Required'; });
    setErrs(e);
    const claimBad = key === 'claim' && injuries.length === 0;
    const docBad = key === 'docs' && isPqme && hasStrike && !docs.some(d => d.typeId === 'd3');
    const ok = Object.keys(e).length === 0 && !claimBad && !docBad;
    const idx = steps.findIndex(s => s.key === key);
    setErrorSteps(prev => { const n = new Set(prev); if (ok) n.delete(idx); else n.add(idx); return n; });
    if (!ok && Object.keys(e).length) scrollToError();
    return ok;
  }

  function toastMsg(m) { setToast(m); clearTimeout(window.__raToast); window.__raToast = setTimeout(() => setToast(null), 3200); }
  function scrollTop() { document.querySelector('.viewport')?.scrollTo({ top: 0, behavior: 'smooth' }); }

  function goNext() {
    if (!validateKey(curKey)) {
      if (curKey === 'claim' && injuries.length === 0) toastMsg('Add at least one claim information entry to continue.');
      else if (curKey === 'docs') toastMsg('Label a document “Panel Strike List”, or uncheck the panel-strike-list box.');
      else toastMsg('Please complete the required fields highlighted below.');
      return;
    }
    const ni = Math.min(step + 1, steps.length - 1);
    setStep(ni); setFurthest(fu => Math.max(fu, ni)); scrollTop();
  }
  function goPrev() { setStep(s => Math.max(0, s - 1)); scrollTop(); }
  function jump(target) {
    const i = typeof target === 'number' ? target : steps.findIndex(s => s.key === target);
    if (i >= 0 && i <= furthest) { setStep(i); scrollTop(); }
  }

  function openSubmit() { setSubmitOpen(true); }
  function confirmSubmit() {
    for (const s of steps) {
      if (['schedule', 'patient', 'applicant', 'defense', 'insurance', 'examiner'].includes(s.key)) {
        if (!validateKey(s.key)) { setSubmitOpen(false); jump(steps.findIndex(x => x.key === s.key)); toastMsg('Please complete the required fields in ' + s.title + '.'); return; }
      }
    }
    if (injuries.length === 0) { setSubmitOpen(false); jump('claim'); validateKey('claim'); toastMsg('Add at least one claim information entry.'); return; }
    try { localStorage.removeItem(storeKey); } catch (e) {}
    setSubmitOpen(false); setDone(true);
  }

  function loadReval() {
    const v = (revalRef.current?.value || '').trim();
    setRevalMsg(v ? ('Loaded prior appointment ' + v.toUpperCase() + ' — fields below have been prefilled.') : 'Enter a prior confirmation number first.');
    if (v) {
      const a = window.MOCK.appointments[0];
      setF(prev => ({ ...prev, firstName: a.patient.firstName, lastName: a.patient.lastName, email: a.patient.email, appointmentTypeId: 't-reval', locationId: 'loc-0', employerName: 'Acme Logistics', employerOccupation: 'Warehouse Associate' }));
    }
  }

  const title = booker.internal
    ? (mode === 'reval' ? 'New re-evaluation' : 'New appointment')
    : (mode === 'reval' ? 'Request a Re-evaluation' : 'Request an Appointment');
  const eyebrow = booker.internal ? 'Booking on behalf of a patient' : (mode === 'reval' ? 'Follow-up evaluation' : 'New evaluation');
  const S1 = window.RaSteps1, S2 = window.RaSteps2;

  if (done) {
    return (
      <div className="ra">
        {!embedded && <RaTopbar booker={booker} onToast={toastMsg} />}
        <div className="ra-wrap" style={{ paddingTop: 60 }}>
          <div className="ra-card" style={{ maxWidth: 620, margin: '0 auto', textAlign: 'center' }}>
            <div className="ra-card__body" style={{ padding: '46px 30px' }}>
              <div style={{ width: 68, height: 68, borderRadius: '50%', background: 'var(--green-50)', color: 'var(--green-700)', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 18px' }}><RIA name="check" size={34} /></div>
              <h2 style={{ fontSize: 23, fontWeight: 800, color: 'var(--n-900)', margin: '0 0 8px' }}>{booker.internal ? 'Appointment created' : 'Request submitted'}</h2>
              <p style={{ color: 'var(--n-500)', fontSize: 14.5, margin: '0 0 22px', lineHeight: 1.55 }}>{booker.internal
                ? <>The {mode === 'reval' ? 're-evaluation' : 'appointment'} was created on the patient's behalf and is now in the appointments list as <b>Pending</b>. You can review, edit, or approve it there.</>
                : <>Your {mode === 'reval' ? 're-evaluation' : 'appointment'} request has been sent to the clinic. You'll get a confirmation by email. To make any changes, contact our staff.</>}</p>
              <button className="af-btn af-btn--primary af-btn--lg" onClick={() => toastMsg(booker.internal ? 'Opening appointments list…' : 'Returning to home…')}><RIA name="home" size={16} />{booker.internal ? 'Go to appointments' : 'Back to home'}</button>
            </div>
          </div>
        </div>
        {toast && <div className="af-toast"><RIA name="check" size={17} />{toast}</div>}
      </div>
    );
  }

  return (
    <div className="ra">
      {!embedded && <RaTopbar booker={booker} onToast={toastMsg} />}

      <div className="ra-head">
        <div className="ra-head__in">
          <div>
            <p className="ra-head__eyebrow">{eyebrow}</p>
            <h1>{title}</h1>
            <p>{mode === 'reval' ? 'Look up the prior appointment, then confirm the details for the follow-up.' : 'Complete the steps below. Your progress is saved automatically as a draft.'}</p>
          </div>
        </div>
      </div>

      <div className="ra-wrap">
        <Stepper steps={steps} current={step} furthest={furthest} errorSteps={errorSteps} onJump={jump} />

        {mode === 'reval' && curKey === 'schedule' && (
          <div className="ra-reval">
            <div className="ra-field">
              <label>Prior confirmation number <span className="req">*</span></label>
              <input ref={revalRef} className="ra-input" placeholder="e.g. PQ-24817" />
            </div>
            <button className="af-btn af-btn--primary" onClick={loadReval} style={{ height: 42 }}><RIA name="refresh" size={15} />Load prior appointment</button>
            {revalMsg && <div className="ra-reval__msg"><RIA name="check" size={14} />{revalMsg}</div>}
          </div>
        )}

        <div className="ra-body">
          {curKey === 'schedule' && <S1.StepSchedule ctx={ctx} />}
          {curKey === 'patient' && <S1.StepPatient ctx={ctx} booker={booker} />}
          {curKey === 'applicant' && <S1.StepApplicant ctx={ctx} booker={booker} on={toggles.applicant} setOn={v => setToggle('applicant', v)} />}
          {curKey === 'defense' && <S1.StepDefense ctx={ctx} booker={booker} on={toggles.defense} setOn={v => setToggle('defense', v)} />}
          {curKey === 'insurance' && <S1.StepInsurance ctx={ctx} on={toggles.insurance} setOn={v => setToggle('insurance', v)} />}
          {curKey === 'examiner' && <S1.StepExaminer ctx={ctx} />}
          {curKey === 'claim' && <S2.StepClaim injuries={injuries} setInjuries={setInjuries} />}
          {curKey === 'docs' && <S2.StepDocuments docs={docs} setDocs={setDocs} isPqme={isPqme} hasStrike={hasStrike} setHasStrike={setHasStrike} />}
          {curKey === 'review' && <S2.StepReview ctx={ctx} booker={booker} injuries={injuries} docs={docs} authUsers={authUsers} setAuthUsers={setAuthUsers} toggles={toggles} jump={jump} />}
        </div>
      </div>

      <div className="ra-foot">
        <div className="ra-foot__in">
          <button className="af-btn af-btn--ghost" onClick={goPrev} disabled={step === 0} style={step === 0 ? { opacity: .4, cursor: 'not-allowed' } : null}><RIA name="chevLeft" size={15} />Back</button>
          <div className="ra-foot__save">{saved ? <><span className="dot" />Draft saved</> : <span style={{ color: 'var(--n-400)' }}>Step {step + 1} of {steps.length}</span>}</div>
          <div className="ra-foot__spacer" />
          {step < steps.length - 1 ? (
            <button className="af-btn af-btn--primary af-btn--lg" onClick={goNext}>Continue<RIA name="chevRight" size={16} /></button>
          ) : (
            <button className="af-btn af-btn--green af-btn--lg" onClick={openSubmit}><RIA name="check" size={17} />Submit request</button>
          )}
        </div>
      </div>

      {submitOpen && (
        <div className="ra-scrim" onClick={() => setSubmitOpen(false)}>
          <div className="ra-modal ra-modal--md" onClick={e => e.stopPropagation()}>
            <div className="ra-modal__head">
              <span className="ic tint-amber" style={{ width: 40, height: 40, borderRadius: 11, display: 'flex', alignItems: 'center', justifyContent: 'center' }}><RIA name="alert" size={19} /></span>
              <h3>Submit this request?</h3>
              <button className="ext-iconbtn x" onClick={() => setSubmitOpen(false)}><RIA name="x" size={17} /></button>
            </div>
            <div className="ra-modal__body">
              <p style={{ margin: 0, fontSize: 14, color: 'var(--n-600)', lineHeight: 1.55 }}>{booker.internal
                ? <>This creates the appointment <b>on the patient's behalf</b>. As staff you can still edit it afterwards from the appointment page.</>
                : <>Once submitted, <b>you won't be able to edit this request yourself.</b> To make changes after submitting, you'll need to contact our staff and request the change.</>}</p>
            </div>
            <div className="ra-modal__foot">
              <button className="af-btn af-btn--ghost" onClick={() => setSubmitOpen(false)}>Keep editing</button>
              <button className="af-btn af-btn--green" onClick={confirmSubmit}><RIA name="check" size={16} />Confirm &amp; submit</button>
            </div>
          </div>
        </div>
      )}

      {toast && <div className="af-toast"><RIA name={/submit|sent/i.test(toast) ? 'check' : 'alert'} size={17} />{toast}</div>}
    </div>
  );
}

function RaTopbar({ booker, onToast }) {
  return (
    <header className="ext-nav">
      <div className="ext-nav__in">
        <a className="ext-brand" href="#" onClick={e => e.preventDefault()} aria-label="Home">
          <img src="assets/header-logo.png" alt={booker.org || 'Clinic'} />
          <span className="ext-brand__div" />
          <span className="ext-brand__tag"><b>Appointment Portal</b><span>Patient &amp; case portal</span></span>
        </a>
        <div className="ext-nav__spacer" />
        <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => onToast('Returning to home…')}><RIA name="chevLeft" size={15} />Back to home</button>
        <div className="ext-acct" style={{ cursor: 'default' }}>
          <span className="ava" style={{ background: window.AfCommon.avaColor(booker.name) }}>{window.AfCommon.initials(booker.first, booker.name.split(' ')[1] || booker.label)}</span>
          <span className="who"><b>{booker.name}</b><span>{booker.label}</span></span>
        </div>
      </div>
    </header>
  );
}

window.AfterRequestAppointment = AfterRequestAppointment;
