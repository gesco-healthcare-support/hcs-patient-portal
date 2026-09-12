/* global React */
/* ============================================================
   Request-an-Appointment wizard — Steps: Schedule · Patient & Employer ·
   and the four CASE-PARTY steps (Applicant Atty · Defense Atty · Insurance ·
   Claim Examiner), each its own page. AA/DA/Insurance are required-by-default
   with a confirm modal on toggle-off; Claim Examiner is always required.
   ============================================================ */
const { useState: useStateS1 } = React;
const { RcIcon: RI, RaText: RT, RaTextarea: RTA, RaSelect: RS, RaAddress: RA_, RaDate: RD, RaRadios: RR, RaSsn: RSSN, RaCard: RC, RaSwitch: RSW } = window.RAC;

const GENDER_OPTS = [{ value: 1, label: 'Male' }, { value: 2, label: 'Female' }, { value: 3, label: 'Other' }];
const YESNO = [{ value: true, label: 'Yes' }, { value: false, label: 'No' }];

/* ---------------- Step: Schedule ---------------- */
function StepSchedule({ ctx }) {
  const D = window.RA;
  const typeId = ctx.f.appointmentTypeId;
  const isPqme = D.APPT_TYPES.find(t => t.id === typeId)?.pqme;
  const ready = typeId && ctx.f.locationId;
  return (
    <RC icon="calendar" tint="tint-blue" title="Appointment details" sub="Choose the evaluation type, location, and an available slot.">
      <div className="ra-grid">
        <RS ctx={ctx} name="appointmentTypeId" label="Appointment type" required col={3} options={D.APPT_TYPES.map(t => ({ value: t.id, label: t.name }))} />
        {isPqme ? (
          <RT ctx={ctx} name="panelNumber" label="Panel number" required col={3} maxLength={50} placeholder="Panel #" hint="Required for Panel QME." />
        ) : (
          <div className="ra-field col-3">
            <label>Panel number</label>
            <div className="ra-input" style={{ display: 'flex', alignItems: 'center', color: 'var(--n-400)', background: 'var(--n-50)' }}>Panel QME only</div>
          </div>
        )}
        <RS ctx={ctx} name="locationId" label="Location" required col={3} options={D.LOCATIONS.map(l => ({ value: l.id, label: l.name }))} />
        {ready ? (
          <RD ctx={ctx} name="appointmentDate" label="Appointment date" required col={3} hint="Highlighted days have open slots." />
        ) : (
          <div className="ra-field col-3">
            <label>Appointment date</label>
            <div className="ra-input" style={{ display: 'flex', alignItems: 'center', color: 'var(--n-400)', background: 'var(--n-25)' }}>Select type & location first</div>
          </div>
        )}
        {ready && <RS ctx={ctx} name="appointmentTime" label="Appointment time" required col={3} options={D.TIME_SLOTS} placeholder="Select a time" />}
      </div>
      {isPqme && (
        <div className="ra-note warn" style={{ marginTop: 16 }}>
          <span className="i"><RI name="alert" size={15} /></span>
          <span>Panel QME requires a <b>Panel number</b> here and a <b>panel strike list</b> document in the Documents step.</span>
        </div>
      )}
    </RC>
  );
}

