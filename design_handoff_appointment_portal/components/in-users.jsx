/* global React */
/* ============================================================
   Group 5 — Users & Access hub. Invite external (form + tokenized
   result + copy link) · Pending invites (resend/revoke/copy, expiry)
   · Internal users (staff list + create modal, email queued/failed
   states) · Tenants (IT Admin). Role-aware sub-nav.
   ============================================================ */
const { useState: useStateU } = React;
function UI({ name, size }) { return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size || 18)} />; }

const U_EXT_ROLES = ['Patient', 'Applicant Attorney', 'Defense Attorney', 'Claim Examiner'];
const U_INVITES0 = [
  { id: 'i1', email: 'mrodriguez@gmail.com', role: 'Patient', by: 'Priya Shah', sent: 'Jun 9, 2026', days: 5, status: 'pending' },
  { id: 'i2', email: 'jchen@chenlaw.com', role: 'Applicant Attorney', by: 'Sandra Cole', sent: 'Jun 8, 2026', days: 4, status: 'pending' },
  { id: 'i3', email: 'kwhitfield@sierraclaims.com', role: 'Claim Examiner', by: 'Sandra Cole', sent: 'Jun 5, 2026', days: 1, status: 'pending' },
  { id: 'i4', email: 'dlopez@aol.com', role: 'Patient', by: 'Priya Shah', sent: 'Jun 2, 2026', days: 0, status: 'expired' },
  { id: 'i5', email: 'lpelton@peltondg.com', role: 'Defense Attorney', by: 'Marcus Lee', sent: 'May 30, 2026', days: 0, status: 'accepted' },
];
const U_STAFF0 = [
  { id: 's1', first: 'Sandra', last: 'Cole', email: 'scole@falkinstein.com', role: 'Staff Supervisor', tenant: 'Falkinstein Orthopedics', active: true },
  { id: 's2', first: 'Priya', last: 'Shah', email: 'pshah@falkinstein.com', role: 'Intake Staff', tenant: 'Falkinstein Orthopedics', active: true },
  { id: 's3', first: 'Tom', last: 'Avery', email: 'tavery@bayspine.com', role: 'Intake Staff', tenant: 'Bay Area Spine Group', active: true },
  { id: 's4', first: 'Dana', last: 'Kim', email: 'dkim@sierrame.com', role: 'Staff Supervisor', tenant: 'Sierra Medical Evaluations', active: false },
];
const U_TENANTS0 = [
  { id: 't1', name: 'Falkinstein Orthopedics', sub: 'falkinstein', edition: 'Professional', users: 14, appts: 128, active: true },
  { id: 't2', name: 'Bay Area Spine Group', sub: 'bayspine', edition: 'Professional', users: 9, appts: 96, active: true },
  { id: 't3', name: 'Sierra Medical Evaluations', sub: 'sierrame', edition: 'Standard', users: 6, appts: 74, active: true },
  { id: 't4', name: 'Coastal QME Associates', sub: 'coastalqme', edition: 'Standard', users: 4, appts: 53, active: false },
];
const U_TENANT_NAMES = U_TENANTS0.map(t => t.name);

const U_SECTIONS = [
  ['invite', 'Invite External User', 'user', ['itadmin', 'supervisor', 'intake']],
  ['pending', 'Pending Invites', 'clock', ['itadmin', 'supervisor', 'intake']],
  ['staff', 'Internal Users', 'users', ['itadmin', 'supervisor']],
  ['tenants', 'Tenants', 'grid', ['itadmin']],
];

function InUsers({ roleKey, onToast }) {
  const [section, setSection] = useStateU('invite');
  const sections = U_SECTIONS.filter(s => s[3].includes(roleKey));
  const cur = sections.find(s => s[0] === section) ? section : sections[0][0];
  return (
    <>
      <div className="ia-head"><div><h1>Users &amp; access</h1><p>Invitations, staff accounts{roleKey === 'itadmin' ? ', and tenants' : ''}.</p></div></div>
      <div className="cf">
        <nav className="cf-rail">
          {sections.map(([k, lbl, ic]) => (
            <button key={k} className="cf-railitem" data-on={cur === k} onClick={() => setSection(k)}>
              <span className="i"><UI name={ic} size={16} /></span>{lbl}
            </button>
          ))}
        </nav>
        <div>
          {cur === 'invite' && <UxInvite onToast={onToast} />}
          {cur === 'pending' && <UxPending onToast={onToast} />}
          {cur === 'staff' && <UxStaff roleKey={roleKey} onToast={onToast} />}
          {cur === 'tenants' && <UxTenants onToast={onToast} />}
        </div>
      </div>
    </>
  );
}

