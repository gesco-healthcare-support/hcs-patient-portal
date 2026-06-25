/* global React */
const { useState: useStateA, useMemo: useMemoA } = React;
const { AfIcon, AfPill, AfStaffShell, avaColor, initials, fmtTime, fmtDateShort } = window.AfCommon;

function AfterApptList() {
  const { appointments, TYPES, LOCATIONS, STATUS } = window.MOCK;
  const [q, setQ] = useStateA('');
  const [showFilters, setShowFilters] = useStateA(false);
  const [fType, setFType] = useStateA('');
  const [fLoc, setFLoc] = useStateA('');
  const [fStatus, setFStatus] = useStateA('');
  const [sort, setSort] = useStateA({ key: 'date', dir: 'asc' });
  const [toast, setToast] = useStateA(null);
  function go(msg) { setToast(msg); clearTimeout(window.__ta); window.__ta = setTimeout(() => setToast(null), 2400); }

  const statusList = Object.entries(STATUS).map(([id, s]) => [id, s.label]);

  const rows = useMemoA(() => {
    let r = appointments.slice();
    const t = q.trim().toLowerCase();
    if (t) r = r.filter(a =>
      (a.patient.firstName + ' ' + a.patient.lastName).toLowerCase().includes(t) ||
      a.confirmation.toLowerCase().includes(t) ||
      a.claimNumber.toLowerCase().includes(t) ||
      a.identityEmail.toLowerCase().includes(t));
    if (fType) r = r.filter(a => a.type === fType);
    if (fLoc) r = r.filter(a => a.location === fLoc);
    if (fStatus) r = r.filter(a => String(a.statusId) === fStatus);
    r.sort((a, b) => {
      let av, bv;
      if (sort.key === 'date') { av = a.appointmentDate; bv = b.appointmentDate; }
      else if (sort.key === 'patient') { av = a.patient.lastName; bv = b.patient.lastName; }
      else if (sort.key === 'status') { av = a.status.label; bv = b.status.label; }
      else { av = a.confirmation; bv = b.confirmation; }
      return (av < bv ? -1 : av > bv ? 1 : 0) * (sort.dir === 'asc' ? 1 : -1);
    });
    return r;
  }, [q, fType, fLoc, fStatus, sort]);

  function toggleSort(key) {
    setSort(s => s.key === key ? { key, dir: s.dir === 'asc' ? 'desc' : 'asc' } : { key, dir: 'asc' });
  }
  function Th({ label, k }) {
    const on = sort.key === k;
    return (
      <th className="sortable" onClick={() => toggleSort(k)}>
        <span className="sortwrap">{label}<span style={{ opacity: on ? 1 : .35 }}><AfIcon name={on ? (sort.dir === 'asc' ? 'arrowUp' : 'arrowDown') : 'sort'} size={12} /></span></span>
      </th>
    );
  }

  const activeChips = [
    fType && ['Type: ' + fType, () => setFType('')],
    fLoc && ['Location: ' + fLoc, () => setFLoc('')],
    fStatus && ['Status: ' + STATUS[fStatus].label, () => setFStatus('')],
  ].filter(Boolean);

  return (
    <AfStaffShell active="appts" crumb="Appointments" pendingCount={window.MOCK.counters.pendingRequests}>
      <div className="af-content fade-in">
        <div className="af-page-head">
          <div>
            <h1 className="af-h1">Appointments</h1>
            <p className="af-sub">{rows.length} appointment{rows.length === 1 ? '' : 's'} · all locations</p>
          </div>
          <button className="af-btn af-btn--primary af-btn--lg" onClick={() => go('Opening new appointment…')}>
            <AfIcon name="plus" size={17} />New appointment
          </button>
        </div>

        <div className="af-card">
          {/* toolbar */}
          <div style={{ padding: '16px 18px', display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap', borderBottom: activeChips.length || showFilters ? '1px solid var(--border)' : 0 }}>
            <div className="af-search" style={{ flex: 1, minWidth: 260 }}>
              <AfIcon name="search" size={16} />
              <input placeholder="Search patient, confirmation #, claim #, email…" value={q} onChange={e => setQ(e.target.value)} />
              {q && <button className="af-rowbtn" style={{ width: 26, height: 26, border: 0, background: 'transparent' }} onClick={() => setQ('')}><AfIcon name="x" size={14} /></button>}
            </div>
            <button className={'af-btn ' + (showFilters ? 'af-btn--primary' : 'af-btn--ghost')} onClick={() => setShowFilters(s => !s)}>
              <AfIcon name="filter" size={16} />Filters{activeChips.length ? ' (' + activeChips.length + ')' : ''}
            </button>
            <button className="af-btn af-btn--ghost" onClick={() => go('Refreshed')}><AfIcon name="refresh" size={15} /></button>
          </div>

          {/* filter panel */}
          {showFilters && (
            <div style={{ padding: '18px', display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 16, borderBottom: '1px solid var(--border)', background: 'var(--n-25)' }}>
              <div className="af-field"><label>Appointment type</label>
                <select className="af-select" value={fType} onChange={e => setFType(e.target.value)}>
                  <option value="">All types</option>{TYPES.map(t => <option key={t}>{t}</option>)}
                </select>
              </div>
              <div className="af-field"><label>Location</label>
                <select className="af-select" value={fLoc} onChange={e => setFLoc(e.target.value)}>
                  <option value="">All locations</option>{LOCATIONS.map(l => <option key={l}>{l}</option>)}
                </select>
              </div>
              <div className="af-field"><label>Status</label>
                <select className="af-select" value={fStatus} onChange={e => setFStatus(e.target.value)}>
                  <option value="">Any status</option>{statusList.map(([id, l]) => <option key={id} value={id}>{l}</option>)}
                </select>
              </div>
            </div>
          )}

          {/* active chips */}
          {activeChips.length > 0 && (
            <div style={{ padding: '13px 18px', display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
              <span style={{ fontSize: 12.5, color: 'var(--n-500)', fontWeight: 600 }}>Active:</span>
              {activeChips.map(([lbl, clear], i) => (
                <span className="af-chip" key={i}>{lbl}<button onClick={clear}><AfIcon name="x" size={12} /></button></span>
              ))}
              <button className="af-btn af-btn--sm" style={{ background: 'transparent', color: 'var(--blue-700)', padding: '4px 8px' }} onClick={() => { setFType(''); setFLoc(''); setFStatus(''); }}>Clear all</button>
            </div>
          )}

          {/* table */}
          <div style={{ overflowX: 'auto' }}>
            <table className="af-table">
              <thead>
                <tr>
                  <Th label="Patient" k="patient" />
                  <Th label="Confirmation" k="conf" />
                  <Th label="Date & time" k="date" />
                  <th>Type</th>
                  <th>Location</th>
                  <th>Decide by</th>
                  <Th label="Status" k="status" />
                  <th style={{ textAlign: 'right' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {rows.map(a => (
                  <tr key={a.id}>
                    <td>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 11 }}>
                        <span className="af-ava" style={{ background: avaColor(a.patient.lastName) }}>{initials(a.patient.firstName, a.patient.lastName)}</span>
                        <div>
                          <div className="af-name">{a.patient.firstName} {a.patient.lastName}</div>
                          <div className="af-sub2">{a.patient.gender} · Panel {a.panelNumber}</div>
                        </div>
                      </div>
                    </td>
                    <td><span className="af-mono">{a.confirmation}</span></td>
                    <td>
                      <div style={{ fontWeight: 600, color: 'var(--n-800)' }}>{fmtDateShort(a.appointmentDate)}</div>
                      <div className="af-sub2">{fmtTime(a.appointmentDate)}</div>
                    </td>
                    <td>{a.type}</td>
                    <td>{a.location}</td>
                    <td>{a.dueDate}</td>
                    <td><AfPill tone={a.status.tone} label={a.status.label} /></td>
                    <td style={{ textAlign: 'right' }}>
                      <span className="af-rowact">
                        <button className="af-rowbtn" title="Review" onClick={() => go('Reviewing ' + a.confirmation)}><AfIcon name="eye" size={15} /></button>
                        <button className="af-rowbtn" title="Documents" onClick={() => go('Documents · ' + a.confirmation)}><AfIcon name="doc" size={15} /></button>
                        <button className="af-rowbtn" title="More"><AfIcon name="dots" size={15} /></button>
                      </span>
                    </td>
                  </tr>
                ))}
                {rows.length === 0 && (
                  <tr><td colSpan="8"><div className="af-empty"><span className="i" dangerouslySetInnerHTML={window.Ico('inbox', 38)} /><div style={{ fontWeight: 700, color: 'var(--n-600)' }}>No appointments match</div><div style={{ fontSize: 13 }}>Adjust your search or filters.</div></div></td></tr>
                )}
              </tbody>
            </table>
          </div>

          <div className="af-pager">
            <span>Showing <b style={{ color: 'var(--n-800)' }}>{rows.length}</b> of {appointments.length}</span>
            <div className="pp">
              <button><AfIcon name="chevLeft" size={15} /></button>
              <button className="on">1</button><button>2</button><button>3</button>
              <button><AfIcon name="chevRight" size={15} /></button>
            </div>
          </div>
        </div>
      </div>
      {toast && <div className="af-toast"><AfIcon name="check" size={17} />{toast}</div>}
    </AfStaffShell>
  );
}

window.AfterApptList = AfterApptList;