/* ---------------- Step: Patient & Employer ---------------- */
function StepPatient({ ctx, booker }) {
  const D = window.RA;
  return (
    <>
      <RC icon="user" tint="tint-blue" title="Patient demographics"
        sub={booker.isNonPatient ? 'Select an existing patient or enter a new one.' : 'Confirm the details we have on file.'}>
        {booker.patientPicker && (
          <div className="ra-grid" style={{ marginBottom: 18 }}>
            <RS ctx={ctx} name="patientId" label="Existing patient" col={6} options={D.EXISTING_PATIENTS.map(p => ({ value: p.id, label: p.displayName }))} placeholder="None — enter a new patient below" />
            <div className="ra-field col-6" style={{ justifyContent: 'flex-end' }}>
              <div className="ra-note"><span className="i"><RI name="user" size={15} /></span><span>Pick a patient to prefill, or leave as <b>None</b> and type the details below to add a new one.</span></div>
            </div>
          </div>
        )}
        <div className="ra-grid">
          <RT ctx={ctx} name="lastName" label="Last name" required col={3} maxLength={50} />
          <RT ctx={ctx} name="firstName" label="First name" required col={3} maxLength={50} />
          <RT ctx={ctx} name="middleName" label="Middle name" col={3} maxLength={50} />
          <RR ctx={ctx} name="genderId" label="Gender" col={3} options={GENDER_OPTS} />

          <RD ctx={ctx} name="dateOfBirth" label="Date of birth" required col={3} />
          <RT ctx={ctx} name="email" label="Email" required col={3} type="email" maxLength={50} readOnly={!booker.isNonPatient} />
          <RT ctx={ctx} name="cellPhoneNumber" label="Cell phone" col={3} maxLength={12} placeholder="(000) 000-0000" />
          <RT ctx={ctx} name="phoneNumber" label="Phone number" col={3} maxLength={20} placeholder="(000) 000-0000" />

          <RSSN ctx={ctx} name="socialSecurityNumber" label="Social Security #" col={3} />
          <RA_ ctx={ctx} name="street" label="Street" col={3} />
          <RT ctx={ctx} name="address" label="Unit #" col={3} maxLength={100} />
          <RT ctx={ctx} name="city" label="City" col={3} maxLength={50} />

          <RS ctx={ctx} name="stateId" label="State" col={3} options={D.STATES} />
          <RT ctx={ctx} name="zipCode" label="Zip code" col={3} maxLength={15} />
          <RS ctx={ctx} name="appointmentLanguageId" label="Appointment language" col={3} options={D.LANGUAGES} />
          <RT ctx={ctx} name="refferedBy" label="Referred by" col={3} maxLength={50} />

          <RR ctx={ctx} name="needsInterpreter" label={booker.interpreterPrompt} col={ctx.f.needsInterpreter === true ? 3 : 6} options={YESNO} />
          {ctx.f.needsInterpreter === true && (
            <RT ctx={ctx} name="interpreterVendorName" label="Interpreter vendor / language" col={3} placeholder="Vendor or language details" />
          )}
        </div>
      </RC>

      <RC icon="map" tint="tint-slate" title="Employer details" sub="Where the injured worker is or was employed.">
        <div className="ra-grid">
          <RT ctx={ctx} name="employerName" label="Employer name" required col={4} maxLength={255} placeholder="Employer name" />
          <RT ctx={ctx} name="employerOccupation" label="Occupation" required col={4} maxLength={255} placeholder="Occupation" />
          <RT ctx={ctx} name="employerPhoneNumber" label="Phone number" col={4} maxLength={12} placeholder="(000) 000-0000" />
          <RA_ ctx={ctx} name="employerStreet" label="Street" col={3} />
          <RT ctx={ctx} name="employerCity" label="City" col={3} maxLength={255} />
          <RS ctx={ctx} name="employerStateId" label="State" col={3} options={D.STATES} />
          <RT ctx={ctx} name="employerZipCode" label="Zip code" col={3} maxLength={10} />
        </div>
      </RC>
    </>
  );
}

