/* global React */
/* ============================================================
   Request-an-Appointment wizard — Claim Information (table + modal) ·
   Documents (labels incl. Panel Strike List + Other) · Review (full summary)
   ============================================================ */
const { useState: useStateS2 } = React;
const { RcIcon: R2, RaText: T2, RaSelect: S2, RaDate: D2x, RaRadios: RR2, RaCard: C2 } = window.RAC;

/* ---------------- Claim Information ---------------- */
function StepClaim({ injuries, setInjuries }) {
  const D = window.RA;
  const [open, setOpen] = useStateS2(false);
  const [editIdx, setEditIdx] = useStateS2(-1);
  const blank = { cumulative: false, dateOfInjury: '', toDateOfInjury: '', claimNumber: '', wcabOfficeId: '', adj: '', bodyParts: [''] };
  const [draft, setDraft] = useStateS2(blank);
  const [err, setErr] = useStateS2('');

  function openAdd() { setDraft(blank); setEditIdx(-1); setErr(''); setOpen(true); }
  function openEdit(i) { setDraft(JSON.parse(JSON.stringify(injuries[i]))); setEditIdx(i); setErr(''); setOpen(true); }
  function save() {
    if (!draft.dateOfInjury || !draft.claimNumber.trim() || !draft.adj.trim() || !draft.bodyParts.some(b => b.trim())) {
      setErr('Date of injury, claim number, ADJ #, and at least one body part are required.'); return;
    }
    const clean = { ...draft, bodyParts: draft.bodyParts.filter(b => b.trim()) };
    const next = injuries.slice();
    if (editIdx >= 0) next[editIdx] = clean; else next.push(clean);
    setInjuries(next); setOpen(false);
  }
  const wcabName = id => D.WCAB_OFFICES.find(o => o.id === id)?.displayName || '—';
  const dctx = { f: draft, set: (k, v) => setDraft(d => ({ ...d, [k]: v })), errs: {} };

  return (
    <C2 icon="doc" tint="tint-purple" title="Claim information" sub="Add one entry per injury / claim. At least one is required."
      right={<button className="af-btn af-btn--primary af-btn--sm" onClick={openAdd}><R2 name="plus" size={14} />Add claim</button>}>
      {injuries.length === 0 ? (
        <table className="ra-table"><tbody><tr><td className="ra-emptyrow">No claim information added yet. Click <b>Add claim</b> to start.</td></tr></tbody></table>
      ) : (
        <table className="ra-table">
          <thead><tr><th>Date of injury</th><th>Claim #</th><th>ADJ #</th><th>WCAB office</th><th>Body parts</th><th style={{ textAlign: 'right' }}>Actions</th></tr></thead>
          <tbody>
            {injuries.map((inj, i) => (
              <tr key={i}>
                <td className="num">{inj.cumulative ? (inj.dateOfInjury + ' → ' + (inj.toDateOfInjury || '…')) : inj.dateOfInjury}</td>
                <td className="num">{inj.claimNumber}</td>
                <td className="num">{inj.adj}</td>
                <td>{wcabName(inj.wcabOfficeId)}</td>
                <td>{inj.bodyParts.join(', ')}</td>
                <td style={{ textAlign: 'right' }}>
                  <span style={{ display: 'inline-flex', gap: 6 }}>
                    <button className="ra-rowbtn" onClick={() => openEdit(i)} title="Edit"><R2 name="doc" size={14} /></button>
                    <button className="ra-rowbtn danger" onClick={() => setInjuries(injuries.filter((_, x) => x !== i))} title="Delete"><R2 name="x" size={14} /></button>
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {open && (
        <div className="ra-scrim" onClick={() => setOpen(false)}>
          <div className="ra-modal ra-modal--lg" onClick={e => e.stopPropagation()}>
            <div className="ra-modal__head"><h3>{editIdx >= 0 ? 'Edit claim' : 'Add claim'}</h3><button className="ext-iconbtn x" onClick={() => setOpen(false)} aria-label="Close"><R2 name="x" size={17} /></button></div>
            <div className="ra-modal__body">
              <div className="ra-grid">
                <RR2 ctx={dctx} name="cumulative" label="Cumulative trauma injury" col={3} options={window.RaSteps1.YESNO} />
                <D2x ctx={dctx} name="dateOfInjury" label={draft.cumulative ? 'From date' : 'Date of injury'} required col={3} />
                {draft.cumulative && <D2x ctx={dctx} name="toDateOfInjury" label="To date" col={3} />}
                <T2 ctx={dctx} name="claimNumber" label="Claim number" required col={3} maxLength={50} placeholder="e.g. WC24-10480" />
                <S2 ctx={dctx} name="wcabOfficeId" label="WCAB office (venue)" col={3} options={D.WCAB_OFFICES.map(o => ({ value: o.id, label: o.displayName }))} />
                <T2 ctx={dctx} name="adj" label="ADJ #" required col={3} maxLength={50} placeholder="e.g. ADJ-4471102" />
              </div>
              <div style={{ marginTop: 16 }}>
                <label style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--n-700)', display: 'block', marginBottom: 8 }}>Body parts <span style={{ color: 'var(--st-rejected-fg)' }}>*</span></label>
                {draft.bodyParts.map((bp, i) => (
                  <div key={i} style={{ display: 'flex', gap: 8, marginBottom: 8 }}>
                    <input className="ra-input" value={bp} maxLength={500} placeholder="e.g. Lower back" onChange={e => { const a = draft.bodyParts.slice(); a[i] = e.target.value; setDraft(d => ({ ...d, bodyParts: a })); }} />
                    <button className="ra-rowbtn danger" disabled={draft.bodyParts.length <= 1} onClick={() => setDraft(d => ({ ...d, bodyParts: d.bodyParts.filter((_, x) => x !== i) }))}><R2 name="x" size={14} /></button>
                  </div>
                ))}
                <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => setDraft(d => ({ ...d, bodyParts: [...d.bodyParts, ''] }))}><R2 name="plus" size={13} />Add body part</button>
              </div>
              {err && <div className="ra-note warn" style={{ marginTop: 16 }}><span className="i"><R2 name="alert" size={15} /></span><span>{err}</span></div>}
            </div>
            <div className="ra-modal__foot">
              <button className="af-btn af-btn--ghost" onClick={() => setOpen(false)}>Cancel</button>
              <button className="af-btn af-btn--primary" onClick={save}><R2 name="check" size={15} />{editIdx >= 0 ? 'Save claim' : 'Add claim'}</button>
            </div>
          </div>
        </div>
      )}
    </C2>
  );
}

/* ---------------- Documents ---------------- */
function StepDocuments({ docs, setDocs, isPqme, hasStrike, setHasStrike }) {
  const D = window.RA;
  const sample = ['Medical_records.pdf', 'Cover_letter.pdf', 'Panel_strike_list.pdf', 'Deposition.pdf', 'Correspondence.pdf'];
  function addSample() {
    const name = sample[docs.length % sample.length];
    setDocs([...docs, { name, size: (0.4 + Math.random() * 4).toFixed(1) + ' MB', typeId: '', other: '' }]);
  }
  function setDoc(i, patch) { const a = docs.slice(); a[i] = { ...a[i], ...patch }; setDocs(a); }
  const strikeMissing = isPqme && hasStrike && !docs.some(d => d.typeId === 'd3');

  return (
    <C2 icon="doc" tint="tint-blue" title="Documents" sub="Optional. Attach files now — they upload after the appointment is created. PDF, JPG, or PNG up to 10 MB each.">
      <div className="ra-drop" onClick={addSample}>
        <div className="ic"><R2 name="doc" size={28} /></div>
        <b>Drag &amp; drop files here, or click to browse</b>
        <span>PDF, JPG, PNG · up to 10 MB each</span>
      </div>

      {isPqme && (
        <div style={{ marginTop: 16 }}>
          <label className="ra-radio">
            <input type="checkbox" checked={hasStrike} onChange={e => setHasStrike(e.target.checked)} style={{ width: 17, height: 17, accentColor: 'var(--blue-700)' }} />
            I have the panel strike list for this Panel QME appointment
          </label>
          {hasStrike && <div className={'ra-hint' + (strikeMissing ? '' : '')} style={{ marginTop: 6, color: strikeMissing ? 'var(--st-rejected-fg)' : 'var(--n-500)' }}>
            {strikeMissing ? 'Upload a document and label it “Panel Strike List” — required to submit while this is checked.' : 'Label the relevant document “Panel Strike List” below.'}
          </div>}
        </div>
      )}

      {docs.map((doc, i) => (
        <div className="ra-doc" key={i}>
          <span className="fi"><R2 name="doc" size={18} /></span>
          <span className="nm"><b>{doc.name}</b><span>{doc.size}{doc.typeId === 'd3' ? ' · Panel strike list' : ''}</span></span>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6, flex: 'none' }}>
            <select className="ra-select" value={doc.typeId} onChange={e => setDoc(i, { typeId: e.target.value, other: e.target.value === '__other' ? doc.other : '' })}>
              <option value="">— Label document —</option>
              {D.DOC_TYPES.map(t => <option key={t.id} value={t.id}>{t.displayName}</option>)}
              <option value="__other">Other…</option>
            </select>
            {doc.typeId === '__other' && (
              <input className="ra-input" style={{ height: 36, width: 200, fontSize: 12.5 }} placeholder="Enter document label" maxLength={100} value={doc.other} onChange={e => setDoc(i, { other: e.target.value })} />
            )}
          </div>
          <button className="ra-rowbtn danger" onClick={() => setDocs(docs.filter((_, x) => x !== i))}><R2 name="x" size={14} /></button>
        </div>
      ))}
    </C2>
  );
}

/* ---------------- Review (full summary) ---------------- */
function val(v) { return (v === 0 || v) && String(v).trim() ? String(v) : null; }
function ReviewGroup({ title, icon, onEdit, rows, full }) {
  const filled = rows.filter(r => r[1] != null);
  if (filled.length === 0) return null;
  return (
    <div className={'ra-review__group' + (full ? ' ra-review__full' : '')}>
      <h4><R2 name={icon} size={15} />{title}{onEdit && <button className="edit" onClick={onEdit}><R2 name="doc" size={12} />Edit</button>}</h4>
      <dl className="ra-dl">{filled.map((r, i) => <React.Fragment key={i}><dt>{r[0]}</dt><dd>{r[1]}</dd></React.Fragment>)}</dl>
    </div>
  );
}

function StepReview({ ctx, booker, injuries, docs, authUsers, setAuthUsers, toggles, jump }) {
  const D = window.RA;
  const f = ctx.f;
  const typeName = D.APPT_TYPES.find(t => t.id === f.appointmentTypeId)?.name;
  const locName = D.LOCATIONS.find(l => l.id === f.locationId)?.name;
  const gender = window.RaSteps1.GENDER_OPTS.find(g => g.value === f.genderId)?.label;
  const aaOn = booker.lockAttorney === 'applicant' || toggles.applicant;
  const daOn = booker.lockAttorney === 'defense' || toggles.defense;
  const fullName = [f.firstName, f.middleName, f.lastName].filter(Boolean).join(' ');
  const docLabel = d => d.typeId === '__other' ? (d.other || 'Other') : (D.DOC_TYPES.find(t => t.id === d.typeId)?.displayName || 'Unlabeled');

  const [auOpen, setAuOpen] = useStateS2(false);
  const [au, setAu] = useStateS2({ firstName: '', lastName: '', email: '', userRole: '', accessTypeId: 'view' });
  function addAu() { if (!au.email.trim() || !au.userRole) return; setAuthUsers([...authUsers, au]); setAu({ firstName: '', lastName: '', email: '', userRole: '', accessTypeId: 'view' }); setAuOpen(false); }
  const auctx = { f: au, set: (k, v) => setAu(s => ({ ...s, [k]: v })), errs: {} };

  return (
    <>
      <C2 icon="check" tint="tint-green" title="Review & submit" sub="Confirm everything below before sending your request.">
        <div className="ra-review">
          <ReviewGroup title="Appointment" icon="calendar" onEdit={() => jump('schedule')} rows={[
            ['Type', val(typeName)], ['Panel #', val(f.panelNumber)], ['Location', val(locName)], ['Date', val(f.appointmentDate)], ['Time', val(f.appointmentTime)],
          ]} />
          <ReviewGroup title="Patient" icon="user" onEdit={() => jump('patient')} rows={[
            ['Name', val(fullName)], ['Gender', val(gender)], ['Date of birth', val(f.dateOfBirth)], ['Email', val(f.email)],
            ['Cell', val(f.cellPhoneNumber)], ['Phone', val(f.phoneNumber)], ['SSN', f.socialSecurityNumber ? '•••-••-' + String(f.socialSecurityNumber).slice(-4) : null],
            ['Address', val([f.street, f.address, f.city, f.stateId, f.zipCode].filter(Boolean).join(', '))],
            ['Language', val(f.appointmentLanguageId)], ['Interpreter', f.needsInterpreter === true ? ('Yes' + (f.interpreterVendorName ? ' — ' + f.interpreterVendorName : '')) : (f.needsInterpreter === false ? 'No' : null)],
            ['Referred by', val(f.refferedBy)],
          ]} />
          <ReviewGroup title="Employer" icon="map" onEdit={() => jump('patient')} rows={[
            ['Employer', val(f.employerName)], ['Occupation', val(f.employerOccupation)], ['Phone', val(f.employerPhoneNumber)],
            ['Address', val([f.employerStreet, f.employerCity, f.employerStateId, f.employerZipCode].filter(Boolean).join(', '))],
          ]} />
          {!booker.hideAttorneys && (
            <ReviewGroup title="Applicant attorney" icon="user" onEdit={() => jump('applicant')} rows={aaOn ? [
              ['Name', val([f.applicantFirstName, f.applicantLastName].filter(Boolean).join(' '))], ['Firm', val(f.applicantFirmName)], ['Email', val(f.applicantEmail)],
              ['Phone', val(f.applicantPhoneNumber)], ['Address', val([f.applicantStreet, f.applicantCity, f.applicantStateId, f.applicantZipCode].filter(Boolean).join(', '))],
            ] : [['Status', 'Applicant is self-represented']]} />
          )}
          {!booker.hideAttorneys && (
            <ReviewGroup title="Defense attorney" icon="user" onEdit={() => jump('defense')} rows={daOn ? [
              ['Name', val([f.defenseFirstName, f.defenseLastName].filter(Boolean).join(' '))], ['Firm', val(f.defenseFirmName)], ['Email', val(f.defenseEmail)],
              ['Phone', val(f.defensePhoneNumber)], ['Address', val([f.defenseStreet, f.defenseCity, f.defenseStateId, f.defenseZipCode].filter(Boolean).join(', '))],
            ] : [['Status', 'No defense attorney assigned']]} />
          )}
          <ReviewGroup title="Insurance" icon="doc" onEdit={() => jump('insurance')} rows={toggles.insurance ? [
            ['Company', val(f.appointmentInsuranceName)], ['Phone', val(f.appointmentInsurancePhoneNumber)],
            ['Address', val([f.appointmentInsuranceStreet, f.appointmentInsuranceCity, f.appointmentInsuranceStateId, f.appointmentInsuranceZip].filter(Boolean).join(', '))],
          ] : [['Status', 'No insurance on this claim']]} />
          <ReviewGroup title="Claim examiner" icon="user" onEdit={() => jump('examiner')} rows={[
            ['Name', val(f.appointmentClaimExaminerName)], ['Email', val(f.appointmentClaimExaminerEmail)], ['Phone', val(f.appointmentClaimExaminerPhoneNumber)],
            ['Address', val([f.appointmentClaimExaminerStreet, f.appointmentClaimExaminerCity, f.appointmentClaimExaminerStateId, f.appointmentClaimExaminerZip].filter(Boolean).join(', '))],
          ]} />
        </div>

        {/* Claims + documents full-width lists */}
        <div className="ra-review" style={{ marginTop: 14 }}>
          <div className="ra-review__group ra-review__full">
            <h4><R2 name="doc" size={15} />Claim information ({injuries.length})<button className="edit" onClick={() => jump('claim')}><R2 name="doc" size={12} />Edit</button></h4>
            {injuries.length === 0 ? <div className="ra-hint">No claims added.</div> : (
              <table className="ra-table"><thead><tr><th>Date of injury</th><th>Claim #</th><th>ADJ #</th><th>Body parts</th></tr></thead>
                <tbody>{injuries.map((inj, i) => <tr key={i}><td className="num">{inj.cumulative ? inj.dateOfInjury + ' → ' + (inj.toDateOfInjury || '…') : inj.dateOfInjury}</td><td className="num">{inj.claimNumber}</td><td className="num">{inj.adj}</td><td>{inj.bodyParts.join(', ')}</td></tr>)}</tbody>
              </table>
            )}
          </div>
          <div className="ra-review__group ra-review__full">
            <h4><R2 name="doc" size={15} />Documents ({docs.length})<button className="edit" onClick={() => jump('docs')}><R2 name="doc" size={12} />Edit</button></h4>
            {docs.length === 0 ? <div className="ra-hint">No documents attached.</div> : (
              <table className="ra-table"><thead><tr><th>File</th><th>Label</th><th>Size</th></tr></thead>
                <tbody>{docs.map((d, i) => <tr key={i}><td>{d.name}</td><td>{docLabel(d)}</td><td className="num">{d.size}</td></tr>)}</tbody>
              </table>
            )}
          </div>
        </div>
      </C2>

      {!booker.hideAuthorizedUsers && (
        <C2 icon="users" tint="tint-slate" title="Additional authorized users" sub="Grant another person rights to view or manage this appointment."
          right={<button className="af-btn af-btn--primary af-btn--sm" onClick={() => setAuOpen(true)}><R2 name="plus" size={14} />Add user</button>}>
          {authUsers.length === 0 ? (
            <table className="ra-table"><tbody><tr><td className="ra-emptyrow">No additional users. Add someone who can manage this appointment on your behalf.</td></tr></tbody></table>
          ) : (
            <table className="ra-table">
              <thead><tr><th>Name</th><th>Email</th><th>Role</th><th>Rights</th><th style={{ textAlign: 'right' }}>Action</th></tr></thead>
              <tbody>
                {authUsers.map((u, i) => (
                  <tr key={i}>
                    <td>{[u.firstName, u.lastName].filter(Boolean).join(' ') || '—'}</td><td>{u.email}</td><td>{u.userRole}</td>
                    <td>{D.ACCESS_TYPES.find(a => a.value === u.accessTypeId)?.label}</td>
                    <td style={{ textAlign: 'right' }}><button className="ra-rowbtn danger" onClick={() => setAuthUsers(authUsers.filter((_, x) => x !== i))}><R2 name="x" size={14} /></button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </C2>
      )}

      <div className="ra-note warn">
        <span className="i"><R2 name="alert" size={15} /></span>
        <span>Do not include Social Security numbers, dates of birth, or other PHI in document file names.</span>
      </div>

      {auOpen && (
        <div className="ra-scrim" onClick={() => setAuOpen(false)}>
          <div className="ra-modal ra-modal--md" onClick={e => e.stopPropagation()}>
            <div className="ra-modal__head"><h3>Add authorized user</h3><button className="ext-iconbtn x" onClick={() => setAuOpen(false)}><R2 name="x" size={17} /></button></div>
            <div className="ra-modal__body">
              <div className="ra-grid">
                <T2 ctx={auctx} name="firstName" label="First name" col={6} maxLength={64} />
                <T2 ctx={auctx} name="lastName" label="Last name" col={6} maxLength={64} />
                <T2 ctx={auctx} name="email" label="Email" required col={6} type="email" placeholder="name@example.com" />
                <S2 ctx={auctx} name="userRole" label="User role" required col={6} options={D.AUTH_ROLES} placeholder="Select role" />
                <S2 ctx={auctx} name="accessTypeId" label="Rights" col={6} options={D.ACCESS_TYPES} />
              </div>
            </div>
            <div className="ra-modal__foot"><button className="af-btn af-btn--ghost" onClick={() => setAuOpen(false)}>Cancel</button><button className="af-btn af-btn--primary" onClick={addAu}>Save</button></div>
          </div>
        </div>
      )}
    </>
  );
}

window.RaSteps2 = { StepClaim, StepDocuments, StepReview };
