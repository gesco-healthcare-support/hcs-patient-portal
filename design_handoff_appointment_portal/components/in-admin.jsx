/* global React */
/* ============================================================
   Group 6 — Admin hub. Notification Templates (split list + editor,
   variable chips, preview, send-test) · System Parameters (grouped
   settings w/ units) · Users & Roles (permission matrix) · Audit Logs
   (searchable + expandable). Renders inside InternalShell.
   ============================================================ */
const { useState: useStateA, useMemo: useMemoA } = React;
function AI({ name, size }) { return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size || 18)} />; }

/* ---------------- seeds ---------------- */
const NT_VARS = ['{{PatientName}}', '{{ConfirmationNumber}}', '{{AppointmentDate}}', '{{Location}}', '{{TenantName}}', '{{Link}}'];
const NT_SAMPLE = { '{{PatientName}}': 'Maria Gonzalez', '{{ConfirmationNumber}}': 'PQ-24817', '{{AppointmentDate}}': 'Jun 16, 2026 · 9:30 AM', '{{Location}}': 'Los Angeles — Wilshire', '{{TenantName}}': 'Falkinstein Orthopedics', '{{Link}}': 'https://falkinstein.caseeval.app/…' };
const NT_T0 = [
  { id: 'n1', code: 'InviteExternalUser', type: 'Account', active: true, ovr: false, desc: 'One-time registration link', subject: 'You\u2019re invited to {{TenantName}}', body: 'Hello {{PatientName}},\n\n{{TenantName}} has invited you to the appointment portal. Use the link below to register — it expires in 7 days.\n\n{{Link}}', sms: '{{TenantName}}: register at {{Link}} (expires in 7 days).' },
  { id: 'n2', code: 'AppointmentApproved', type: 'Appointment', active: true, ovr: true, desc: 'Sent when staff approve a request', subject: 'Appointment {{ConfirmationNumber}} approved', body: 'Hello {{PatientName}},\n\nYour appointment {{ConfirmationNumber}} was approved for {{AppointmentDate}} at {{Location}}.\n\nPlease arrive 15 minutes early with a photo ID.', sms: 'Approved: {{ConfirmationNumber}} on {{AppointmentDate}} at {{Location}}.' },
  { id: 'n3', code: 'AppointmentRejected', type: 'Appointment', active: true, ovr: false, desc: 'Sent when staff reject a request', subject: 'Appointment request {{ConfirmationNumber}} — action needed', body: 'Hello {{PatientName}},\n\nYour request {{ConfirmationNumber}} was not approved. Sign in to review the reason and re-submit.\n\n{{Link}}', sms: '' },
  { id: 'n4', code: 'RescheduleRequested', type: 'Appointment', active: true, ovr: false, desc: 'Change-request notice to staff/parties', subject: 'Reschedule requested for {{ConfirmationNumber}}', body: 'A reschedule was requested for {{ConfirmationNumber}} ({{PatientName}}). Review it in the change-requests queue.', sms: '' },
  { id: 'n5', code: 'ConsentRequest', type: 'Appointment', active: true, ovr: false, desc: 'Opposing-counsel consent link', subject: 'Consent needed — {{ConfirmationNumber}}', body: 'A change to appointment {{ConfirmationNumber}} needs your agreement. Respond here:\n\n{{Link}}', sms: '' },
  { id: 'n6', code: 'AppointmentReminder', type: 'Reminder', active: false, ovr: false, desc: 'Pre-visit reminder', subject: 'Reminder: {{ConfirmationNumber}} on {{AppointmentDate}}', body: 'Hello {{PatientName}},\n\nThis is a reminder of your evaluation {{ConfirmationNumber}} on {{AppointmentDate}} at {{Location}}.', sms: 'Reminder: {{ConfirmationNumber}} on {{AppointmentDate}}.' },
];