/* attorney field grid — prefix = 'applicant' | 'defense' */
function attorneyGrid(ctx, prefix, locked) {
  const D = window.RA;
  return (
    <div className="ra-grid">
      <RT ctx={ctx} name={prefix + 'FirstName'} label="First name" required col={3} maxLength={50} />
      <RT ctx={ctx} name={prefix + 'LastName'} label="Last name" required col={3} maxLength={50} />
      <RT ctx={ctx} name={prefix + 'Email'} label="Email" required col={6} type="email" readOnly={locked} />
      <RT ctx={ctx} name={prefix + 'FirmName'} label="Firm name" required col={3} maxLength={50} />
      <RT ctx={ctx} name={prefix + 'WebAddress'} label="Web address" col={6} maxLength={100} />
      <RT ctx={ctx} name={prefix + 'PhoneNumber'} label="Phone number" required col={3} maxLength={20} />
      <RT ctx={ctx} name={prefix + 'FaxNumber'} label="Fax" col={3} maxLength={19} />
      <RA_ ctx={ctx} name={prefix + 'Street'} label="Street" required col={3} />
      <RT ctx={ctx} name={prefix + 'City'} label="City" required col={3} maxLength={50} />
      <RS ctx={ctx} name={prefix + 'StateId'} label="State" required col={3} options={D.STATES} />
      <RT ctx={ctx} name={prefix + 'ZipCode'} label="Zip" required col={3} maxLength={10} />
    </div>
  );
}

/* Confirm-on-toggle-off modal */
function PartyConfirm({ copy, onKeep, onConfirm }) {
  return (
    <div className="ra-scrim" onClick={onKeep}>
      <div className="ra-modal ra-modal--md" onClick={e => e.stopPropagation()}>
        <div className="ra-modal__head"><span className="ra-card__head" style={{ padding: 0, border: 0 }}><span className="ic tint-amber" style={{ width: 38, height: 38, borderRadius: 11, display: 'flex', alignItems: 'center', justifyContent: 'center', marginRight: 10 }}><RI name="alert" size={18} /></span></span><h3>{copy.title}</h3><button className="ext-iconbtn x" onClick={onKeep}><RI name="x" size={17} /></button></div>
        <div className="ra-modal__body"><p style={{ margin: 0, fontSize: 14, color: 'var(--n-600)', lineHeight: 1.5 }}>{copy.body}</p></div>
        <div className="ra-modal__foot">
          <button className="af-btn af-btn--ghost" onClick={onKeep}>{copy.keep}</button>
          <button className="af-btn af-btn--primary" onClick={onConfirm}>{copy.confirm}</button>
        </div>
      </div>
    </div>
  );
}

/* Generic party page: card + optional include toggle (with confirm modal) */
function PartyStep({ ctx, icon, tint, title, sub, toggleable, on, setOn, copy, offText, children }) {
  const [confirm, setConfirm] = useStateS1(false);
  function handleToggle(v) { if (v) { setOn(true); } else { setConfirm(true); } }
  return (
    <>
      <RC icon={icon} tint={tint} title={title} sub={sub}
        right={toggleable && <RSW label="Include" checked={on} onChange={handleToggle} />}>
        {on ? children : (
          <div className="ra-note"><span className="i"><RI name="check" size={15} /></span><span>{offText}</span></div>
        )}
      </RC>
      {confirm && <PartyConfirm copy={copy} onKeep={() => setConfirm(false)} onConfirm={() => { setOn(false); setConfirm(false); }} />}
    </>
  );
}

function StepApplicant({ ctx, booker, on, setOn }) {
  const mandatory = booker.lockAttorney === 'applicant';
  return (
    <PartyStep ctx={ctx} icon="user" tint="tint-blue" title="Applicant attorney"
      sub={mandatory ? 'Your firm — prefilled from your account.' : 'Required by default. Turn off only if the applicant is self-represented.'}
      toggleable={!mandatory} on={mandatory || on} setOn={setOn}
      offText="Applicant is self-represented — no applicant attorney on this claim."
      copy={{ title: 'Is the applicant self-represented?', body: 'Turning this off confirms the applicant has no attorney and is representing themselves. You can add an attorney later by contacting staff.', keep: 'Cancel', confirm: 'Yes, self-represented' }}>
      {attorneyGrid(ctx, 'applicant', mandatory)}
    </PartyStep>
  );
}

