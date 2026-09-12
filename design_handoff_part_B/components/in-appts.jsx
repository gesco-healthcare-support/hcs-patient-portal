/* global React */
/* ============================================================
   Internal Appointments list — search + status chips + collapsible
   filters, dense table (select + kebab), due-date urgency, bulk bar,
   pager. Role-aware (intake = no delete). Renders inside the shell.
   ============================================================ */
const { useState: useStateIA, useMemo: useMemoIA } = React;
function IAI({ name, size }) { return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size || 18)} />; }

const IA_NOW = new Date('2026-06-10T00:00:00');
function daysLeft(due) { const d = new Date(due); return Math.round((d - IA_NOW) / 86400000); }
function catOf(statusId) {
  if (statusId === 2) return 'approved';
  if (statusId === 3) return 'rejected';
  if (statusId === 5 || statusId === 6 || statusId === 13) return 'cancelled';
  if (statusId === 12) return 'rescheduled';
  if (statusId === 14) return 'info';
  return 'pending';
}
const IA_CHIPS = [['all', 'All'], ['pending', 'Pending'], ['info', 'Info Requested'], ['approved', 'Approved'], ['rejected', 'Rejected'], ['cancelled', 'Cancelled'], ['rescheduled', 'Rescheduled']];

function InAppointments({ roleKey, onToast, onOpen }) {
  const rows0 = window.MOCK.appointments;
  const [q, setQ] = useStateIA('');
  const [chip, setChip] = useStateIA('all');
  const [showFilters, setShowFilters] = useStateIA(false);
  const [filters, setFilters] = useStateIA({});
  const [draft, setDraft] = useStateIA({});
  const [sel, setSel] = useStateIA(new Set());
  const [menu, setMenu] = useStateIA(null);
  const [page, setPage] = useStateIA(1);
  const [pageSize, setPageSize] = useStateIA(10);
  const canDelete = roleKey !== 'intake';

  const counts = useMemoIA(() => {
    const c = { all: rows0.length, pending: 0, info: 0, approved: 0, rejected: 0, cancelled: 0, rescheduled: 0 };
    rows0.forEach(a => c[catOf(a.statusId)]++);
    return c;
  }, []);

  const filtered = useMemoIA(() => {
    let r = rows0;
    if (chip !== 'all') r = r.filter(a => catOf(a.statusId) === chip);
    const t = q.trim().toLowerCase();
    if (t) r = r.filter(a => (a.patient.firstName + ' ' + a.patient.lastName).toLowerCase().includes(t) || a.confirmation.toLowerCase().includes(t) || (a.panelNumber || '').toLowerCase().includes(t) || a.type.toLowerCase().includes(t) || a.identityEmail.toLowerCase().includes(t));
    if (filters.panel) r = r.filter(a => (a.panelNumber || '').toLowerCase().includes(filters.panel.toLowerCase()));
    if (filters.type) r = r.filter(a => a.type === filters.type);
    if (filters.location) r = r.filter(a => a.location === filters.location);
    if (filters.identity) r = r.filter(a => a.identityEmail.toLowerCase().includes(filters.identity.toLowerCase()));
    return r;
  }, [q, chip, filters]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const pg = Math.min(page, totalPages);
  const shown = filtered.slice((pg - 1) * pageSize, pg * pageSize);
  const activeFilters = Object.keys(filters).filter(k => filters[k]).length;
  const allSel = shown.length > 0 && shown.every(a => sel.has(a.id));

  function toggleSel(id) { const n = new Set(sel); n.has(id) ? n.delete(id) : n.add(id); setSel(n); }
  function toggleAll() { const n = new Set(sel); if (allSel) shown.forEach(a => n.delete(a.id)); else shown.forEach(a => n.add(a.id)); setSel(n); }
  function applyFilters() { setFilters(draft); setShowFilters(false); setPage(1); }
  function fmtDate(iso) { return new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }); }
  function fmtTime(iso) { return new Date(iso).toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' }); }

  const TYPES = window.MOCK.TYPES, LOCS = window.MOCK.LOCATIONS;

  return (
    <>
      <div className="ia-head">
        <div><h1>Appointments</h1><p>{filtered.length} of {rows0.length} appointments</p></div>
        <a className="af-btn af-btn--primary" onClick={() => onToast('Opening new appointment…')}><IAI name="plus" size={16} />New appointment</a>
      </div>

      <div className="ia-toolbar">
        <div className="ia-search"><IAI name="search" size={16} /><input placeholder="Search confirmation #, patient, panel #, type, booker…" value={q} onChange={e => { setQ(e.target.value); setPage(1); }} />{q && <button className="ia-kebab" style={{ border: 0 }} onClick={() => setQ('')}><IAI name="x" size={14} /></button>}</div>
        <button className="ia-fbtn" aria-expanded={showFilters} onClick={() => { setDraft(filters); setShowFilters(!showFilters); }}><IAI name="filter" size={15} />Filters{activeFilters > 0 && <span className="badge">{activeFilters}</span>}</button>
      </div>

      <div className="ia-chips">
        {IA_CHIPS.map(([k, lbl]) => <button key={k} className="ia-chip" data-on={chip === k} onClick={() => { setChip(k); setPage(1); }}>{lbl}<span className="cnt">{counts[k]}</span></button>)}
      </div>

      {showFilters && (
        <div className="ia-filters">
          <div className="ia-filters__grid">
            <div className="ia-field"><label>Panel #</label><input className="ia-input" value={draft.panel || ''} onChange={e => setDraft({ ...draft, panel: e.target.value })} placeholder="e.g. P-2204" /></div>
            <div className="ia-field"><label>Appointment type</label><select className="ia-input" value={draft.type || ''} onChange={e => setDraft({ ...draft, type: e.target.value })}><option value="">All types</option>{TYPES.map(t => <option key={t}>{t}</option>)}</select></div>
            <div className="ia-field"><label>Location</label><select className="ia-input" value={draft.location || ''} onChange={e => setDraft({ ...draft, location: e.target.value })}><option value="">All locations</option>{LOCS.map(l => <option key={l}>{l}</option>)}</select></div>
            <div className="ia-field"><label>Min appointment date</label><input className="ia-input" type="date" value={draft.dmin || ''} onChange={e => setDraft({ ...draft, dmin: e.target.value })} /></div>
            <div className="ia-field"><label>Max appointment date</label><input className="ia-input" type="date" value={draft.dmax || ''} onChange={e => setDraft({ ...draft, dmax: e.target.value })} /></div>
            <div className="ia-field"><label>Booker (identity user)</label><input className="ia-input" value={draft.identity || ''} onChange={e => setDraft({ ...draft, identity: e.target.value })} placeholder="email" /></div>
          </div>
          <div className="ia-filters__foot">
            <button className="af-btn af-btn--ghost" onClick={() => { setDraft({}); setFilters({}); }}>Clear</button>
            <button className="af-btn af-btn--primary" onClick={applyFilters}><IAI name="search" size={15} />Apply</button>
          </div>
        </div>
      )}

      {sel.size > 0 && (
        <div className="ia-bulk">
          <IAI name="check" size={16} />{sel.size} selected
          <div className="x">
            <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => onToast('Exporting ' + sel.size + ' rows…')}><IAI name="arrowDown" size={14} />Export</button>
            {canDelete && <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => { onToast('Deleting ' + sel.size + '…'); setSel(new Set()); }}><IAI name="x" size={14} />Delete</button>}
            <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => setSel(new Set())}>Clear</button>
          </div>
        </div>
      )}

      <div className="ia-wrap">
        <div className="ia-scroll">
          <table className="ia-table">
            <thead><tr>
              <th style={{ width: 38 }}><input type="checkbox" className="ia-chk" checked={allSel} onChange={toggleAll} /></th>
              <th>Confirmation #</th><th>Patient</th><th>Type</th><th>Appointment</th><th>Status</th><th>Panel #</th><th>Location</th><th>Decide by</th><th style={{ width: 44 }}></th>
            </tr></thead>
            <tbody>
              {shown.map(a => {
                const cat = catOf(a.statusId);
                // staff decision deadline: 3 days from request — only meaningful while Pending
                const isPending = cat === 'pending';
                const dl = daysLeft(a.dueDate); const cls = dl <= 1 ? 'crit' : dl <= 2 ? 'warn' : 'ok';
                // staff can reschedule/cancel any non-terminal appointment (incl. Pending & Info Requested)
                const actionable = ['pending', 'info', 'approved', 'rescheduled'].includes(cat);
                return (
                  <tr key={a.id} className={sel.has(a.id) ? 'sel' : ''} onClick={() => onOpen && onOpen(a)}>
                    <td onClick={e => e.stopPropagation()}><input type="checkbox" className="ia-chk" checked={sel.has(a.id)} onChange={() => toggleSel(a.id)} /></td>
                    <td><span className="ia-conf">{a.confirmation}</span></td>
                    <td><span className="ia-pt"><span className="ava" style={{ background: window.AfCommon.avaColor(a.patient.firstName + a.patient.lastName) }}>{window.AfCommon.initials(a.patient.firstName, a.patient.lastName)}</span><b>{a.patient.firstName} {a.patient.lastName}</b></span></td>
                    <td>{a.type}</td>
                    <td>{fmtDate(a.appointmentDate)}<div className="ia-sub">{fmtTime(a.appointmentDate)}</div></td>
                    <td><window.AfCommon.AfPill tone={a.status.tone} label={a.status.label} /></td>
                    <td className="ia-sub" style={{ fontFamily: 'var(--font-num)', color: 'var(--n-700)' }}>{a.panelNumber}</td>
                    <td>{a.location}</td>
                    <td>{isPending ? <span className="ia-due">{fmtDate(a.dueDate)}<span className={'dl ' + cls}>{dl < 0 ? 'past' : dl === 0 ? 'today' : dl + 'd'}</span></span> : <span className="ia-sub">—</span>}</td>
                    <td onClick={e => e.stopPropagation()} style={{ position: 'relative' }}>
                      <button className="ia-kebab" onClick={() => setMenu(menu === a.id ? null : a.id)}><IAI name="dots" size={16} /></button>
                      {menu === a.id && (<>
                        <div className="ia-clickaway" onClick={() => setMenu(null)} />
                        <div className="ia-menu">
                          <button onClick={() => { setMenu(null); onOpen && onOpen(a); }}><span className="i"><IAI name="eye" size={15} /></span>Review</button>
                          {actionable && <button onClick={() => { setMenu(null); onToast('Reschedule ' + a.confirmation); }}><span className="i"><IAI name="refresh" size={15} /></span>Reschedule</button>}
                          {actionable && <button onClick={() => { setMenu(null); onToast('Cancel ' + a.confirmation); }}><span className="i"><IAI name="x" size={15} /></span>Cancel</button>}
                          {canDelete && <><div className="ia-menu__div" /><button className="danger" onClick={() => { setMenu(null); onToast('Delete ' + a.confirmation); }}><span className="i"><IAI name="x" size={15} /></span>Delete</button></>}
                        </div>
                      </>)}
                    </td>
                  </tr>
                );
              })}
              {shown.length === 0 && <tr><td colSpan="10"><div className="ia-empty"><IAI name="inbox" size={36} /><b>No appointments match</b><span>Try a different search, status, or filter.</span></div></td></tr>}
            </tbody>
          </table>
        </div>
        <div className="ia-pager">
          <div className="info">Showing {shown.length ? (pg - 1) * pageSize + 1 : 0}–{(pg - 1) * pageSize + shown.length} of {filtered.length}</div>
          <div className="right">
            <label className="info">Rows: <select value={pageSize} onChange={e => { setPageSize(+e.target.value); setPage(1); }}>{[10, 25, 50].map(n => <option key={n} value={n}>{n}</option>)}</select></label>
            <div className="ia-pp">
              <button onClick={() => setPage(Math.max(1, pg - 1))}><IAI name="chevLeft" size={14} /></button>
              {Array.from({ length: totalPages }, (_, i) => i + 1).map(n => <button key={n} className={n === pg ? 'on' : ''} onClick={() => setPage(n)}>{n}</button>)}
              <button onClick={() => setPage(Math.min(totalPages, pg + 1))}><IAI name="chevRight" size={14} /></button>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

window.InAppointments = InAppointments;