/* ---------------- Invite External User ---------------- */
function UxInvite({ onToast }) {
  const [d, setD] = useStateU({ first: '', last: '', email: '', role: 'Patient', firm: '' });
  const [result, setResult] = useStateU(null);
  const isAttorney = d.role === 'Applicant Attorney' || d.role === 'Defense Attorney';
  function send() {
    if (!d.email.trim() || !/.+@.+\..+/.test(d.email)) { onToast('Enter a valid email address.'); return; }
    setResult({ ...d, tenant: 'Falkinstein Orthopedics', expires: 'Jun 18, 2026', url: 'https://auth.falkinstein.example/Account/Register?inviteToken=9f2c…b41a' });
    onToast('Invite sent to ' + d.email + '.');
  }
  return (
    <>
      <div className="ra-card">
        <div className="ra-card__head"><span className="ic tint-blue"><UI name="user" size={19} /></span>
          <div><h3>Invite an external user</h3><p>Sends a one-time registration link by email. The link expires after 7 days.</p></div>
        </div>
        <div className="ra-card__body">
          <div className="ra-grid">
            <div className="ra-field col-6"><label>First name <span className="opt">(optional)</span></label><input className="ra-input" maxLength={128} value={d.first} onChange={e => setD({ ...d, first: e.target.value })} /></div>
            <div className="ra-field col-6"><label>Last name <span className="opt">(optional)</span></label><input className="ra-input" maxLength={128} value={d.last} onChange={e => setD({ ...d, last: e.target.value })} /></div>
            <div className="ra-field col-6"><label>Email <span className="req">*</span></label><input className="ra-input" type="email" maxLength={256} placeholder="user@example.com" value={d.email} onChange={e => setD({ ...d, email: e.target.value })} /></div>
            <div className="ra-field col-6"><label>Role <span className="req">*</span></label>
              <select className="ra-select" value={d.role} onChange={e => setD({ ...d, role: e.target.value, firm: (e.target.value === 'Applicant Attorney' || e.target.value === 'Defense Attorney') ? d.firm : '' })}>{U_EXT_ROLES.map(r => <option key={r}>{r}</option>)}</select>
              <div className="ra-hint">Internal roles are created under Internal Users — not invitable here.</div>
            </div>
            {isAttorney && (
              <div className="ra-field col-6"><label>Firm name <span className="opt">(optional)</span></label>
                <input className="ra-input" maxLength={100} placeholder="e.g. Brooks & Associates" value={d.firm} onChange={e => setD({ ...d, firm: e.target.value })} />
              </div>
            )}
          </div>
          <div className="mp-editfoot">
            <button className="af-btn af-btn--ghost" onClick={() => { setD({ first: '', last: '', email: '', role: 'Patient', firm: '' }); setResult(null); }}>Reset</button>
            <button className="af-btn af-btn--primary" onClick={send}><UI name="arrowUp" size={15} />Send invite</button>
          </div>
        </div>
      </div>

      {result && (
        <div className="ux-result">
          <div className="ux-result__head"><UI name="check" size={16} />Invite sent</div>
          <div className="ux-result__body">
            <div className="ux-result__grid">
              <div><div className="k">Tenant</div><div className="v">{result.tenant}</div></div>
              <div><div className="k">Email</div><div className="v">{result.email}</div></div>
              <div><div className="k">Role</div><div className="v">{result.role}</div></div>
              {result.firm && <div><div className="k">Firm</div><div className="v">{result.firm}</div></div>}
              <div><div className="k">Expires</div><div className="v">{result.expires}</div></div>
            </div>
            <div className="ux-link">
              <code>{result.url}</code>
              <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => onToast('Link copied to clipboard.')}><UI name="doc" size={14} />Copy link</button>
            </div>
            <div className="ra-hint" style={{ marginTop: 8 }}>Share the link manually if email delivery is delayed.</div>
          </div>
        </div>
      )}
    </>
  );
}