const SP_GROUPS = [
  ['Booking windows', 'calendar', 'tint-blue', [
    ['appointmentLeadTime', 'Minimum lead time', 'days', 2, 'Earliest a new request may be scheduled.'],
    ['appointmentMaxTimePQME', 'Max window — Panel QME', 'days', 60, ''],
    ['appointmentMaxTimeAME', 'Max window — AME', 'days', 90, ''],
    ['appointmentMaxTimeOTHER', 'Max window — other types', 'days', 120, ''],
    ['appointmentMaxTimeInternal', 'Max window — internal booking', 'days', 180, ''],
  ]],
  ['Cancellation & auto-cancel', 'x', 'tint-red', [
    ['appointmentCancelTime', 'Cancel cutoff', 'hours before', 48, 'External cancel requests blocked inside this window.'],
    ['autoCancelCutoffTime', 'Auto-cancel unconfirmed after', 'hours', 72, ''],
  ]],
  ['Deadlines & reminders', 'clock', 'tint-amber', [
    ['appointmentDueDays', 'Decision deadline', 'days', 3, 'Days staff have to decide a request after submission. Legal limit is 5 — 3 leaves a 2-day buffer.'],
    ['appointmentDurationTime', 'Default slot duration', 'minutes', 60, ''],
    ['reminderCutoffTime', 'Reminder sent before visit', 'hours', 48, ''],
    ['pendingAppointmentOverDueNotificationDays', 'Pending-overdue alert after', 'days', 5, ''],
    ['jointDeclarationUploadCutoffDays', 'Joint-declaration upload cutoff', 'days', 10, ''],
  ]],
];

const PM_ROLES = [
  { key: 'itadmin', name: 'IT Admin', kind: 'Internal', locked: true },
  { key: 'supervisor', name: 'Staff Supervisor', kind: 'Internal' },
  { key: 'intake', name: 'Intake Staff', kind: 'Internal' },
  { key: 'patient', name: 'Patient', kind: 'External' },
  { key: 'aa', name: 'Applicant Attorney', kind: 'External' },
  { key: 'da', name: 'Defense Attorney', kind: 'External' },
  { key: 'ce', name: 'Claim Examiner', kind: 'External' },
];
const PM_GROUPS = [
  ['Appointments', ['View', 'Create', 'Edit', 'Delete', 'Approve', 'Reject', 'Reschedule', 'Cancel']],
  ['Change requests', ['View', 'Approve', 'Reject']],
  ['Scheduling', ['Manage availabilities', 'Manage locations', 'Manage WCAB offices']],
  ['Configuration', ['Manage lookups', 'Field configuration']],
  ['People', ['View', 'Manage']],
  ['Users & access', ['Invite external users', 'Create internal users', 'Manage tenants']],
  ['Reports', ['View', 'Export']],
];
const PM_DEFAULTS = {
  itadmin: 'all',
  supervisor: ['Appointments:View', 'Appointments:Create', 'Appointments:Edit', 'Appointments:Delete', 'Appointments:Approve', 'Appointments:Reject', 'Appointments:Reschedule', 'Appointments:Cancel', 'Change requests:View', 'Change requests:Approve', 'Change requests:Reject', 'Scheduling:Manage availabilities', 'Scheduling:Manage locations', 'Scheduling:Manage WCAB offices', 'Configuration:Manage lookups', 'Configuration:Field configuration', 'People:View', 'People:Manage', 'Users & access:Invite external users', 'Users & access:Create internal users', 'Reports:View', 'Reports:Export'],
  intake: ['Appointments:View', 'Appointments:Create', 'Appointments:Reschedule', 'Appointments:Cancel', 'People:View', 'People:Manage', 'Users & access:Invite external users'],
  patient: ['Appointments:View', 'Appointments:Create'],
  aa: ['Appointments:View', 'Appointments:Create', 'People:View'],
  da: ['Appointments:View', 'People:View'],
  ce: ['Appointments:View', 'People:View'],
};

