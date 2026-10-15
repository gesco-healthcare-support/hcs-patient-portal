/* global React */
const { useState: useStateAf, useMemo: useMemoAf, useEffect: useEffectAf } = React;
const { AfIcon: EIcon, AfPill: EPill, avaColor: eAva, initials: eInit, fmtTime: eTime, fmtMonth: eMonth, fmtDay: eDay, fmtDateShort: eDate } = window.AfCommon;

/* status → segment bucket — aligned to the 5-status model
   (Pending · Approved · Rescheduled · Cancelled · Rejected) */
function bucketOf(tone, sid) {
  if (sid === 2) return 'approved';
  if (sid === 3) return 'rejected';
  if (sid === 5 || sid === 6 || sid === 13) return 'cancelled';
  if (sid === 12) return 'rescheduled';
  if (sid === 14) return 'info';
  return 'pending';
}

/* ----------------------------------------------------------------
   Top navbar — logo slot + notifications + help + account dropdown
   ---------------------------------------------------------------- */
function ExtNav({ role, onToast, onOpenQuery, onOpenProfile }) {
  const [menu, setMenu] = useStateAf(null); // 'notif' | 'acct' | null
  const [notifs, setNotifs] = useStateAf(window.EXT.NOTIFS);
  const unread = notifs.filter(n => n.unread).length;

  useEffectAf(() => {
    if (!menu) return;
    function onKey(e) { if (e.key === 'Escape') setMenu(null); }
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [menu]);

  const tones = {
    approved: 'tint-green', pending: 'tint-amber', info: 'tint-blue',
    purple: 'tint-purple', teal: 'tint-teal', rejected: 'tint-red',
  };

  return (
    <header className="ext-nav">
      <div className="ext-nav__in">
        {/* tenant logo slot — swapped per tenant at runtime */}
        <a className="ext-brand" href="#" onClick={e => e.preventDefault()} aria-label="Home">
          <img src="assets/header-logo.png" alt={role.org || 'Clinic'} />
          <span className="ext-brand__div" />
          <span className="ext-brand__tag"><b>Appointment Portal</b><span>Patient &amp; case portal</span></span>
        </a>

        <div className="ext-nav__spacer" />

        <div className="ext-nav__actions">
          {/* notifications */}
          <div style={{ position: 'relative' }}>
            <button
              className="ext-iconbtn" aria-label="Notifications" aria-expanded={menu === 'notif'}
              onClick={() => setMenu(menu === 'notif' ? null : 'notif')}
            >
              <EIcon name="bell" size={19} />
              {unread > 0 && <span className="nipple">{unread}</span>}
            </button>
            {menu === 'notif' && (
              <>
                <div className="ext-clickaway" onClick={() => setMenu(null)} />
                <div className="ext-pop ext-pop--notif" role="menu">
                  <div className="ext-notif-head">
                    <b>Notifications</b>
                    <button onClick={() => setNotifs(notifs.map(n => ({ ...n, unread: false })))}>Mark all read</button>
                  </div>
                  <div className="ext-notif-list">
                    {notifs.map(n => (
                      <div key={n.id} className={'ext-notif-item' + (n.unread ? ' unread' : '')}
                        onClick={() => { setNotifs(notifs.map(x => x.id === n.id ? { ...x, unread: false } : x)); onToast('Opening: ' + n.title); }}>
                        <span className={'ic ' + (tones[n.tone] || 'tint-slate')}><EIcon name={n.icon} size={17} /></span>
                        <span className="tx">
                          <b>{n.title}</b>
                          <p>{n.body}</p>
                          <span className="tm">{n.time}</span>
                        </span>
                        {n.unread && <span className="dot" />}
                      </div>
                    ))}
                  </div>
                </div>
              </>
            )}
          </div>

          {/* help */}
          <button className="ext-iconbtn" aria-label="Help" title="Help &amp; support" onClick={onOpenQuery}>
            <EIcon name="help" size={19} />
          </button>

          {/* account */}
          <div style={{ position: 'relative' }}>
            <button className="ext-acct" aria-expanded={menu === 'acct'} onClick={() => setMenu(menu === 'acct' ? null : 'acct')}>
              <span className="ava" style={{ background: eAva(role.name) }}>{eInit(role.first, role.name.split(' ')[1] || role.label)}</span>
              <span className="who"><b>{role.name}</b><span>{role.label}</span></span>
              <span className="cv"><EIcon name="chevDown" size={15} /></span>
            </button>
            {menu === 'acct' && (
              <>
                <div className="ext-clickaway" onClick={() => setMenu(null)} />
                <div className="ext-pop ext-pop--acct" role="menu">
                  <div className="ext-acct-head">
                    <span className="ava">{eInit(role.first, role.name.split(' ')[1] || role.label)}</span>
                    <span className="meta">
                      <b>{role.name}</b>
                      <span className="em">{role.email}</span>
                      <span className="rl">{role.label}</span>
                    </span>
                  </div>
                  <div className="ext-menu">
                    <a href="#" onClick={e => { e.preventDefault(); setMenu(null); onOpenProfile(); }}><span className="i"><EIcon name="user" size={17} /></span>My profile</a>
                    <a href="My Documents - Redesign.html"><span className="i"><EIcon name="doc" size={17} /></span>My documents</a>
                    <a href="#" onClick={e => { e.preventDefault(); setMenu(null); onOpenQuery(); }}><span className="i"><EIcon name="help" size={17} /></span>Help &amp; support</a>
                    <div className="ext-menu__div" />
                    {role.org && <div className="ext-tenant-row"><span className="i"><EIcon name="map" size={15} /></span>{role.org}</div>}
                    <button className="danger" onClick={() => { setMenu(null); onToast('Signing out…'); }}><span className="i"><EIcon name="logout" size={17} /></span>Sign out</button>
                  </div>
                </div>
              </>
            )}
          </div>
        </div>
      </div>
    </header>
  );
}

/* ----------------------------------------------------------------
   Hero + stat strip
   ---------------------------------------------------------------- */
function ExtHero({ role }) {
  return (
    <div className="ext-hero">
      <div className="ext-hero__in">
        <div>
          <h1>Welcome back, {role.first}</h1>
          <p>{role.heroSub}</p>
        </div>
      </div>
    </div>
  );
}

/* ----------------------------------------------------------------
   Quick actions (role-aware)
   ---------------------------------------------------------------- */
function ExtActions({ role, onToast }) {
  const list = [];
  if (role.canBook) list.push(['Request an Appointment', 'Schedule a new QME or AME evaluation', 'calendar', 'tint-blue', () => onToast('Opening appointment request…')]);
  if (role.canReeval) list.push(['Request a Re-evaluation', 'Follow-up on a previous appointment', 'refresh', 'tint-green', () => onToast('Opening re-evaluation request…')]);
  if (list.length === 0) return null;

  return (
    <div className="ext-wrap">
      <div className="ext-actions">
        {list.map(([t, s, ic, tint, fn]) => (
          <button key={t} className="ext-action" onClick={fn}>
            <span className={'ext-action__ic ' + tint}><EIcon name={ic} size={22} /></span>
            <span className="ext-action__tx"><b>{t}</b><span>{s}</span></span>
            <span className="ext-action__go"><EIcon name="chevRight" size={20} /></span>
          </button>
        ))}
      </div>
    </div>
  );
}

window.ExtAfterParts = { ExtNav, ExtHero, ExtActions, bucketOf, EIcon, EPill, eAva, eInit, eTime, eMonth, eDay, eDate };
