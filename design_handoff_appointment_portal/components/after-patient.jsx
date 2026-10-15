/* global React */
const { useState: useStateP, useMemo: useMemoP } = React;
const { AfIcon, AfPill, avaColor, initials, fmtTime, fmtMonth, fmtDay } = window.AfCommon;

function AfterPatientHome() {
  const { appointments } = window.MOCK;
  // patient view: pretend these are "my" requests
  const [q, setQ] = useStateP('');
  const [filter, setFilter] = useStateP('all');
  const [toast, setToast] = useStateP(null);

  function showToast(msg) { setToast(msg); clearTimeout(window.__t); window.__t = setTimeout(() => setToast(null), 2600); }

  const statusFilters = useMemoP(() => {
    const groups = {
      all: appointments.length,
      pending: appointments.filter(a => a.status.tone === 'pending').length,
      approved: appointments.filter(a => a.status.tone === 'approved').length,
      past: appointments.filter(a => ['teal', 'purple', 'neutral'].includes(a.status.tone)).length,
    };
    return groups;
  }, []);

  const rows = useMemoP(() => {
    let r = appointments;
    if (filter === 'pending') r = r.filter(a => a.status.tone === 'pending');
    else if (filter === 'approved') r = r.filter(a => a.status.tone === 'approved');
    else if (filter === 'past') r = r.filter(a => ['teal', 'purple', 'neutral'].includes(a.status.tone));
    const t = q.trim().toLowerCase();
    if (t) r = r.filter(a =>
      (a.patient.firstName + ' ' + a.patient.lastName).toLowerCase().includes(t) ||
      a.confirmation.toLowerCase().includes(t) ||
      a.claimNumber.toLowerCase().includes(t) ||
      a.type.toLowerCase().includes(t));
    return r;
  }, [q, filter]);

  return (
    <div className="af">
      <div className="af-pt">
        <div className="af-pt-top">
          <div className="af-pt-top__in">
            <img src="assets/header-logo.png" alt="Falkinstein Orthopedics" />
            <div className="who">
              <div className="nm"><b>Maria Gonzalez</b><span>Patient</span></div>
              <div className="ava" style={{ background: avaColor('Maria Gonzalez') }}>MG</div>
            </div>
          </div>
        </div>

        <div className="af-pt-hero">
          <div className="af-pt-hero__in">
            <h1>Welcome back, Maria</h1>
            <p>Book a new evaluation or track the status of your existing requests.</p>
          </div>
        </div>

        <div className="af-pt-body">
          <div className="af-actions">
            <button className="af-action" onClick={() => showToast('Opening new appointment booking…')}>
              <span className="af-action__ic tint-blue"><AfIcon name="calendar" size={26} /></span>
              <span className="af-action__tx"><b>Book an appointment</b><span>Schedule a new QME or AME evaluation</span></span>
              <span className="af-action__go"><AfIcon name="chevRight" size={22} /></span>
            </button>
            <button className="af-action" onClick={() => showToast('Opening re-evaluation booking…')}>
              <span className="af-action__ic tint-green"><AfIcon name="refresh" size={24} /></span>
              <span className="af-action__tx"><b>Book a re-evaluation</b><span>Follow-up on a previous appointment</span></span>
              <span className="af-action__go"><AfIcon name="chevRight" size={22} /></span>
            </button>
          </div>

          <div className="af-rule" />

          <div className="af-page-head" style={{ marginBottom: 18 }}>
            <div>
              <h2 className="af-h1" style={{ fontSize: 21 }}>My appointment requests</h2>
              <p className="af-sub">{rows.length} of {appointments.length} requests shown</p>
            </div>
            <div className="af-search" style={{ minWidth: 280 }}>
              <AfIcon name="search" size={16} />
              <input placeholder="Search by type, confirmation #, claim…" value={q} onChange={e => setQ(e.target.value)} />
              {q && <button className="af-rowbtn" style={{ width: 26, height: 26, border: 0, background: 'transparent' }} onClick={() => setQ('')}><AfIcon name="x" size={14} /></button>}
            </div>
          </div>

          <div className="af-segfilter" style={{ marginBottom: 18 }}>
            {[['all', 'All'], ['pending', 'Pending'], ['approved', 'Approved'], ['past', 'Completed']].map(([k, lbl]) => (
              <button key={k} data-on={filter === k} onClick={() => setFilter(k)}>
                {lbl}<span className="cnt">{statusFilters[k]}</span>
              </button>
            ))}
          </div>

          {rows.length === 0 ? (
            <div className="af-card"><div className="af-empty">
              <span className="i" dangerouslySetInnerHTML={window.Ico('inbox', 40)} />
              <div style={{ fontWeight: 700, color: 'var(--n-600)', fontSize: 15 }}>No matching requests</div>
              <div style={{ fontSize: 13 }}>Try a different search or filter.</div>
            </div></div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              {rows.map(a => (
                <div className="af-reqcard" key={a.id}>
                  <div className="when">
                    <div className="mo">{fmtMonth(a.appointmentDate)}</div>
                    <div className="dy">{fmtDay(a.appointmentDate)}</div>
                    <div className="tm">{fmtTime(a.appointmentDate)}</div>
                  </div>
                  <div className="meta">
                    <b>{a.type}</b>
                    <div className="row2">
                      <span><AfIcon name="map" size={14} />{a.location}</span>
                      <span><AfIcon name="doc" size={14} />Claim {a.claimNumber}</span>
                      <span><AfIcon name="user" size={14} />Conf. <b style={{ color: 'var(--blue-700)', fontFamily: 'var(--font-num)', marginLeft: 2 }}>{a.confirmation}</b></span>
                    </div>
                  </div>
                  <div className="right">
                    <AfPill tone={a.status.tone} label={a.status.label} />
                    <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => showToast('Opening documents for ' + a.confirmation)}>
                      <AfIcon name="doc" size={14} />Documents
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
      {toast && <div className="af-toast"><AfIcon name="check" size={17} />{toast}</div>}
    </div>
  );
}

window.AfterPatientHome = AfterPatientHome;
