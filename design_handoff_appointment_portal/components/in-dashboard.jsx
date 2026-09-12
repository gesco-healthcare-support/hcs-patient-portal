/* global React */
/* ============================================================
   Internal dashboard content — Tenant (Admin/Supervisor), Intake (light),
   and Host variants. Renders inside InternalShell. Lightweight charts.
   ============================================================ */
const { useState: useStateDH } = React;
function DHI({ name, size }) { return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size || 18)} />; }

/* Pending requests nearing the staff DECISION deadline — must be decided
   within 3 days of the request date (legal limit 5; 3 leaves a buffer). */
const DH_DEADLINES = [
  { conf: 'PQ-24830', pt: 'Sofia Rossi', req: 'Jun 8', due: 'Jun 11', days: 0 },
  { conf: 'PQ-24817', pt: 'Maria Gonzalez', req: 'Jun 9', due: 'Jun 12', days: 1 },
  { conf: 'AM-24820', pt: 'David Chen', req: 'Jun 10', due: 'Jun 13', days: 2 },
];
const DH_TREND = [['Wk 1', 14], ['Wk 2', 19], ['Wk 3', 16], ['Wk 4', 23], ['Wk 5', 18], ['Wk 6', 26]];
const DH_STATUS = [['Pending', 17, 'var(--st-pending-dot)'], ['Info Requested', 4, 'var(--st-purple-dot, #8a63c9)'], ['Approved', 42, 'var(--green-500)'], ['Rescheduled', 7, 'var(--blue-500)'], ['Cancelled', 11, 'var(--n-300)'], ['Rejected', 5, 'var(--st-rejected-dot)']];
const DH_SCHED = [
  ['8:30 AM', 'Panel QME · Aisha Hassan', 'Sacramento — Midtown'],
  ['9:00 AM', 'AME Evaluation · David Chen', 'San Diego — Hillcrest'],
  ['9:30 AM', 'Panel QME · Maria Gonzalez', 'Los Angeles — Wilshire'],
  ['11:00 AM', 'QME Follow-up · Kevin Patel', 'Sacramento — Midtown'],
  ['1:45 PM', 'Supplemental · Anthony Russo', 'Fresno — Herndon'],
];
const DH_ACTIVITY = [
  ['check', 'tint-green', '<b>Sandra Cole</b> approved PQ-24817', '12m ago'],
  ['refresh', 'tint-amber', 'Reschedule requested on FU-24779', '1h ago'],
  ['doc', 'tint-blue', 'Documents uploaded for AM-24820', '2h ago'],
  ['x', 'tint-red', 'AM-24744 was rejected', '3h ago'],
  ['user', 'tint-purple', 'New patient added: Nicole Adams', '5h ago'],
];
const DH_TENANTS = [
  ['Falkinstein Orthopedics', 'F', 128, 18, 42, 9],
  ['Bay Area Spine Group', 'B', 96, 11, 37, 6],
  ['Sierra Medical Evaluations', 'S', 74, 7, 29, 3],
  ['Coastal QME Associates', 'C', 53, 5, 21, 2],
];

function DonutChart() {
  const total = DH_STATUS.reduce((s, x) => s + x[1], 0);
  let acc = 0; const stops = DH_STATUS.map(([, v, c]) => { const a = acc / total * 360, b = (acc + v) / total * 360; acc += v; return `${c} ${a}deg ${b}deg`; }).join(', ');
  return (
    <div className="dh-donutwrap">
      <div className="dh-donut" style={{ background: `conic-gradient(${stops})` }}>
        <div className="dh-donut__c"><b>{total}</b><span>total</span></div>
      </div>
      <div className="dh-legend">
        {DH_STATUS.map(([nm, v, c]) => <div className="dh-legrow" key={nm}><span className="sw" style={{ background: c }} /><span className="nm">{nm}</span><span className="vl">{v}</span></div>)}
      </div>
    </div>
  );
}

