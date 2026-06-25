/* global React */
/* ============================================================
   Internal staff shell — collapsible navy sidebar + topbar, role-aware.
   Roles: itadmin (IT Admin, host-scoped) · supervisor (Staff Supervisor,
   host-scoped) — both can switch tenants. intake (Intake Staff) is
   tenant-scoped and cannot switch.
   ============================================================ */
const { useState: useStateIN } = React;
function INI({ name, size }) { return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size || 18)} />; }

const IN_ROLES = {
  itadmin: { key: 'itadmin', label: 'IT Admin', name: 'Marcus Lee', initials: 'ML', host: true },
  supervisor: { key: 'supervisor', label: 'Staff Supervisor', name: 'Sandra Cole', initials: 'SC', host: false },
  intake: { key: 'intake', label: 'Intake Staff', name: 'Priya Shah', initials: 'PS', host: false },
};

/* Tenant operational nav (Staff Supervisor; IT Admin when switched into a tenant). */
const IN_NAV = [
  { sect: 'Workspace', items: [
    { id: 'dashboard', label: 'Dashboard', icon: 'grid', roles: ['supervisor', 'intake'] },
    { id: 'appointments', label: 'Appointments', icon: 'calendar', roles: ['supervisor', 'intake'], badge: 18 },
    { id: 'change-requests', label: 'Change Requests', icon: 'refresh', roles: ['supervisor'], badge: 7 },
    { id: 'change-logs', label: 'Change Logs', icon: 'clock', roles: ['supervisor'] },
    { id: 'reports', label: 'Reports', icon: 'list', roles: ['supervisor'] },
  ] },
  { sect: 'Scheduling', items: [
    { id: 'availabilities', label: 'Doctor Availabilities', icon: 'calendar', roles: ['supervisor'] },
    { id: 'locations', label: 'Locations', icon: 'map', roles: ['supervisor'] },
    { id: 'wcab', label: 'WCAB Offices', icon: 'map', roles: ['supervisor'] },
  ] },
  { sect: 'Configuration', items: [
    { id: 'appt-types', label: 'Appointment Types', icon: 'list', roles: ['supervisor'] },
    { id: 'appt-statuses', label: 'Appointment Statuses', icon: 'list', roles: ['supervisor'] },
    { id: 'doc-types', label: 'Document Types', icon: 'doc', roles: ['supervisor'] },
    { id: 'languages', label: 'Appointment Languages', icon: 'list', roles: ['supervisor'] },
    { id: 'states', label: 'States', icon: 'map', roles: ['supervisor'] },
  ] },
  { sect: 'People', items: [
    { id: 'patients', label: 'Patients', icon: 'users', roles: ['supervisor', 'intake'] },
    { id: 'applicant-attorneys', label: 'Applicant Attorneys', icon: 'user', roles: ['supervisor'] },
    { id: 'defense-attorneys', label: 'Defense Attorneys', icon: 'user', roles: ['supervisor'] },
    { id: 'claim-examiners', label: 'Claim Examiners', icon: 'user', roles: ['supervisor'] },
  ] },
  { sect: 'Administration', items: [
    { id: 'invite-external', label: 'Users & Access', icon: 'user', roles: ['supervisor', 'intake'] },
    { id: 'identity', label: 'Users & Roles', icon: 'users', roles: ['supervisor'] },
    { id: 'notif-templates', label: 'Notification Templates', icon: 'doc', roles: ['supervisor'] },
    { id: 'settings', label: 'System Parameters', icon: 'settings', roles: ['supervisor'] },
    { id: 'audit', label: 'Audit Logs', icon: 'clock', roles: ['supervisor'] },
  ] },
];

/* IT Admin platform (host / cross-tenant) nav. IT Admin can also switch into a
   tenant for the full operational nav above. */
const IN_NAV_HOST = [
  { sect: 'Platform', items: [{ id: 'dashboard', label: 'Overview', icon: 'grid', roles: ['itadmin'] }] },
  { sect: 'SaaS', items: [
    { id: 'tenants', label: 'Tenants', icon: 'users', roles: ['itadmin'] },
    { id: 'editions', label: 'Editions', icon: 'list', roles: ['itadmin'] },
    { id: 'internal-users', label: 'Internal Users', icon: 'user', roles: ['itadmin'] },
  ] },
  { sect: 'Administration', items: [
    { id: 'identity', label: 'Users & Roles', icon: 'users', roles: ['itadmin'] },
    { id: 'notif-templates', label: 'Notification Templates', icon: 'doc', roles: ['itadmin'] },
    { id: 'settings', label: 'System Parameters', icon: 'settings', roles: ['itadmin'] },
    { id: 'audit', label: 'Audit Logs', icon: 'clock', roles: ['itadmin'] },
  ] },
];

