/* global React */
const { useState: useStateD } = React;
const { AfIcon, AfStaffShell } = window.AfCommon;

function Kpi({ icon, tint, label, num, foot, trend, trendUp, click, variant, onClick }) {
  return (
    <div className={'af-kpi' + (click ? ' click' : '') + (variant ? ' ' + variant : '')} onClick={onClick} role={click ? 'button' : undefined}>
      <div className="af-kpi__top">
        <span className={'af-kpi__ic ' + tint}><AfIcon name={icon} size={21} /></span>
        {trend != null && <span className={'af-trend ' + (trendUp ? 'up' : 'down')}><AfIcon name={trendUp ? 'arrowUp' : 'arrowDown'} size={12} />{trend}</span>}
      </div>
      <div className="af-kpi__lbl">{label}</div>
      <div className="af-kpi__num">{num}</div>
      {foot && <div className="af-kpi__foot">{foot}</div>}
    </div>
  );
}

function AfterDashboard() {
  const c = window.MOCK.counters;
  const [toast, setToast] = useStateD(null);
  function go(msg) { setToast(msg); clearTimeout(window.__td); window.__td = setTimeout(() => setToast(null), 2400); }

  return (
    <AfStaffShell active="dashboard" crumb="Dashboard" pendingCount={c.pendingRequests}>
      <div className="af-content fade-in">
        <div className="af-page-head">
          <div>
            <h1 className="af-h1">Good morning, Sandra</h1>
            <p className="af-sub">Friday, June 5, 2026 · Here's what needs your attention today.</p>
          </div>
          <button className="af-btn af-btn--primary af-btn--lg" onClick={() => go('Opening new appointment…')}>
            <AfIcon name="plus" size={17} />New appointment
          </button>
        </div>

        {/* Needs attention */}
        <section className="af-kpi-sect">
          <div className="af-sect-head">
            <h3>Needs your attention</h3><span className="ln" />
          </div>
          <div className="af-grid af-grid--3">
            <Kpi icon="inbox" tint="tint-amber" variant="af-kpi--warn" label="Pending requests" num={c.pendingRequests}
              foot={<><span className="af-trend up" style={{ background: 'transparent', color: 'var(--st-pending-fg)', padding: 0 }}>Awaiting review</span></>}
              click onClick={() => go('Filtering appointments → Pending')} />
            <Kpi icon="refresh" tint="tint-blue" label="Pending change requests" num={c.pendingChangeRequests}
              foot={<span>Reschedule &amp; cancellation asks</span>}
              click onClick={() => go('Opening change requests')} />
            <Kpi icon="alert" tint="tint-red" variant="af-kpi--alert" label="Decision deadline at risk" num={c.requestsApproachingLegalDeadline}
              foot={<span style={{ color: 'var(--st-rejected-fg)', fontWeight: 600 }}>Decide within 3 days of request</span>}
              click onClick={() => go('Filtering by decision deadline')} />
          </div>
        </section>

        {/* This week */}
        <section className="af-kpi-sect">
          <div className="af-sect-head">
            <h3>This week</h3><span className="ln" />
          </div>
          <div className="af-grid af-grid--4">
            <Kpi icon="check" tint="tint-green" label="Approved" num={c.approvedThisWeek} trend="+12%" trendUp click onClick={() => go('Filtering → Approved')} />
            <Kpi icon="x" tint="tint-red" label="Rejected" num={c.rejectedThisWeek} trend="-3%" trendUp={false} click onClick={() => go('Filtering → Rejected')} />
            <Kpi icon="calendar" tint="tint-amber" label="Rescheduled" num={c.rescheduledThisMonth} click onClick={() => go('Filtering → Rescheduled')} />
            <Kpi icon="inbox" tint="tint-slate" label="Cancelled" num={c.cancelledThisWeek} click onClick={() => go('Filtering → Cancelled')} />
          </div>
        </section>

        {/* Today on the floor */}
        <section className="af-kpi-sect">
          <div className="af-sect-head">
            <h3>Today on the floor</h3><span className="ln" />
          </div>
          <div className="af-grid af-grid--4">
            <Kpi icon="user" tint="tint-blue" label="Checked in" num={c.checkedInToday} click onClick={() => go('Checked-in patients')} />
            <Kpi icon="logout" tint="tint-teal" label="Checked out" num={c.checkedOutToday} click onClick={() => go('Checked-out patients')} />
            <Kpi icon="clock" tint="tint-slate" label="No-shows (month)" num={c.noShowThisMonth} click onClick={() => go('No-show report')} />
            <Kpi icon="money" tint="tint-purple" label="Billed (month)" num={c.billedThisMonth} trend="+8%" trendUp click onClick={() => go('Billing report')} />
          </div>
        </section>
      </div>
      {toast && <div className="af-toast"><AfIcon name="check" size={17} />{toast}</div>}
    </AfStaffShell>
  );
}

window.AfterDashboard = AfterDashboard;
