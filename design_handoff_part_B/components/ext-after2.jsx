/* global React */
const { useState: useStateE2, useMemo: useMemoE2 } = React;
const { ExtNav, ExtHero, ExtActions, bucketOf, EIcon: E2, EPill: P2, eAva: A2, eInit: I2, eTime: T2, eMonth: M2, eDay: D2, eDate: DT2 } = window.ExtAfterParts;

const SEGS = [
  ['all', 'All'], ['pending', 'Pending'], ['info', 'Info Requested'], ['approved', 'Approved'],
  ['rescheduled', 'Rescheduled'], ['cancelled', 'Cancelled'], ['rejected', 'Rejected', true],
];

/* ----------------------------------------------------------------
   Submit-query modal (Help)
   ---------------------------------------------------------------- */
function ExtQueryModal({ open, onClose, onToast }) {
  const [msg, setMsg] = useStateE2('');
  const [conf, setConf] = useStateE2('');
  if (!open) return null;
  const max = 1000;
  return (
    <div className="ext-modal-scrim" onClick={onClose}>
      <div className="ext-modal" onClick={e => e.stopPropagation()}>
        <div className="ext-modal__head">
          <span className="ic tint-blue"><E2 name="help" size={22} /></span>
          <div>
            <h3>Submit a query</h3>
            <p>Send a question to the clinic team. We'll reply by email.</p>
          </div>
          <button className="ext-iconbtn x" onClick={onClose} aria-label="Close"><E2 name="x" size={17} /></button>
        </div>
        <div className="ext-modal__body">
          <div className="af-field" style={{ marginBottom: 16 }}>
            <label>Message <span style={{ color: 'var(--st-rejected-fg)' }}>*</span></label>
            <textarea className="ext-textarea" maxLength={max} value={msg} onChange={e => setMsg(e.target.value)}
              placeholder="Describe your question or request…" />
            <div className="ext-count">{msg.length} / {max}</div>
          </div>
          <div className="af-field">
            <label>Confirmation number <span style={{ color: 'var(--n-400)', fontWeight: 500 }}>(optional)</span></label>
            <input className="af-input" value={conf} onChange={e => setConf(e.target.value)} placeholder="e.g. PQ-24817" />
          </div>
          <div className="ext-help-note">
            <span className="i"><E2 name="alert" size={15} /></span>
            <span>Do not include Social Security numbers, dates of birth, or other protected health information in your message.</span>
          </div>
        </div>
        <div className="ext-modal__foot">
          <button className="af-btn af-btn--ghost" onClick={onClose}>Close</button>
          <button className="af-btn af-btn--primary" disabled={!msg.trim()} style={!msg.trim() ? { opacity: .5, cursor: 'not-allowed' } : null}
            onClick={() => { onClose(); onToast('Your query has been sent to the clinic team.'); }}>
            <E2 name="check" size={16} />Send query
          </button>
        </div>
      </div>
    </div>
  );
}

/* ----------------------------------------------------------------
   Appointments section
   ---------------------------------------------------------------- */