function TrendChart() {
  const max = Math.max(...DH_TREND.map(d => d[1]));
  return (
    <div className="dh-bars">
      {DH_TREND.map(([lb, v]) => <div className="dh-barcol" key={lb}><div className="bar" data-v={v} style={{ height: (v / max * 100) + '%' }} /><span className="lb">{lb}</span></div>)}
    </div>
  );
}

function HeroKpi({ tone, icon, tint, label, num, delta, deltaDir, onToast }) {
  return (
    <div className={'dh-kpi ' + tone} onClick={() => onToast('Opening ' + label + '…')}>
      <div className="dh-kpi__top"><span className={'dh-kpi__ic ' + tint}><DHI name={icon} size={18} /></span><span className="dh-kpi__lbl">{label}</span></div>
      <div className="dh-kpi__num">{num}</div>
      <div className="dh-kpi__foot"><span className={'dh-delta ' + deltaDir}><DHI name={deltaDir === 'down' ? 'arrowDown' : 'arrowUp'} size={11} />{delta}</span> vs. last period</div>
    </div>
  );
}

function InDashboard({ roleKey, onToast }) {
  const c = window.MOCK.counters;
  const [tf, setTf] = useStateDH('week');
  const host = roleKey === 'itadmin';
  const intake = roleKey === 'intake';

  if (host) {
    const tot = DH_TENANTS.reduce((a, t) => ({ ap: a.ap + t[2], pe: a.pe + t[3], app: a.app + t[4] }), { ap: 0, pe: 0, app: 0 });
    return (
      <>
        <div className="dh-head"><div><h1>Host dashboard</h1><p>Activity across all tenants.</p></div></div>
        <div className="dh-hero">
          <HeroKpi tone="blue" icon="users" tint="tint-blue" label="Total tenants" num={DH_TENANTS.length} delta="+1" deltaDir="up" onToast={onToast} />
          <HeroKpi tone="green" icon="stetho" tint="tint-green" label="Total doctors" num={c.totalDoctors} delta="+3" deltaDir="up" onToast={onToast} />
          <HeroKpi tone="amber" icon="calendar" tint="tint-amber" label="Appointments" num={tot.ap} delta="+24" deltaDir="up" onToast={onToast} />
          <HeroKpi tone="red" icon="clock" tint="tint-red" label="Pending across tenants" num={tot.pe} delta="-4" deltaDir="down" onToast={onToast} />
        </div>
        <div className="dh-card">
          <div className="dh-card__head"><h3>Tenants</h3><a onClick={() => onToast('Opening tenants…')}>Manage tenants</a></div>
          <div className="dh-card__body" style={{ padding: 0 }}>
            <table className="dh-ttable">
              <thead><tr><th>Tenant</th><th className="r">Appointments</th><th className="r">Pending</th><th className="r">Approved</th><th className="r">This week</th></tr></thead>
              <tbody>
                {DH_TENANTS.map(([nm, mk, ap, pe, app, wk]) => (
                  <tr key={nm} onClick={() => onToast('Switching to ' + nm + '…')}>
                    <td><span className="tn"><span className="mk">{mk}</span>{nm}</span></td>
                    <td className="r num">{ap}</td><td className="r num">{pe}</td><td className="r num">{app}</td><td className="r num">{wk}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </>
    );
  }

  // shared blocks
  const scheduleCard = (
    <div className="dh-card">
      <div className="dh-card__head"><h3>Today's schedule</h3><a onClick={() => onToast('Opening appointments…')}>View all</a></div>
      <div className="dh-card__body">
        {DH_SCHED.map(([tm, t, loc]) => <div className="dh-srow" key={tm + t}><span className="tm">{tm}</span><span className="tx"><b>{t}</b><span>{loc}</span></span></div>)}
      </div>
    </div>
  );
  const activityCard = (
    <div className="dh-card">
      <div className="dh-card__head"><h3>Recent activity</h3></div>
      <div className="dh-card__body">
        {DH_ACTIVITY.map(([ic, tint, html, tm], i) => <div className="dh-act" key={i}><span className={'ic ' + tint}><DHI name={ic} size={15} /></span><span className="tx"><span dangerouslySetInnerHTML={{ __html: html }} /><span className="tm">{tm}</span></span></div>)}
      </div>
    </div>
  );

  if (intake) {
    return (
      <>
        <div className="dh-head"><div><h1>Dashboard</h1><p>Today at Falkinstein Orthopedics.</p></div></div>
        <div className="dh-hero" style={{ gridTemplateColumns: 'repeat(2, 1fr)', maxWidth: 560 }}>
          <HeroKpi tone="amber" icon="clock" tint="tint-amber" label="Pending Requests" num={c.pendingRequests} delta="+5" deltaDir="up" onToast={onToast} />
          <HeroKpi tone="blue" icon="calendar" tint="tint-blue" label="Today's Appointments" num={DH_SCHED.length} delta="+2" deltaDir="up" onToast={onToast} />
        </div>
        <div className="dh-grid dh-grid--2-1">{scheduleCard}{activityCard}</div>
      </>
    );
  }

  // Tenant Admin / Staff Supervisor
  return (
    <>
      <div className="dh-head">
        <div><h1>Dashboard</h1><p>Overview for Falkinstein Orthopedics.</p></div>
        <div className="dh-tf">
          {[['week', 'This week'], ['month', 'This month'], ['quarter', 'This quarter']].map(([k, lb]) => <button key={k} data-on={tf === k} onClick={() => setTf(k)}>{lb}</button>)}
        </div>
      </div>

      <div className="dh-hero">
        <HeroKpi tone="amber" icon="clock" tint="tint-amber" label="Pending Requests" num={c.pendingRequests} delta="+5" deltaDir="up" onToast={onToast} />
        <HeroKpi tone="blue" icon="refresh" tint="tint-blue" label="Pending Change Requests" num={c.pendingChangeRequests} delta="+2" deltaDir="up" onToast={onToast} />
        <HeroKpi tone="green" icon="check" tint="tint-green" label="Approved Requests" num={tf === 'month' ? 168 : c.approvedThisWeek} delta="+12%" deltaDir="up" onToast={onToast} />
        <HeroKpi tone="red" icon="x" tint="tint-red" label="Rejected Requests" num={tf === 'month' ? 19 : c.rejectedThisWeek} delta="-3%" deltaDir="down" onToast={onToast} />
      </div>

      <div className="dh-alert">
        <div className="dh-alert__head">
          <span className="ic"><DHI name="alert" size={19} /></span>
          <div><b>{DH_DEADLINES.length} pending requests approaching the decision deadline</b><span>Requests must be decided within 3 days of submission — the legal limit is 5.</span></div>
          <button className="more" onClick={() => onToast('Opening pending requests by decision deadline…')}>View all</button>
        </div>
        <div className="dh-alert__list">
          {DH_DEADLINES.map(d => (
            <div className="dh-alert__row" key={d.conf} onClick={() => onToast('Opening ' + d.conf + '…')}>
              <span className="conf">{d.conf}</span><span className="pt">{d.pt}</span><span className="due">Requested {d.req} · decide by {d.due}</span>
              <span className={'days' + (d.days <= 1 ? '' : ' warn')}>{d.days === 0 ? 'due today' : d.days + 'd left'}</span>
            </div>
          ))}
        </div>
      </div>

      <div className="dh-grid dh-grid--2-1">
        <div className="dh-card">
          <div className="dh-card__head"><h3>Requests over time</h3><a onClick={() => onToast('Opening reports…')}>Reports</a></div>
          <div className="dh-card__body"><TrendChart /></div>
        </div>
        <div className="dh-card">
          <div className="dh-card__head"><h3>Status breakdown</h3></div>
          <div className="dh-card__body"><DonutChart /></div>
        </div>
      </div>

      <div className="dh-grid dh-grid--1-1">{scheduleCard}{activityCard}</div>
    </>
  );
}

window.InDashboard = InDashboard;
