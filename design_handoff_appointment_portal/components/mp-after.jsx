/* global React */
/* ============================================================
   My Profile — AFTER. Profile header + grouped sections (Personal,
   Contact, Address, Preferences), read-only with per-section Edit →
   Save (confirm modal). Attorneys/examiners get an enriched read-only
   card. SSN omitted; email read-only (managed by support).
   ============================================================ */
const { useState: useStateMP } = React;
const { RcIcon: MPI, RaText: MT, RaSelect: MS, RaRadios: MR } = window.RAC;

const MP_GENDER = [{ value: 'Male', label: 'Male' }, { value: 'Female', label: 'Female' }, { value: 'Other', label: 'Other' }];
const MP_PHONE_TYPE = [{ value: 'Cell', label: 'Cell' }, { value: 'Home', label: 'Home' }, { value: 'Work', label: 'Work' }];
const MP_YESNO = [{ value: true, label: 'Yes' }, { value: false, label: 'No' }];

function VRow({ k, v, full, mono, lock }) {
  return (
    <div className={'mp-field' + (full ? ' full' : '')}>
      <span className="k">{k}</span>
      <span className={'v' + (mono ? ' mono' : '') + (v ? '' : ' empty')}>{v || '—'}{lock && <span className="lock"><MPI name="settings" size={11} />{lock}</span>}</span>
    </div>
  );
}

function patientSeed(role) {
  return {
    firstName: role.first, lastName: role.name.split(' ')[1] || '', middleName: '',
    gender: 'Female', dateOfBirth: '1984-03-22', email: role.email,
    phoneNumber: '(213) 555-0102', cellPhoneNumber: '(213) 555-0148', phoneType: 'Cell',
    street: '128 W 4th St', unit: 'Apt 5', city: 'Los Angeles', state: 'CA', zip: '90013',
    appointmentLanguage: 'Spanish', needsInterpreter: true, interpreterVendor: 'LA Interpreting Services', otherLanguage: '',
  };
}