function ExtAppointments({ role, rows, activeSeg, setSeg, onToast }) {
  const [q, setQ] = useStateE2('');
  const [view, setView] = useStateE2(role.defaultView);
  const [showFilters, setShowFilters] = useStateE2(false);
  const [filters, setFilters] = useStateE2({});
  const [draft, setDraft] = useStateE2({});

  const counts = useMemoE2(() => {
    const c = { all: rows.length, pending: 0, info: 0, approved: 0, rescheduled: 0, cancelled: 0, rejected: 0 };
    rows.forEach(a => { const b = bucketOf(a.status.tone, a.statusId); if (c[b] != null) c[b]++; });
    return c;
  }, [rows]);

  const filtered = useMemoE2(() => {
    let r = rows;
    if (activeSeg !== 'all') r = r.filter(a => bucketOf(a.status.tone, a.statusId) === activeSeg);
    const t = q.trim().toLowerCase();
    if (t) r = r.filter(a =>
      (a.patient.firstName + ' ' + a.patient.lastName).toLowerCase().includes(t) ||
      a.confirmation.toLowerCase().includes(t) || a.claimNumber.toLowerCase().includes(t) || (a.adjNumber || '').toLowerCase().includes(t) ||
      a.type.toLowerCase().includes(t) || a.location.toLowerCase().includes(t));
    if (filters.type) r = r.filter(a => a.type === filters.type);
    if (filters.location) r = r.filter(a => a.location === filters.location);
    if (filters.conf) r = r.filter(a => a.confirmation.toLowerCase().includes(filters.conf.toLowerCase()));
    if (filters.claim) r = r.filter(a => a.claimNumber.toLowerCase().includes(filters.claim.toLowerCase()));
    if (filters.adj) r = r.filter(a => (a.adjNumber || '').toLowerCase().includes(filters.adj.toLowerCase()));
    return r;
  }, [rows, activeSeg, q, filters]);

  const activeFilterKeys = Object.keys(filters).filter(k => filters[k]);
  const types = window.MOCK.TYPES, locs = window.MOCK.LOCATIONS;

  function applyFilters() { setFilters(draft); setShowFilters(false); }
  function resetFilters() { setDraft({}); setFilters({}); }
  function clearChip(k) { const f = { ...filters }; delete f[k]; setFilters(f); setDraft(f); }

  return (
    <div className="ext-wrap ext-list">
      <div className="ext-list__head">
        <div>
          <h2>{role.listTitle}</h2>
          <p className="sub">{role.listSub} · {filtered.length} of {rows.length} shown</p>
        </div>
        <div className="ext-toolbar">
          <div className="af-search ext-search">
            <E2 name="search" size={16} />
            <input placeholder="Search type, confirmation #, claim, patient…" value={q} onChange={e => setQ(e.target.value)} />
            {q && <button className="af-rowbtn" style={{ width: 26, height: 26, border: 0, background: 'transparent' }} onClick={() => setQ('')}><E2 name="x" size={14} /></button>}
          </div>
          <button className="ext-fbtn" aria-expanded={showFilters} onClick={() => { setDraft(filters); setShowFilters(!showFilters); }}>
            <E2 name="filter" size={15} />Filters
            {activeFilterKeys.length > 0 && <span className="badge">{activeFilterKeys.length}</span>}
          </button>
          <div className="ext-vtoggle">
            <button data-on={view === 'cards'} onClick={() => setView('cards')} aria-label="Card view"><E2 name="grid" size={16} /></button>
            <button data-on={view === 'table'} onClick={() => setView('table')} aria-label="Table view"><E2 name="list" size={16} /></button>
          </div>
        </div>
      </div>

      {/* advanced filter panel */}
      {showFilters && (
        <div className="ext-filters">
          <div className="ext-filters__grid">
            <div className="af-field"><label>Appointment type</label>
              <select className="af-select" value={draft.type || ''} onChange={e => setDraft({ ...draft, type: e.target.value })}>
                <option value="">All types</option>{types.map(t => <option key={t}>{t}</option>)}
              </select></div>
            <div className="af-field"><label>Confirmation #</label>
              <input className="af-input" placeholder="e.g. PQ-24817" value={draft.conf || ''} onChange={e => setDraft({ ...draft, conf: e.target.value })} /></div>
            <div className="af-field"><label>Location</label>
              <select className="af-select" value={draft.location || ''} onChange={e => setDraft({ ...draft, location: e.target.value })}>
                <option value="">All locations</option>{locs.map(l => <option key={l}>{l}</option>)}
              </select></div>
            <div className="af-field"><label>Status</label>
              <select className="af-select" value={draft.status || ''} onChange={e => setDraft({ ...draft, status: e.target.value })}>
                <option value="">Any status</option><option>Pending</option><option>Approved</option><option>Checked In</option><option>Billed</option>
              </select></div>
            <div className="af-field"><label>Claim #</label>
              <input className="af-input" placeholder="e.g. WC24-10480" value={draft.claim || ''} onChange={e => setDraft({ ...draft, claim: e.target.value })} /></div>
            <div className="af-field"><label>ADJ # (EAMS)</label>
              <input className="af-input" placeholder="e.g. ADJ-4471102" value={draft.adj || ''} onChange={e => setDraft({ ...draft, adj: e.target.value })} /></div>
            <div className="af-field"><label>Date of injury</label>
              <input className="af-input" type="date" value={draft.doi || ''} onChange={e => setDraft({ ...draft, doi: e.target.value })} /></div>
            {role.showDob && (
              <div className="af-field"><label>Date of birth</label>
                <input className="af-input" type="date" value={draft.dob || ''} onChange={e => setDraft({ ...draft, dob: e.target.value })} /></div>
            )}
            <div className="af-field"><label>Social Security #</label>
              <input className="af-input" placeholder="•••-••-0000" value={draft.ssn || ''} onChange={e => setDraft({ ...draft, ssn: e.target.value })} /></div>
          </div>
          <div className="ext-filters__foot">
            <button className="af-btn af-btn--ghost" onClick={resetFilters}><E2 name="refresh" size={15} />Reset</button>
            <button className="af-btn af-btn--primary" onClick={applyFilters}><E2 name="search" size={15} />Apply filters</button>
          </div>
        </div>
      )}

      {/* segmented status filters */}
      <div className="ext-segs">
        {SEGS.map(([k, lbl, alert]) => (
          <button key={k} className={'ext-seg' + (alert ? ' alert' : '')} data-on={activeSeg === k} onClick={() => setSeg(k)}>
            {lbl}<span className="cnt">{counts[k]}</span>
          </button>
        ))}
      </div>

      {/* active filter chips */}
      {activeFilterKeys.length > 0 && (
        <div className="ext-chips">
          {activeFilterKeys.map(k => (
            <span key={k} className="af-chip">{labelFor(k)}: {filters[k]}<button onClick={() => clearChip(k)}><E2 name="x" size={12} /></button></span>
          ))}
          <button className="af-btn af-btn--ghost af-btn--sm" onClick={resetFilters}>Clear all</button>
        </div>
      )}

      {filtered.length === 0 ? (
        <div className="ext-empty">
          <span className="i"><E2 name="inbox" size={40} /></span>
          <b>No matching appointments</b>
          <span>Try a different search, status, or filter.</span>
        </div>
      ) : view === 'cards' ? (
        <div className="ext-cards">
          {filtered.map(a => (
            <div className="ext-rcard" key={a.id}>
              <div className="when"><div className="mo">{M2(a.appointmentDate)}</div><div className="dy">{D2(a.appointmentDate)}</div><div className="tm">{T2(a.appointmentDate)}</div></div>
              <div className="meta">
                <b>{a.type}</b>
                <div className="row2">
                  {role.showPatientCol && <span><E2 name="user" size={14} />{a.patient.firstName} {a.patient.lastName}</span>}
                  <span><E2 name="map" size={14} />{a.location}</span>
                  <span><E2 name="doc" size={14} />Claim <b style={{ color: 'var(--n-700)', marginLeft: 2 }}>{a.claimNumber}</b></span>
                  <span><E2 name="list" size={14} />ADJ <b style={{ color: 'var(--n-700)', marginLeft: 2 }}>{a.adjNumber}</b></span>
                  <span><E2 name="calendar" size={14} />Conf. <b style={{ color: 'var(--blue-700)', fontFamily: 'var(--font-num)', marginLeft: 3 }}>{a.confirmation}</b></span>
                </div>
              </div>
              <div className="right">
                <P2 tone={a.status.tone} label={a.status.label} />
                <div className="ext-rcard__acts">
                  <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => onToast('Opening appointment ' + a.confirmation)}><E2 name="eye" size={14} />View</button>
                  <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => onToast('Opening documents for ' + a.confirmation)}><E2 name="doc" size={14} />Documents</button>
                </div>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="ext-tablewrap">
          <div className="ext-tablescroll">
            <table className="af-table">
              <thead>
                <tr>
                  {role.showPatientCol && <th>Patient</th>}
                  <th>Type</th><th>Confirmation #</th><th>Appointment date</th>
                  <th>Claim #</th><th>ADJ #</th><th>Location</th><th>Status</th><th style={{ textAlign: 'right' }}>Action</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map(a => (
                  <tr key={a.id}>
                    {role.showPatientCol && (
                      <td><div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                        <span className="af-ava" style={{ background: A2(a.patient.firstName + a.patient.lastName), width: 32, height: 32, fontSize: 12 }}>{I2(a.patient.firstName, a.patient.lastName)}</span>
                        <span className="af-name">{a.patient.firstName} {a.patient.lastName}</span>
                      </div></td>
                    )}
                    <td>{a.type}</td>
                    <td><span className="af-mono">{a.confirmation}</span></td>
                    <td>{DT2(a.appointmentDate)}<div className="af-sub2">{T2(a.appointmentDate)}</div></td>
                    <td className="af-sub2" style={{ fontFamily: 'var(--font-num)' }}>{a.claimNumber}</td>
                    <td className="af-sub2" style={{ fontFamily: 'var(--font-num)' }}>{a.adjNumber}</td>
                    <td>{a.location}</td>
                    <td><P2 tone={a.status.tone} label={a.status.label} /></td>
                    <td>
                      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
                        <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => onToast('Opening appointment ' + a.confirmation)}><E2 name="eye" size={14} />View</button>
                        <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => onToast('Opening documents for ' + a.confirmation)}><E2 name="doc" size={14} />Documents</button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="af-pager">
            <span>Showing 1–{filtered.length} of {filtered.length}</span>
            <div className="pp"><button><E2 name="chevLeft" size={14} /></button><button className="on">1</button><button><E2 name="chevRight" size={14} /></button></div>
          </div>
        </div>
      )}
    </div>
  );
}