const AU_LOGS = [
  { id: 'a1', t: 'Jun 11 · 10:42:18', user: 'scole@falkinstein.com', m: 'POST', url: '/api/app/appointments/approve/PQ-24817', s: 200, ms: 312, ip: '73.92.14.8', ua: 'Chrome 126 · macOS', tenant: 'Falkinstein' },
  { id: 'a2', t: 'Jun 11 · 10:38:02', user: 'mgonzalez@aol.com', m: 'POST', url: '/api/app/appointment-documents', s: 200, ms: 1240, ip: '98.10.77.2', ua: 'Safari 19 · iOS', tenant: 'Falkinstein' },
  { id: 'a3', t: 'Jun 11 · 10:31:55', user: 'pshah@falkinstein.com', m: 'PUT', url: '/api/app/patients/8c2f…', s: 200, ms: 188, ip: '73.92.14.8', ua: 'Edge 126 · Windows', tenant: 'Falkinstein' },
  { id: 'a4', t: 'Jun 11 · 10:19:44', user: 'dbrooks@brookslaw.com', m: 'POST', url: '/api/app/appointment-change-requests', s: 403, ms: 45, ip: '64.30.21.99', ua: 'Chrome 126 · Windows', tenant: 'Falkinstein' },
  { id: 'a5', t: 'Jun 11 · 09:58:31', user: 'anonymous', m: 'GET', url: '/api/app/public/document-upload/validate', s: 429, ms: 12, ip: '190.4.55.1', ua: 'Chrome 125 · Android', tenant: '—' },
  { id: 'a6', t: 'Jun 11 · 09:46:09', user: 'mlee@host.caseeval.app', m: 'DELETE', url: '/api/app/doctor-availabilities/77b1…', s: 500, ms: 2210, ip: '12.44.9.20', ua: 'Chrome 126 · macOS', tenant: 'Bay Area Spine' },
];