/* ---------------- Pending Invites ---------------- */
function UxPending({ onToast }) {
  const [rows, setRows] = useStateU(U_INVITES0);
  const stLbl = { pending: ['Pending', 'invited'], accepted: ['Accepted', 'linked'], expired: ['Expired', 'none'] };
  return (
    <div className="ia-wrap">
      <table className="ia-table" style={{ minWidth: 0 }}>
        <thead><tr><th>Email</th><th>Role</th><th>Invited by</th><th>Sent</th><th>Expires</th><th>Status</th><th style={{ textAlign: 'right' }}>Actions</th></tr></thead>
        <tbody>
          {rows.map(r => (
            <tr key={r.id}>
              <td><b style={{ color: 'var(--n-900)' }}>{r.email}</b></td>
              <td><span className="ux-role">{r.role}</span></td>
              <td className="ia-sub">{r.by}</td>
              <td className="ia-sub" style={{ fontFamily: 'var(--font-num)' }}>{r.sent}</td>
              <td>{r.status === 'pending' ? <span className={'ux-exp ' + (r.days <= 1 ? 'soon' : 'ok')}>{r.days}d left</span> : <span className="ux-exp gone">—</span>}</td>
              <td><span className={'pp-portal ' + stLbl[r.status][1]}><span className="d" />{stLbl[r.status][0]}</span></td>
              <td style={{ textAlign: 'right' }}>
                <span style={{ display: 'inline-flex', gap: 6 }}>
                  {r.status !== 'accepted' && <button className="ra-rowbtn" title="Resend" onClick={() => { setRows(rs => rs.map(x => x.id === r.id ? { ...x, status: 'pending', days: 7, sent: 'Jun 11, 2026' } : x)); onToast('Invite re-sent to ' + r.email + '.'); }}><UI name="refresh" size={14} /></button>}
                  {r.status === 'pending' && <button className="ra-rowbtn" title="Copy link" onClick={() => onToast('Invite link copied.')}><UI name="doc" size={14} /></button>}
                  {r.status === 'pending' && <button className="ra-rowbtn danger" title="Revoke" onClick={() => { setRows(rs => rs.map(x => x.id === r.id ? { ...x, status: 'expired', days: 0 } : x)); onToast('Invite revoked — the link no longer works.'); }}><UI name="x" size={14} /></button>}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/* ---------------- Internal Users ---------------- */
function UxStaff({ roleKey, onToast }) {
  const [rows, setRows] = useStateU(U_STAFF0);
  const [open, setOpen] = useStateU(false);
  const [d, setD] = useStateU({ tenant: '', role: '', first: '', last: '', email: '', phone: '' });
  const [result, setResult] = useStateU(null);
  function create() {
    if (!d.tenant || !d.role || !d.first.trim() || !d.last.trim() || !/.+@.+\..+/.test(d.email)) { onToast('Complete the required fields.'); return; }
    setRows(rs => [...rs, { id: 's' + Date.now(), first: d.first, last: d.last, email: d.email, role: d.role, tenant: d.tenant, active: true }]);
    setResult({ ...d, queued: d.email.indexOf('fail') === -1 });
    setOpen(false); setD({ tenant: '', role: '', first: '', last: '', email: '', phone: '' });
  }
  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 12 }}>
        <button className="af-btn af-btn--primary" onClick={() => { setOpen(true); setResult(null); }}><UI name="plus" size={15} />Create internal user</button>
      </div>
      <div className="ia-wrap">
        <table className="ia-table" style={{ minWidth: 0 }}>
          <thead><tr><th>Name</th><th>Email</th><th>Role</th><th>Tenant</th><th>Status</th><th style={{ textAlign: 'right' }}>Actions</th></tr></thead>
          <tbody>
            {rows.map(r => (
              <tr key={r.id}>
                <td><span className="ia-pt"><span className="ava" style={{ background: window.AfCommon.avaColor(r.first + r.last) }}>{window.AfCommon.initials(r.first, r.last)}</span><b>{r.first} {r.last}</b></span></td>
                <td className="ia-sub">{r.email}</td>
                <td><span className={'ux-role ' + (r.role === 'Staff Supervisor' ? 'sup' : '')}>{r.role}</span></td>
                <td>{r.tenant}</td>
                <td><span className={'lw-active ' + (r.active ? 'on' : 'off')}>{r.active ? 'Active' : 'Deactivated'}</span></td>
                <td style={{ textAlign: 'right' }}>
                  <span style={{ display: 'inline-flex', gap: 6 }}>
                    <button className="ra-rowbtn" title="Send password reset" onClick={() => onToast('Password reset email queued for ' + r.email + '.')}><UI name="refresh" size={14} /></button>
                    <button className="ra-rowbtn" title={r.active ? 'Deactivate' : 'Reactivate'} onClick={() => { setRows(rs => rs.map(x => x.id === r.id ? { ...x, active: !x.active } : x)); onToast(r.active ? 'User deactivated.' : 'User reactivated.'); }}><UI name={r.active ? 'x' : 'check'} size={14} /></button>
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {result && (
        <div className="ux-result">
          <div className={'ux-result__head' + (result.queued ? '' : ' warn')}><UI name={result.queued ? 'check' : 'alert'} size={16} />{result.queued ? 'User created — welcome email queued' : 'User created — welcome email failed to queue'}</div>
          <div className="ux-result__body">
            <div className="ux-result__grid">
              <div><div className="k">Tenant</div><div className="v">{result.tenant}</div></div>
              <div><div className="k">Email</div><div className="v">{result.email}</div></div>
              <div><div className="k">Role</div><div className="v">{result.role}</div></div>
              <div><div className="k">First sign-in</div><div className="v">Must change password</div></div>
            </div>
            {!result.queued && <div className="ra-note warn" style={{ marginTop: 12 }}><span className="i"><UI name="alert" size={15} /></span><span>The account exists, but the temporary-password email didn't reach the queue. Reset the password from this list and hand-deliver the credentials.</span></div>}
          </div>
        </div>
      )}

      {open && (
        <div className="ra-scrim" onClick={() => setOpen(false)}>
          <div className="ra-modal ra-modal--lg" onClick={e => e.stopPropagation()}>
            <div className="ra-modal__head"><h3>Create internal user</h3><button className="ext-iconbtn x" onClick={() => setOpen(false)}><UI name="x" size={17} /></button></div>
            <div className="ra-modal__body">
              <div className="ra-grid">
                <div className="ra-field col-6"><label>Tenant <span className="req">*</span></label>
                  <select className="ra-select" value={d.tenant} onChange={e => setD({ ...d, tenant: e.target.value })}><option value="">Select tenant</option>{U_TENANT_NAMES.map(t => <option key={t}>{t}</option>)}</select>
                  <div className="ra-hint">IT Admin and Staff Supervisor can create users in any tenant.</div>
                </div>
                <div className="ra-field col-6"><label>Role <span className="req">*</span></label>
                  <select className="ra-select" value={d.role} onChange={e => setD({ ...d, role: e.target.value })}><option value="">Select role</option><option>Staff Supervisor</option><option>Intake Staff</option></select>
                  <div className="ra-hint">IT Admin accounts can't be self-created. External roles are invited instead.</div>
                </div>
                <div className="ra-field col-6"><label>First name <span className="req">*</span></label><input className="ra-input" maxLength={64} value={d.first} onChange={e => setD({ ...d, first: e.target.value })} /></div>
                <div className="ra-field col-6"><label>Last name <span className="req">*</span></label><input className="ra-input" maxLength={64} value={d.last} onChange={e => setD({ ...d, last: e.target.value })} /></div>
                <div className="ra-field col-6"><label>Email <span className="req">*</span></label><input className="ra-input" type="email" maxLength={256} placeholder="staff@clinic.example" value={d.email} onChange={e => setD({ ...d, email: e.target.value })} /></div>
                <div className="ra-field col-6"><label>Phone <span className="opt">(optional)</span></label><input className="ra-input" maxLength={20} value={d.phone} onChange={e => setD({ ...d, phone: e.target.value })} /></div>
                <div className="ra-field col-12"><div className="ra-note"><span className="i"><UI name="alert" size={15} /></span><span>The new user gets a one-time temporary password by email and must choose a new one on first sign-in.</span></div></div>
              </div>
            </div>
            <div className="ra-modal__foot"><button className="af-btn af-btn--ghost" onClick={() => setOpen(false)}>Cancel</button><button className="af-btn af-btn--primary" onClick={create}><UI name="check" size={15} />Create user</button></div>
          </div>
        </div>
      )}
    </>
  );
}

/* ---------------- Tenants (IT Admin) ---------------- */
function UxTenants({ onToast }) {
  const [rows, setRows] = useStateU(U_TENANTS0);
  const [edit, setEdit] = useStateU(null);
  const [d, setD] = useStateU({});
  function open(row) { setD(row ? { ...row } : { name: '', sub: '', edition: 'Standard', active: true, adminEmail: '' }); setEdit(row ? row.id : 'new'); }
  function save() {
    if (!d.name.trim() || !d.sub.trim()) { onToast('Name and subdomain are required.'); return; }
    setRows(rs => edit === 'new' ? [...rs, { ...d, id: 't' + Date.now(), users: 1, appts: 0 }] : rs.map(r => r.id === edit ? { ...r, ...d } : r));
    setEdit(null); onToast('Tenant saved.');
  }
  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 12 }}>
        <button className="af-btn af-btn--primary" onClick={() => open(null)}><UI name="plus" size={15} />New tenant</button>
      </div>
      <div className="ia-wrap">
        <table className="ia-table" style={{ minWidth: 0 }}>
          <thead><tr><th>Tenant</th><th>Subdomain</th><th>Edition</th><th>Users</th><th>Appointments</th><th>Status</th><th style={{ textAlign: 'right' }}>Actions</th></tr></thead>
          <tbody>
            {rows.map(r => (
              <tr key={r.id} onClick={() => open(r)}>
                <td><span className="tn" style={{ display: 'flex', alignItems: 'center', gap: 10, fontWeight: 700, color: 'var(--n-900)' }}><span style={{ width: 28, height: 28, borderRadius: 8, background: 'var(--blue-700)', color: '#fff', fontSize: 11, fontWeight: 800, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>{r.name[0]}</span>{r.name}</span></td>
                <td className="ia-sub" style={{ fontFamily: 'var(--font-num)' }}>{r.sub}.caseeval.app</td>
                <td><span className="ux-ed">{r.edition}</span></td>
                <td className="num" style={{ fontFamily: 'var(--font-num)' }}>{r.users}</td>
                <td className="num" style={{ fontFamily: 'var(--font-num)' }}>{r.appts}</td>
                <td><span className={'lw-active ' + (r.active ? 'on' : 'off')}>{r.active ? 'Active' : 'Inactive'}</span></td>
                <td style={{ textAlign: 'right' }} onClick={e => e.stopPropagation()}>
                  <span style={{ display: 'inline-flex', gap: 6 }}>
                    <button className="ra-rowbtn" title="Switch into tenant" onClick={() => onToast('Switching to ' + r.name + '…')}><UI name="chevRight" size={14} /></button>
                    <button className="ra-rowbtn" title="Edit" onClick={() => open(r)}><UI name="doc" size={14} /></button>
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {edit != null && (
        <div className="ra-scrim" onClick={() => setEdit(null)}>
          <div className="ra-modal ra-modal--md" onClick={e => e.stopPropagation()}>
            <div className="ra-modal__head"><h3>{edit === 'new' ? 'New tenant' : 'Edit tenant'}</h3><button className="ext-iconbtn x" onClick={() => setEdit(null)}><UI name="x" size={17} /></button></div>
            <div className="ra-modal__body">
              <div className="ra-grid">
                <div className="ra-field col-6"><label>Practice name <span className="req">*</span></label><input className="ra-input" maxLength={100} value={d.name || ''} onChange={e => setD({ ...d, name: e.target.value })} /></div>
                <div className="ra-field col-6"><label>Subdomain <span className="req">*</span></label>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 7 }}>
                    <input className="ra-input" maxLength={40} value={d.sub || ''} onChange={e => setD({ ...d, sub: e.target.value.toLowerCase() })} />
                    <span style={{ fontSize: 12.5, color: 'var(--n-500)', whiteSpace: 'nowrap' }}>.caseeval.app</span>
                  </div>
                </div>
                <div className="ra-field col-6"><label>Edition</label>
                  <select className="ra-select" value={d.edition || ''} onChange={e => setD({ ...d, edition: e.target.value })}><option>Standard</option><option>Professional</option></select>
                </div>
                {edit === 'new' && <div className="ra-field col-6"><label>Admin email <span className="req">*</span></label><input className="ra-input" type="email" value={d.adminEmail || ''} onChange={e => setD({ ...d, adminEmail: e.target.value })} placeholder="admin@practice.example" /></div>}
                <div className="ra-field col-12">
                  <label className="ra-switch" style={{ marginTop: 4 }}>Active — tenant can sign in and book
                    <input type="checkbox" checked={!!d.active} onChange={e => setD({ ...d, active: e.target.checked })} />
                    <span className="track" />
                  </label>
                </div>
              </div>
            </div>
            <div className="ra-modal__foot"><button className="af-btn af-btn--ghost" onClick={() => setEdit(null)}>Cancel</button><button className="af-btn af-btn--primary" onClick={save}><UI name="check" size={15} />Save</button></div>
          </div>
        </div>
      )}
    </>
  );
}

window.InUsers = InUsers;