function labelFor(k) {
  return { type: 'Type', conf: 'Conf #', location: 'Location', status: 'Status', claim: 'Claim #', adj: 'ADJ #', doi: 'DOI', dob: 'DOB', ssn: 'SSN' }[k] || k;
}

/* ----------------------------------------------------------------
   Composed After external home
   ---------------------------------------------------------------- */
function AfterExternalHome({ roleKey }) {
  const role = window.EXT.ROLES[roleKey];
  const rows = useMemoE2(() => window.EXT.rowsFor(roleKey), [roleKey]);
  const [seg, setSeg] = useStateE2('all');
  const [toast, setToast] = useStateE2(null);
  const [queryOpen, setQueryOpen] = useStateE2(false);

  function showToast(m) { setToast(m); clearTimeout(window.__et); window.__et = setTimeout(() => setToast(null), 2800); }

  return (
    <div className="ext">
      <ExtNav role={role} onToast={showToast} onOpenQuery={() => setQueryOpen(true)} onOpenProfile={() => showToast('Opening my profile…')} />
      <ExtHero role={role} />
      <ExtActions role={role} onToast={showToast} />
      <ExtAppointments role={role} rows={rows} activeSeg={seg} setSeg={setSeg} onToast={showToast} />
      <ExtQueryModal open={queryOpen} onClose={() => setQueryOpen(false)} onToast={showToast} />
      {toast && <div className="af-toast"><E2 name="check" size={17} />{toast}</div>}
    </div>
  );
}

window.AfterExternalHome = AfterExternalHome;