/* ---------------- Notification Templates ---------------- */
function NtPage({ onToast }) {
  const [rows, setRows] = useStateA(NT_T0);
  const [sel, setSel] = useStateA('n1');
  const [q, setQ] = useStateA('');
  const [type, setType] = useStateA('');
  const t = rows.find(r => r.id === sel);
  const shown = rows.filter(r => (!type || r.type === type) && (!q || r.code.toLowerCase().includes(q.toLowerCase())));
  function patch(p) { setRows(rs => rs.map(r => r.id === sel ? { ...r, ...p } : r)); }
  const preview = useMemoA(() => {
    let s = t ? t.subject : '', b = t ? t.body : '';
    NT_VARS.forEach(v => { s = s.split(v).join(NT_SAMPLE[v]); b = b.split(v).join('\u0001' + NT_SAMPLE[v] + '\u0002'); });
    return { s, b };
  }, [t]);

  return (
    <>
      <div className="ia-toolbar">
        <div className="ia-search" style={{ maxWidth: 280 }}><AI name="search" size={16} /><input placeholder="Search templates…" value={q} onChange={e => setQ(e.target.value)} /></div>
        <select className="ia-input" style={{ width: 160 }} value={type} onChange={e => setType(e.target.value)}>
          <option value="">All types</option><option>Account</option><option>Appointment</option><option>Reminder</option>
        </select>
      </div>
      <div className="nt">
        <div className="nt-list">
          {shown.map(r => (
            <button key={r.id} className="nt-item" data-on={sel === r.id} onClick={() => setSel(r.id)}>
              <span className={'dot ' + (r.active ? 'on' : 'off')} />
              <span className="tx"><b>{r.code}</b><span>{r.type} · {r.desc}</span></span>
              {r.ovr && <span className="nt-ovr">Customized</span>}
            </button>
          ))}
        </div>

        {t && (
          <div className="ra-card">
            <div className="ra-card__head">
              <span className="ic tint-blue"><AI name="doc" size={18} /></span>
              <div><h3>{t.code}</h3><p>{t.type} · {t.ovr ? 'Customized for this tenant' : 'Host default'}</p></div>
              <div className="right" style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
                <label className="ra-switch">Active
                  <input type="checkbox" checked={t.active} onChange={e => { patch({ active: e.target.checked }); onToast(e.target.checked ? 'Template activated.' : 'Template deactivated.'); }} />
                  <span className="track" />
                </label>
              </div>
            </div>
            <div className="ra-card__body">
              <div className="ra-grid">
                <div className="ra-field col-12"><label>Subject</label><input className="ra-input" value={t.subject} onChange={e => patch({ subject: e.target.value })} /></div>
                <div className="ra-field col-12"><label>Insert variable</label><div className="nt-vars">{NT_VARS.map(v => <button key={v} className="nt-var" onClick={() => { patch({ body: t.body + ' ' + v }); onToast(v + ' inserted.'); }}>{v}</button>)}</div></div>
                <div className="ra-field col-12"><label>Email body</label><textarea className="ra-input" rows={7} value={t.body} onChange={e => patch({ body: e.target.value })} /></div>
                <div className="ra-field col-12"><label>SMS body <span className="opt">(optional)</span></label><textarea className="ra-input" rows={2} value={t.sms} onChange={e => patch({ sms: e.target.value })} /></div>
                <div className="ra-field col-12">
                  <label>Preview <span className="opt">(sample values)</span></label>
                  <div className="nt-preview">
                    <div className="sub">{preview.s}</div>
                    <div className="bd">{preview.b.split('\u0001').map((part, i) => i === 0 ? part : (<React.Fragment key={i}><b>{part.split('\u0002')[0]}</b>{part.split('\u0002')[1]}</React.Fragment>))}</div>
                  </div>
                </div>
              </div>
              <div className="mp-editfoot">
                <button className="af-btn af-btn--ghost" onClick={() => onToast('Test email queued to scole@falkinstein.com.')}><AI name="arrowUp" size={15} />Send test</button>
                <button className="af-btn af-btn--primary" onClick={() => { patch({ ovr: true }); onToast('Template saved.'); }}><AI name="check" size={15} />Save template</button>
              </div>
            </div>
          </div>
        )}
      </div>
    </>
  );
}

/* ---------------- System Parameters ---------------- */
function SpPage({ onToast }) {
  const [v, setV] = useStateA(() => { const o = {}; SP_GROUPS.forEach(([, , , flds]) => flds.forEach(([k, , , dv]) => o[k] = dv)); return { ...o, ccEmailIds: 'records@falkinstein.com', isCustomField: false }; });
  return (
    <>
      {SP_GROUPS.map(([title, ic, tint, flds]) => (
        <div className="sp-group" key={title}>
          <div className="sp-group__head"><span className={'ic ' + tint}><AI name={ic} size={17} /></span><h3>{title}</h3></div>
          <div className="sp-group__body">
            {flds.map(([k, lbl, unit, , hint]) => (
              <div className="sp-field" key={k}>
                <label>{lbl}</label>
                <div className="row"><input type="number" min="0" value={v[k]} onChange={e => setV({ ...v, [k]: e.target.value })} /><span className="unit">{unit}</span></div>
                {hint && <div className="hint">{hint}</div>}
              </div>
            ))}
          </div>
        </div>
      ))}
      <div className="sp-group">
        <div className="sp-group__head"><span className="ic tint-teal"><AI name="bell" size={17} /></span><h3>Notifications</h3></div>
        <div className="sp-group__body" style={{ gridTemplateColumns: '1fr' }}>
          <div className="sp-field">
            <label>CC every appointment email to</label>
            <input type="text" value={v.ccEmailIds} onChange={e => setV({ ...v, ccEmailIds: e.target.value })} placeholder="comma-separated emails" style={{ fontFamily: 'var(--font)' }} />
            <div className="hint">Comma-separated. Leave blank for none.</div>
          </div>
          <label className="ra-switch" style={{ alignSelf: 'flex-start' }}>Enable custom fields on the booking form
            <input type="checkbox" checked={v.isCustomField} onChange={e => setV({ ...v, isCustomField: e.target.checked })} />
            <span className="track" />
          </label>
        </div>
      </div>
      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
        <button className="af-btn af-btn--ghost" onClick={() => onToast('Reverted to saved values.')}>Revert</button>
        <button className="af-btn af-btn--primary" onClick={() => onToast('System parameters saved for Falkinstein Orthopedics.')}><AI name="check" size={15} />Save parameters</button>
      </div>
    </>
  );
}

