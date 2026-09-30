/* global React */

function AfIcon({ name, size }) {
  return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size)} />;
}

function AfPill({ tone, label }) {
  return (
    <span className={'af-pill pill-' + (tone || 'neutral')}>
      <span className="d" />{label}
    </span>
  );
}

const AVA_COLORS = ['#055495', '#075ca1', '#0a4778', '#2f7cbf', '#1f6e6e', '#5b3ea6', '#82a52a', '#a35a26'];
function avaColor(seed) {
  let h = 0; for (let i = 0; i < seed.length; i++) h = (h * 31 + seed.charCodeAt(i)) >>> 0;
  return AVA_COLORS[h % AVA_COLORS.length];
}
function initials(f, l) { return (f[0] || '') + (l[0] || ''); }

function fmtTime(iso) { return new Date(iso).toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' }); }
function fmtMonth(iso) { return new Date(iso).toLocaleDateString('en-US', { month: 'short' }); }
function fmtDay(iso) { return new Date(iso).getDate(); }
function fmtDateShort(iso) { return new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }); }

/* ---------------- Staff shell ---------------- */
function AfStaffShell({ active, crumb, children, pendingCount }) {
  const main = [
    ['Dashboard', 'grid', 'dashboard', null],
    ['Appointments', 'calendar', 'appts', pendingCount],
    ['Documents', 'doc', 'docs', null],
  ];
  const admin = [
    ['Appointment Types', 'list'],
    ['Locations', 'map'],
    ['Doctors', 'stetho'],
    ['Patients', 'users'],
    ['Settings', 'settings'],
  ];
  return (
    <div className="af">
      <div className="af-app">
        <aside className="af-side">
          <div className="af-side__brand">
            <span className="mark">A</span>
            <span className="tt"><b>Appointment Portal</b><span>Falkinstein Orthopedics</span></span>
          </div>
          <div className="af-side__sect">Workspace</div>
          <nav className="af-nav">
            {main.map(([t, ic, k, n]) => (
              <a key={k} href="#" onClick={e => e.preventDefault()} className={active === k ? 'active' : ''}>
                <AfIcon name={ic} size={18} />{t}
                {n ? <span className="badge-n">{n}</span> : null}
              </a>
            ))}
          </nav>
          <div className="af-side__sect">Administration</div>
          <nav className="af-nav">
            {admin.map(([t, ic]) => (
              <a key={t} href="#" onClick={e => e.preventDefault()}><AfIcon name={ic} size={18} />{t}</a>
            ))}
          </nav>
          <div className="af-side__foot">
            <div className="af-side__user">
              <span className="ava">SC</span>
              <span className="nm"><b>Sandra Cole</b><span>Clinic Staff</span></span>
            </div>
          </div>
        </aside>

        <div className="af-main">
          <header className="af-topbar">
            <div className="crumb">Home <AfIcon name="chevRight" size={13} /> <b>{crumb}</b></div>
            <div className="af-topbar__search">
              <AfIcon name="search" size={16} />
              <input placeholder="Search appointments, patients, claims…" />
            </div>
            <div className="spacer" />
            <button className="af-iconbtn" title="Notifications"><AfIcon name="bell" size={18} /><span className="pip" /></button>
            <button className="af-iconbtn" title="Help"><AfIcon name="settings" size={18} /></button>
          </header>
          {children}
        </div>
      </div>
    </div>
  );
}

window.AfCommon = { AfIcon, AfPill, AfStaffShell, avaColor, initials, fmtTime, fmtMonth, fmtDay, fmtDateShort, fmtDateShort };