function AfterMyProfile({ roleKey }) {
  const role = window.RA.BOOKERS[roleKey];
  const isPatient = !role.isNonPatient;
  const [vals, setVals] = useStateMP(() => patientSeed(role));
  const [editing, setEditing] = useStateMP(null);
  const [draft, setDraft] = useStateMP({});
  const [confirm, setConfirm] = useStateMP(false);
  const [toast, setToast] = useStateMP(null);

  // reset on role change
  React.useEffect(() => { setVals(patientSeed(role)); setEditing(null); }, [roleKey]);

  function showToast(m) { setToast(m); clearTimeout(window.__mpT); window.__mpT = setTimeout(() => setToast(null), 2800); }
  function startEdit(key) { setDraft({ ...vals }); setEditing(key); }
  function cancelEdit() { setEditing(null); }
  function requestSave() { setConfirm(true); }
  function doSave() { setVals(draft); setEditing(null); setConfirm(false); showToast('Profile changes saved.'); }
  const ctx = { f: draft, set: (k, v) => setDraft(d => ({ ...d, [k]: v })), errs: {} };

  const D = window.RA;
  const initials = window.AfCommon.initials(role.first, role.name.split(' ')[1] || role.label);

  function Section({ k, icon, tint, title, view, edit }) {
    const on = editing === k;
    return (
      <div className="mp-card">
        <div className="mp-card__head">
          <span className={'ic ' + tint}><MPI name={icon} size={18} /></span>
          <h3>{title}</h3>
          {!on && <div className="right"><button className="mp-editbtn" onClick={() => startEdit(k)}><MPI name="doc" size={14} />Edit</button></div>}
        </div>
        <div className="mp-card__body">
          {on ? (
            <>
              <div className="ra-grid">{edit}</div>
              <div className="mp-editfoot">
                <button className="af-btn af-btn--ghost" onClick={cancelEdit}>Cancel</button>
                <button className="af-btn af-btn--primary" onClick={requestSave}><MPI name="check" size={15} />Save changes</button>
              </div>
            </>
          ) : <div className="mp-dl">{view}</div>}
        </div>
      </div>
    );
  }

  return (
    <div className="mp">
      <header className="ext-nav">
        <div className="ext-nav__in">
          <a className="ext-brand" href="#" onClick={e => e.preventDefault()} aria-label="Home">
            <img src="assets/header-logo.png" alt={role.org || 'Clinic'} />
            <span className="ext-brand__div" />
            <span className="ext-brand__tag"><b>Appointment Portal</b><span>Patient &amp; case portal</span></span>
          </a>
          <div className="ext-nav__spacer" />
          <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => showToast('Returning to home…')}><MPI name="chevLeft" size={15} />Back to home</button>
        </div>
      </header>

      <div className="mp-head">
        <div className="mp-head__in">
          <span className="mp-head__ava" style={{ background: window.AfCommon.avaColor(role.name) }}>{initials}</span>
          <div className="mp-head__meta">
            <h1>{vals.firstName} {vals.lastName}</h1>
            <span className="mp-head__role"><MPI name="user" size={12} />{role.label}</span>
            <div className="mp-head__sub">
              <span><MPI name="user" size={14} />{role.email}</span>
              {role.org && <span><MPI name="map" size={14} />{role.org}</span>}
            </div>
          </div>
        </div>
      </div>

      <div className="mp-wrap">
        {isPatient ? (
          <>
            <Section k="personal" icon="user" tint="tint-blue" title="Personal"
              view={<>
                <VRow k="First name" v={vals.firstName} />
                <VRow k="Last name" v={vals.lastName} />
                <VRow k="Middle name" v={vals.middleName} />
                <VRow k="Gender" v={vals.gender} />
                <VRow k="Date of birth" v={vals.dateOfBirth} mono />
              </>}
              edit={<>
                <MT ctx={ctx} name="firstName" label="First name" required col={4} maxLength={50} />
                <MT ctx={ctx} name="lastName" label="Last name" required col={4} maxLength={50} />
                <MT ctx={ctx} name="middleName" label="Middle name" col={4} maxLength={50} />
                <MS ctx={ctx} name="gender" label="Gender" col={4} options={MP_GENDER} />
                <div className="ra-field col-4"><label>Date of birth</label><div className="ra-affix"><input className="ra-input" type="date" value={draft.dateOfBirth || ''} onChange={e => ctx.set('dateOfBirth', e.target.value)} /><button type="button" tabIndex={-1}><MPI name="calendar" size={16} /></button></div></div>
              </>} />

            <Section k="contact" icon="user" tint="tint-teal" title="Contact"
              view={<>
                <VRow k="Email" v={vals.email} lock="Managed by support" full />
                <VRow k="Phone" v={vals.phoneNumber} mono />
                <VRow k="Cell phone" v={vals.cellPhoneNumber} mono />
                <VRow k="Phone type" v={vals.phoneType} />
              </>}
              edit={<>
                <div className="ra-field col-6"><label>Email <span className="opt">(managed by support)</span></label><input className="ra-input" value={vals.email} readOnly style={{ background: 'var(--n-50)', color: 'var(--n-500)' }} /></div>
                <MS ctx={ctx} name="phoneType" label="Phone type" col={6} options={MP_PHONE_TYPE} />
                <MT ctx={ctx} name="phoneNumber" label="Phone" col={6} maxLength={20} placeholder="(000) 000-0000" />
                <MT ctx={ctx} name="cellPhoneNumber" label="Cell phone" col={6} maxLength={12} placeholder="(000) 000-0000" />
              </>} />

            <Section k="address" icon="map" tint="tint-slate" title="Address"
              view={<>
                <VRow k="Street" v={vals.street} />
                <VRow k="Unit #" v={vals.unit} />
                <VRow k="City" v={vals.city} />
                <VRow k="State" v={vals.state} />
                <VRow k="Zip code" v={vals.zip} mono />
              </>}
              edit={<>
                <MT ctx={ctx} name="street" label="Street" col={6} maxLength={255} />
                <MT ctx={ctx} name="unit" label="Unit #" col={6} maxLength={100} />
                <MT ctx={ctx} name="city" label="City" col={4} maxLength={50} />
                <MS ctx={ctx} name="state" label="State" col={4} options={D.STATES} />
                <MT ctx={ctx} name="zip" label="Zip code" col={4} maxLength={15} />
              </>} />

            <Section k="preferences" icon="settings" tint="tint-amber" title="Preferences"
              view={<>
                <VRow k="Appointment language" v={vals.appointmentLanguage} />
                <VRow k="Interpreter" v={vals.needsInterpreter ? 'Yes — ' + (vals.interpreterVendor || '') : 'No'} />
                <VRow k="Other language" v={vals.otherLanguage} />
              </>}
              edit={<>
                <MS ctx={ctx} name="appointmentLanguage" label="Appointment language" col={6} options={D.LANGUAGES} />
                <MR ctx={ctx} name="needsInterpreter" label="Need an interpreter?" col={6} options={MP_YESNO} />
                {draft.needsInterpreter === true && <MT ctx={ctx} name="interpreterVendor" label="Interpreter vendor / language" col={6} maxLength={255} />}
                <MT ctx={ctx} name="otherLanguage" label="Other language" col={6} maxLength={100} />
              </>} />
          </>
        ) : (
          <div className="mp-card">
            <div className="mp-card__head"><span className="ic tint-blue"><MPI name="user" size={18} /></span><h3>Profile</h3></div>
            <div className="mp-card__body">
              <div className="mp-dl">
                <VRow k="Name" v={role.name} />
                <VRow k="Role" v={role.label} />
                <VRow k="Firm / organization" v={role.org} />
                <VRow k="Email" v={role.email} lock="Managed by support" />
                <VRow k="Phone" v={vals.phoneNumber} mono />
              </div>
              <div className="ra-note" style={{ marginTop: 16 }}><span className="i"><MPI name="alert" size={15} /></span><span>Your profile is managed through your firm's account. Contact support to update these details.</span></div>
            </div>
          </div>
        )}

        {/* Account & security */}
        <div className="mp-card">
          <div className="mp-card__head"><span className="ic tint-slate"><MPI name="settings" size={18} /></span><h3>Account &amp; security</h3></div>
          <div>
            <div className="mp-link">
              <span className="ic tint-amber"><MPI name="settings" size={17} /></span>
              <div className="tx"><b>Password</b><span>Change your password on the secure sign-in page.</span></div>
              <button className="mp-link__action" onClick={() => showToast('Opening secure password page…')}>Change password<MPI name="chevRight" size={13} /></button>
            </div>
            <div className="mp-link">
              <span className="ic tint-teal"><MPI name="user" size={17} /></span>
              <div className="tx"><b>Email address</b><span>{role.email}</span></div>
              <span className="mp-link__pill"><MPI name="settings" size={12} />Managed by support</span>
            </div>
          </div>
        </div>
      </div>

      {confirm && (
        <div className="ra-scrim" onClick={() => setConfirm(false)}>
          <div className="ra-modal ra-modal--md" onClick={e => e.stopPropagation()}>
            <div className="ra-modal__head">
              <span className="ic tint-blue" style={{ width: 40, height: 40, borderRadius: 11, display: 'flex', alignItems: 'center', justifyContent: 'center' }}><MPI name="check" size={19} /></span>
              <h3>Save profile changes?</h3>
              <button className="ext-iconbtn x" onClick={() => setConfirm(false)}><MPI name="x" size={17} /></button>
            </div>
            <div className="ra-modal__body"><p style={{ margin: 0, fontSize: 14, color: 'var(--n-600)', lineHeight: 1.55 }}>Your updated details will be saved to your profile and used for future appointment requests.</p></div>
            <div className="ra-modal__foot">
              <button className="af-btn af-btn--ghost" onClick={() => setConfirm(false)}>Keep editing</button>
              <button className="af-btn af-btn--primary" onClick={doSave}><MPI name="check" size={16} />Confirm &amp; save</button>
            </div>
          </div>
        </div>
      )}

      {toast && <div className="af-toast"><MPI name="check" size={17} />{toast}</div>}
    </div>
  );
}

window.AfterMyProfile = AfterMyProfile;