/* ---------------- Users & Roles ---------------- */
function RolesPage({ onToast }) {
  const [sel, setSel] = useStateA('supervisor');
  const [grants, setGrants] = useStateA(() => {
    const g = {};
    PM_ROLES.forEach(r => {
      const def = PM_DEFAULTS[r.key];
      const set = new Set();
      PM_GROUPS.forEach(([grp, perms]) => perms.forEach(p => { if (def === 'all' || (def || []).includes(grp + ':' + p)) set.add(grp + ':' + p); }));
      g[r.key] = set;
    });
    return g;
  });
  const role = PM_ROLES.find(r => r.key === sel);
  function toggle(key) {
    if (role.locked) return;
    setGrants(g => { const s = new Set(g[sel]); s.has(key) ? s.delete(key) : s.add(key); return { ...g, [sel]: s }; });
  }
  return (
    <div className="pm">
      <nav className="cf-rail" style={{ position: 'static' }}>
        {['Internal', 'External'].map(kind => (
          <React.Fragment key={kind}>
            <div style={{ fontSize: 10.5, textTransform: 'uppercase', letterSpacing: '.08em', color: 'var(--n-400)', fontWeight: 700, padding: '10px 12px 4px' }}>{kind}</div>
            {PM_ROLES.filter(r => r.kind === kind).map(r => (
              <button key={r.key} className="cf-railitem" data-on={sel === r.key} onClick={() => setSel(r.key)}>
                <span className="i"><AI name="user" size={15} /></span>{r.name}
                <span className="cnt">{grants[r.key].size}</span>
              </button>
            ))}
          </React.Fragment>
        ))}
      </nav>
      <div>
        <div className="ia-head" style={{ marginBottom: 12 }}>
          <div><h1 style={{ fontSize: 18 }}>{role.name} permissions</h1><p>{role.locked ? 'System role — all permissions granted, not editable.' : 'Toggle what this role can do. Changes apply on next sign-in.'}</p></div>
          {!role.locked && <button className="af-btn af-btn--primary" onClick={() => onToast('Permissions saved for ' + role.name + '.')}><AI name="check" size={15} />Save</button>}
        </div>
        <div className="pm-matrix">
          {PM_GROUPS.map(([grp, perms]) => (
            <div className="pm-group" key={grp}>
              <h4>{grp}</h4>
              <div className="pm-perms">
                {perms.map(p => {
                  const key = grp + ':' + p;
                  return (
                    <label className="pm-perm" key={p}>
                      <input type="checkbox" checked={grants[sel].has(key)} disabled={role.locked} onChange={() => toggle(key)} />
                      <span>{p}</span>
                    </label>
                  );
                })}
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

/* ---------------- Audit Logs ---------------- */
function AuditPage({ onToast }) {
  const [q, setQ] = useStateA('');
  const [m, setM] = useStateA('');
  const [open, setOpen] = useStateA({});
  const shown = AU_LOGS.filter(l => (!m || l.m === m) && (!q || l.user.includes(q.toLowerCase()) || l.url.toLowerCase().includes(q.toLowerCase())));
  const sCls = s => s >= 500 ? 's5' : s >= 400 ? 's4' : 's2';
  const mCls = mm => ({ GET: 'get', POST: 'post', PUT: 'put', DELETE: 'del' })[mm];
  return (
    <>
      <div className="ia-toolbar">
        <div className="ia-search"><AI name="search" size={16} /><input placeholder="Search by user or URL…" value={q} onChange={e => setQ(e.target.value)} /></div>
        <select className="ia-input" style={{ width: 130 }} value={m} onChange={e => setM(e.target.value)}>
          <option value="">All methods</option><option>GET</option><option>POST</option><option>PUT</option><option>DELETE</option>
        </select>
        <button className="af-btn af-btn--ghost" onClick={() => onToast('Exporting audit CSV…')}><AI name="arrowDown" size={15} />Export</button>
      </div>
      <div className="ia-wrap">
        <table className="ia-table" style={{ minWidth: 0 }}>
          <thead><tr><th style={{ width: 34 }}></th><th>Time</th><th>User</th><th>Action</th><th>Method</th><th>Status</th><th>Duration</th></tr></thead>
          <tbody>
            {shown.map(l => (
              <React.Fragment key={l.id}>
                <tr onClick={() => setOpen(o => ({ ...o, [l.id]: !o[l.id] }))}>
                  <td><span style={{ color: 'var(--n-400)', display: 'inline-flex', transform: open[l.id] ? 'rotate(90deg)' : 'none', transition: 'transform .15s' }}><AI name="chevRight" size={15} /></span></td>
                  <td className="ia-sub" style={{ fontFamily: 'var(--font-num)', color: 'var(--n-800)' }}>{l.t}</td>
                  <td><b style={{ color: 'var(--n-900)' }}>{l.user}</b></td>
                  <td className="ia-sub" style={{ fontFamily: 'var(--font-num)' }}>{l.url}</td>
                  <td><span className={'au-chip ' + mCls(l.m)}>{l.m}</span></td>
                  <td><span className={'au-chip ' + sCls(l.s)}>{l.s}</span></td>
                  <td className="ia-sub" style={{ fontFamily: 'var(--font-num)' }}>{l.ms} ms</td>
                </tr>
                {open[l.id] && (
                  <tr><td colSpan="7" style={{ padding: 0 }}>
                    <div className="au-detail">
                      <div><div className="k">IP address</div><div className="v">{l.ip}</div></div>
                      <div><div className="k">Client</div><div className="v">{l.ua}</div></div>
                      <div><div className="k">Tenant</div><div className="v">{l.tenant}</div></div>
                      <div><div className="k">Result</div><div className="v">{l.s < 400 ? 'Success' : l.s < 500 ? 'Denied / throttled' : 'Server error'}</div></div>
                    </div>
                  </td></tr>
                )}
              </React.Fragment>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

/* ---------------- hub ---------------- */
const A_SECTIONS = [
  ['templates', 'Notification Templates', 'doc'],
  ['params', 'System Parameters', 'settings'],
  ['roles', 'Users & Roles', 'users'],
  ['audit', 'Audit Logs', 'clock'],
];
function InAdmin({ onToast }) {
  const [section, setSection] = useStateA('templates');
  return (
    <>
      <div className="ia-head"><div><h1>Administration</h1><p>Templates, parameters, roles, and audit for this tenant.</p></div></div>
      <div className="cf">
        <nav className="cf-rail">
          {A_SECTIONS.map(([k, lbl, ic]) => (
            <button key={k} className="cf-railitem" data-on={section === k} onClick={() => setSection(k)}>
              <span className="i"><AI name={ic} size={16} /></span>{lbl}
            </button>
          ))}
        </nav>
        <div>
          {section === 'templates' && <NtPage onToast={onToast} />}
          {section === 'params' && <SpPage onToast={onToast} />}
          {section === 'roles' && <RolesPage onToast={onToast} />}
          {section === 'audit' && <AuditPage onToast={onToast} />}
        </div>
      </div>
    </>
  );
}

window.InAdmin = InAdmin;