function InternalShell({ roleKey, active, setActive, onToast, children, crumb, contentClass }) {
  const role = IN_ROLES[roleKey];
  const [collapsed, setCollapsed] = useStateIN(false);
  const [acctOpen, setAcctOpen] = useStateIN(false);
  const platform = role.host; // IT Admin shows the platform nav by default ("All tenants")
  const nav = platform ? IN_NAV_HOST : IN_NAV;
  const groups = nav.map(g => ({ ...g, items: g.items.filter(it => it.roles.includes(roleKey)) })).filter(g => g.items.length);
  const activeLabel = (nav.flatMap(g => g.items).find(it => it.id === active) || {}).label || (platform ? 'Overview' : 'Dashboard');
  const canSwitch = roleKey !== 'intake';

  return (
    <div className={'in' + (collapsed ? ' in--collapsed' : '')}>
      <aside className="in-side">
        <div className="in-side__brand">
          <span className="mark">A</span>
          <span className="tt"><b>Appointment Portal</b><span>{platform ? 'Platform administration' : 'Falkinstein Orthopedics'}</span></span>
        </div>
        <nav className="in-nav">
          {groups.map(g => (
            <React.Fragment key={g.sect}>
              <div className="in-sect"><span>{g.sect}</span></div>
              {g.items.map(it => (
                <button key={it.id} className={'in-link' + (active === it.id ? ' on' : '') + (it.badge ? ' has-badge' : '')} data-tip={it.label} onClick={() => setActive(it.id)}>
                  <span className="i"><INI name={it.icon} size={18} /></span>
                  <span className="lbl">{it.label}</span>
                  {it.badge ? <span className="badge">{it.badge}</span> : null}
                  {it.badge ? <span className="pip" /> : null}
                </button>
              ))}
            </React.Fragment>
          ))}
        </nav>
      </aside>

      <div className="in-main">
        <header className="in-top">
          <button className="in-collapse" onClick={() => setCollapsed(!collapsed)} aria-label="Toggle sidebar"><INI name="list" size={18} /></button>
          <div className="in-crumb">{platform ? 'Platform' : 'Home'} <INI name="chevRight" size={13} /> <b>{crumb || activeLabel}</b></div>
          <div className="spacer" />
          <div className={'in-tenant' + (platform ? ' host' : '')} style={canSwitch ? null : { cursor: 'default' }} onClick={() => canSwitch && onToast(platform ? 'Switching tenant…' : 'Switch tenant…')}>
            <span className="mk">{platform ? 'A' : 'F'}</span>
            {platform ? 'All tenants' : 'Falkinstein Orthopedics'}
            {canSwitch && <INI name="chevDown" size={14} />}
          </div>
          <button className="in-iconbtn" onClick={() => onToast('Notifications')} aria-label="Notifications"><INI name="bell" size={18} /><span className="nip">3</span></button>
          {!platform && <button className="af-btn af-btn--primary af-btn--sm" onClick={() => { setActive('add-appt'); onToast('New appointment'); }}><INI name="plus" size={15} />New appointment</button>}
          <div className="in-acctwrap">
            {acctOpen && <div className="in-clickaway" onClick={() => setAcctOpen(false)} />}
            <button className="in-acct" aria-expanded={acctOpen} onClick={() => setAcctOpen(!acctOpen)}>
              <span className="ava" style={{ background: window.AfCommon.avaColor(role.name) }}>{role.initials}</span>
              <span className="who"><b>{role.name}</b><span>{role.label}</span></span>
              <span className="cv"><INI name="chevDown" size={14} /></span>
            </button>
            {acctOpen && (
              <div className="in-acct__pop">
                <a href="#" onClick={e => { e.preventDefault(); setAcctOpen(false); onToast('Opening my account…'); }}><span className="i"><INI name="user" size={16} /></span>My account</a>
                <a href="#" onClick={e => { e.preventDefault(); setAcctOpen(false); setActive('settings'); }}><span className="i"><INI name="settings" size={16} /></span>Settings</a>
                <div className="in-acct__div" />
                <button className="danger" onClick={() => { setAcctOpen(false); onToast('Signing out…'); }}><span className="i"><INI name="logout" size={16} /></span>Sign out</button>
              </div>
            )}
          </div>
        </header>
        <div className={'in-content' + (contentClass ? ' ' + contentClass : '')}>{children}</div>
      </div>
    </div>
  );
}

window.InternalShell = InternalShell;
window.IN_ROLES = IN_ROLES;