function StepDefense({ ctx, booker, on, setOn }) {
  const mandatory = booker.lockAttorney === 'defense';
  return (
    <PartyStep ctx={ctx} icon="user" tint="tint-slate" title="Defense attorney"
      sub={mandatory ? 'Your firm — prefilled from your account.' : 'Required by default. Turn off only if no defense attorney is assigned.'}
      toggleable={!mandatory} on={mandatory || on} setOn={setOn}
      offText="No defense attorney is assigned to this claim."
      copy={{ title: 'No defense attorney assigned?', body: 'Turning this off confirms there is no defense attorney assigned to this claim. You can add one later by contacting staff.', keep: 'Cancel', confirm: 'Yes, none assigned' }}>
      {attorneyGrid(ctx, 'defense', mandatory)}
    </PartyStep>
  );
}

function StepInsurance({ ctx, on, setOn }) {
  const D = window.RA;
  return (
    <PartyStep ctx={ctx} icon="doc" tint="tint-teal" title="Insurance"
      sub="Required by default. Turn off only if there is no primary insurance carrier."
      toggleable={true} on={on} setOn={setOn}
      offText="No primary insurance carrier on this claim."
      copy={{ title: 'No insurance on this claim?', body: 'Turning this off confirms there is no primary insurance carrier for this claim. You can add one later by contacting staff.', keep: 'Cancel', confirm: 'Yes, no insurance' }}>
      <div className="ra-grid">
        <RT ctx={ctx} name="appointmentInsuranceName" label="Insurance company" required col={6} maxLength={50} />
        <RT ctx={ctx} name="appointmentInsuranceSuite" label="Suite" col={3} maxLength={255} />
        <RT ctx={ctx} name="appointmentInsurancePhoneNumber" label="Phone number" col={3} maxLength={12} />
        <RT ctx={ctx} name="appointmentInsuranceFaxNumber" label="Fax" col={3} maxLength={20} />
        <RA_ ctx={ctx} name="appointmentInsuranceStreet" label="Street" col={3} />
        <RT ctx={ctx} name="appointmentInsuranceCity" label="City" col={3} maxLength={50} />
        <RS ctx={ctx} name="appointmentInsuranceStateId" label="State" col={3} options={D.STATES} />
        <RT ctx={ctx} name="appointmentInsuranceZip" label="Zip" col={3} maxLength={10} />
      </div>
    </PartyStep>
  );
}

function StepExaminer({ ctx }) {
  const D = window.RA;
  return (
    <RC icon="user" tint="tint-amber" title="Claim examiner" sub="Adjuster handling the claim. Always required.">
      <div className="ra-grid">
        <RT ctx={ctx} name="appointmentClaimExaminerName" label="Name" required col={6} maxLength={50} />
        <RT ctx={ctx} name="appointmentClaimExaminerEmail" label="Email" required col={6} type="email" maxLength={50} />
        <RT ctx={ctx} name="appointmentClaimExaminerSuite" label="Suite" col={3} maxLength={255} />
        <RT ctx={ctx} name="appointmentClaimExaminerPhoneNumber" label="Phone number" required col={3} maxLength={12} />
        <RT ctx={ctx} name="appointmentClaimExaminerFax" label="Fax" col={3} maxLength={20} />
        <RA_ ctx={ctx} name="appointmentClaimExaminerStreet" label="Street" required col={3} />
        <RT ctx={ctx} name="appointmentClaimExaminerCity" label="City" required col={3} maxLength={50} />
        <RS ctx={ctx} name="appointmentClaimExaminerStateId" label="State" required col={3} options={D.STATES} />
        <RT ctx={ctx} name="appointmentClaimExaminerZip" label="Zip" required col={3} maxLength={10} />
      </div>
    </RC>
  );
}

window.RaSteps1 = { StepSchedule, StepPatient, StepApplicant, StepDefense, StepInsurance, StepExaminer, GENDER_OPTS, YESNO };
