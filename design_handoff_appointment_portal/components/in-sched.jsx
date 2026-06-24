/* global React */
/* ============================================================
   Group 2 — Doctor Availabilities (week grid + table toggle) ·
   Generate slots (form → conflict preview → submit) · Locations ·
   WCAB Offices. Renders inside InternalShell.
   ============================================================ */
const { useState: useStateS, useMemo: useMemoS } = React;
function SI({ name, size }) { return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size || 18)} />; }

/* ---------------- mock data ---------------- */
const AV_LOCS = ['Los Angeles — Wilshire', 'Sacramento — Midtown'];
const AV_WEEK = ['Mon 15', 'Tue 16', 'Wed 17', 'Thu 18', 'Fri 19', 'Sat 20', 'Sun 21'];
function mkSlot(t, status, cap, booked) { return { id: t + status + Math.random().toString(36).slice(2, 6), t, status, cap, booked }; }
const AV_DATA = {
  'Los Angeles — Wilshire': [
    [mkSlot('8:30 – 9:30', 'booked', 3, 3), mkSlot('9:30 – 10:30', 'available', 3, 1), mkSlot('1:00 – 2:00', 'available', 3, 0)],
    [mkSlot('8:30 – 9:30', 'booked', 3, 3), mkSlot('10:30 – 11:30', 'reserved', 2, 0), mkSlot('1:00 – 2:00', 'available', 3, 2)],
    [mkSlot('9:30 – 10:30', 'available', 3, 0), mkSlot('1:00 – 2:00', 'available', 3, 0)],
    [mkSlot('8:30 – 9:30', 'available', 3, 1), mkSlot('9:30 – 10:30', 'booked', 3, 3), mkSlot('2:00 – 3:00', 'reserved', 2, 0)],
    [mkSlot('8:30 – 9:30', 'available', 3, 0)],
    [],
    [],
  ],
  'Sacramento — Midtown': [
    [mkSlot('9:00 – 10:00', 'available', 2, 0), mkSlot('11:00 – 12:00', 'booked', 2, 2)],
    [mkSlot('9:00 – 10:00', 'available', 2, 1)],
    [mkSlot('9:00 – 10:00', 'reserved', 2, 0), mkSlot('11:00 – 12:00', 'available', 2, 0), mkSlot('2:00 – 3:00', 'available', 2, 0)],
    [],
    [mkSlot('9:00 – 10:00', 'booked', 2, 2), mkSlot('11:00 – 12:00', 'available', 2, 1)],
    [mkSlot('10:00 – 11:00', 'available', 2, 0)],
    [],
  ],
};
const LW_LOCS0 = [
  { id: 1, name: 'Los Angeles — Wilshire', address: '3600 Wilshire Blvd, Ste 1200', city: 'Los Angeles', zip: '90010', state: 'CA', fee: 12, active: true, types: ['Panel QME', 'AME Evaluation'] },
  { id: 2, name: 'Sacramento — Midtown', address: '2120 J St, Ste 400', city: 'Sacramento', zip: '95816', state: 'CA', fee: 8, active: true, types: ['Panel QME', 'QME Follow-up'] },
  { id: 3, name: 'San Diego — Hillcrest', address: '3737 Fifth Ave, Ste 210', city: 'San Diego', zip: '92103', state: 'CA', fee: 10, active: false, types: ['AME Evaluation'] },
];
const LW_WCAB0 = [
  { id: 1, name: 'Los Angeles (WCAB)', code: 'LAO' }, { id: 2, name: 'Anaheim (WCAB)', code: 'ANA' },
  { id: 3, name: 'San Diego (WCAB)', code: 'SDO' }, { id: 4, name: 'Sacramento (WCAB)', code: 'SAC' },
  { id: 5, name: 'Oakland (WCAB)', code: 'OAK' }, { id: 6, name: 'Fresno (WCAB)', code: 'FRE' },
];

/* ---------------- Availabilities ---------------- */
function AvailPage({ onToast }) {
  const [view, setView] = useStateS('grid');
  const [loc, setLoc] = useStateS(AV_LOCS[0]);
  const [status, setStatus] = useStateS('all');
  const [week, setWeek] = useStateS(0);
  const [data, setData] = useStateS(AV_DATA);
  const [confirm, setConfirm] = useStateS(null); // day index for bulk delete
  const [open, setOpen] = useStateS({});

  const days = data[loc];
  const filt = s => status === 'all' || s.status === status;

  function removeSlot(di, id) { setData(d => ({ ...d, [loc]: d[loc].map((arr, i) => i === di ? arr.filter(s => s.id !== id) : arr) })); onToast('Slot deleted.'); }
  function clearDay(di) { setData(d => ({ ...d, [loc]: d[loc].map((arr, i) => i === di ? [] : arr) })); setConfirm(null); onToast('All slots removed for ' + AV_WEEK[di] + '.'); }

  return (
    <>
      <div className="ia-head">
        <div><h1>Doctor availabilities</h1><p>Slot schedule for this practice, by location.</p></div>
        <a className="af-btn af-btn--primary" onClick={() => onToast('Opening Generate slots…')}><SI name="plus" size={16} />Generate slots</a>
      </div>

      <div className="ia-toolbar">
        <select className="ia-input" style={{ width: 230 }} value={loc} onChange={e => setLoc(e.target.value)}>{AV_LOCS.map(l => <option key={l}>{l}</option>)}</select>
        <select className="ia-input" style={{ width: 150 }} value={status} onChange={e => setStatus(e.target.value)}>
          <option value="all">All statuses</option><option value="available">Available</option><option value="booked">Booked</option><option value="reserved">Reserved</option>
        </select>
        <div className="av-week">
          <button onClick={() => { setWeek(week - 1); onToast('Previous week'); }}><SI name="chevLeft" size={15} /></button>
          <b>Jun 15 – 21, 2026</b>
          <button onClick={() => { setWeek(week + 1); onToast('Next week'); }}><SI name="chevRight" size={15} /></button>
        </div>
        <div className="ia-toolbar" style={{ margin: 0, marginLeft: 'auto' }}>
          <div className="av-toggle">
            <button data-on={view === 'grid'} onClick={() => setView('grid')}><SI name="grid" size={14} />Week</button>
            <button data-on={view === 'table'} onClick={() => setView('table')}><SI name="list" size={14} />Table</button>
          </div>
        </div>
      </div>

      <div className="av-legend" style={{ marginBottom: 14 }}>
        <span className="it"><span className="sw" style={{ background: 'var(--green-500)' }} />Available</span>
        <span className="it"><span className="sw" style={{ background: 'var(--st-rejected-dot)' }} />Booked</span>
        <span className="it"><span className="sw" style={{ background: 'var(--st-pending-dot)' }} />Reserved</span>
      </div>

      {view === 'grid' ? (
        <div className="av-grid">
          {AV_WEEK.map((lbl, di) => {
            const slots = days[di].filter(filt);
            const tot = days[di].reduce((s, x) => s + x.cap, 0);
            const booked = days[di].reduce((s, x) => s + x.booked, 0);
            return (
              <div className="av-day" key={lbl}>
                <div className="av-day__head">
                  <div className="d1">
                    <span><span className="dow">{lbl.split(' ')[0]}</span> <span className="num">{lbl.split(' ')[1]}</span></span>
                    {days[di].length > 0 && <button className="del" title="Delete all slots this day" onClick={() => setConfirm(di)}><SI name="x" size={13} /></button>}
                  </div>
                  {tot > 0 && (<><div className="av-util"><span style={{ width: Math.round(booked / tot * 100) + '%' }} /></div><div className="ut">{booked}/{tot} booked</div></>)}
                </div>
                <div className="av-day__body">
                  {slots.length === 0 ? <div className="av-day__empty">No slots</div> : slots.map(s => (
                    <div className={'av-slot ' + s.status} key={s.id} onClick={() => onToast(s.t + ' · ' + s.status + ' · capacity ' + s.cap)}>
                      <span className="t">{s.t}</span>
                      <span className="c">{s.booked}/{s.cap} booked</span>
                      <button className="x" title="Delete slot" onClick={e => { e.stopPropagation(); removeSlot(di, s.id); }}><SI name="x" size={12} /></button>
                    </div>
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      ) : (
        <div className="ia-wrap">
          <table className="ia-table">
            <thead><tr><th style={{ width: 38 }}></th><th>Location</th><th>Date</th><th>Available</th><th>Booked</th><th>Reserved</th><th>Total slots</th><th style={{ textAlign: 'right' }}>Action</th></tr></thead>
            <tbody>
              {AV_WEEK.map((lbl, di) => {
                const slots = days[di];
                if (!slots.length) return null;
                const cnt = st => slots.filter(s => s.status === st).length;
                return (
                  <React.Fragment key={lbl}>
                    <tr onClick={() => setOpen(o => ({ ...o, [di]: !o[di] }))}>
                      <td><span style={{ color: 'var(--n-400)', display: 'inline-flex', transform: open[di] ? 'rotate(90deg)' : 'none', transition: 'transform .15s' }}><SI name="chevRight" size={15} /></span></td>
                      <td>{loc}</td><td className="ia-sub" style={{ fontFamily: 'var(--font-num)', color: 'var(--n-800)' }}>{lbl}, Jun 2026</td>
                      <td className="num">{cnt('available')}</td><td className="num">{cnt('booked')}</td><td className="num">{cnt('reserved')}</td><td className="num">{slots.length}</td>
                      <td style={{ textAlign: 'right' }} onClick={e => e.stopPropagation()}><button className="ra-rowbtn danger" title="Delete day" onClick={() => setConfirm(di)}><SI name="x" size={14} /></button></td>
                    </tr>
                    {open[di] && slots.map(s => (
                      <tr key={s.id} style={{ background: 'var(--n-25)' }}>
                        <td></td>
                        <td colSpan={2} className="ia-sub" style={{ fontFamily: 'var(--font-num)' }}>{s.t}</td>
                        <td colSpan={3}><span className={'av-slot ' + s.status} style={{ padding: '3px 10px', display: 'inline-block', cursor: 'default' }}>{s.status[0].toUpperCase() + s.status.slice(1)}</span></td>
                        <td className="num">{s.booked}/{s.cap}</td>
                        <td style={{ textAlign: 'right' }}><button className="ra-rowbtn danger" onClick={() => removeSlot(di, s.id)}><SI name="x" size={14} /></button></td>
                      </tr>
                    ))}
                  </React.Fragment>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {confirm != null && (
        <div className="ra-scrim" onClick={() => setConfirm(null)}>
          <div className="ra-modal ra-modal--md" onClick={e => e.stopPropagation()}>
            <div className="ra-modal__head"><h3>Delete all slots on {AV_WEEK[confirm]}?</h3><button className="ext-iconbtn x" onClick={() => setConfirm(null)}><SI name="x" size={17} /></button></div>
            <div className="ra-modal__body"><p style={{ margin: 0, fontSize: 14, color: 'var(--n-600)' }}>This removes <b>{days[confirm].length} slot{days[confirm].length === 1 ? '' : 's'}</b> at {loc} on {AV_WEEK[confirm]}, Jun 2026. Booked slots keep their appointments and must be rescheduled separately.</p></div>
            <div className="ra-modal__foot"><button className="af-btn af-btn--ghost" onClick={() => setConfirm(null)}>Cancel</button><button className="af-btn af-btn--primary" style={{ background: 'var(--st-rejected-fg)' }} onClick={() => clearDay(confirm)}><SI name="x" size={15} />Delete all</button></div>
          </div>
        </div>
      )}
    </>
  );
}

/* ---------------- Generate slots ---------------- */
const GN_DOW = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
function GenPage({ onToast }) {
  const [loc, setLoc] = useStateS('');
  const [from, setFrom] = useStateS('2026-06-15');
  const [to, setTo] = useStateS('2026-06-19');
  const [dows, setDows] = useStateS([false, true, true, true, true, true, false]);
  const [mode, setMode] = useStateS('range');      // 'range' | 'pick'
  const [selDays, setSelDays] = useStateS([]);     // ISO dates picked on the calendar
  const [ranges, setRanges] = useStateS([{ from: '08:30', to: '11:30', dur: '' }]);
  const [cap, setCap] = useStateS(3);
  const [dur, setDur] = useStateS(60);
  const [types, setTypes] = useStateS([]);
  const [preview, setPreview] = useStateS(null);

  function setRange(i, k, v) { setRanges(r => r.map((x, j) => j === i ? { ...x, [k]: v } : x)); }
  function genPreview() {
    if (!loc) { onToast('Select a location first.'); return; }
    if (mode === 'pick' && selDays.length === 0) { onToast('Pick at least one day on the calendar.'); return; }
    const labels = mode === 'pick'
      ? selDays.slice().sort().map(iso => { const dd = new Date(iso + 'T00:00:00'); return GN_DOW[dd.getDay()] + ' ' + dd.getDate(); })
      : ['Mon 15', 'Tue 16', 'Wed 17', 'Thu 18', 'Fri 19'];
    const days = labels.map((lbl, i) => ({
      lbl,
      slots: ranges.flatMap((r, ri) => {
        const startH = parseInt(r.from.split(':')[0], 10);
        const n = Math.max(1, Math.round(((parseInt(r.to.split(':')[0], 10) - startH) * 60) / (parseInt(r.dur || dur, 10))));
        return Array.from({ length: n }, (_, k) => {
          const h = startH + Math.floor((k * (parseInt(r.dur || dur, 10))) / 60);
          const conflict = i === 1 && k === 0 && ri === 0; // mock: Tue first slot conflicts
          return { id: i + '-' + ri + '-' + k, t: (h > 12 ? h - 12 : h) + ':' + (r.from.split(':')[1]) + (h >= 12 ? ' PM' : ' AM'), conflict };
        });
      }),
    }));
    setPreview(days);
  }
  function removePrev(di, id) { setPreview(p => p.map((d, i) => i === di ? { ...d, slots: d.slots.filter(s => s.id !== id) } : d)); }
  const totalSlots = preview ? preview.reduce((s, d) => s + d.slots.length, 0) : 0;
  const conflicts = preview ? preview.reduce((s, d) => s + d.slots.filter(x => x.conflict).length, 0) : 0;

  return (
    <>
      <div className="ia-head"><div><h1>Generate slots</h1><p>Define the rules, preview the result, then submit. Conflicting slots must be removed first.</p></div></div>

      <div className="ra-card" style={{ marginBottom: 16 }}>
        <div className="ra-card__body" style={{ padding: 22 }}>
          <div className="ra-grid">
            <div className="ra-field col-4"><label>Location <span className="req">*</span></label>
              <select className="ra-select" value={loc} onChange={e => setLoc(e.target.value)}><option value="">Select location</option>{AV_LOCS.concat('San Diego — Hillcrest').map(l => <option key={l}>{l}</option>)}</select>
            </div>
            <div className="ra-field col-8"><label>Schedule pattern</label>
              <div className="av-toggle" style={{ alignSelf: 'flex-start' }}>
                <button data-on={mode === 'range'} onClick={() => setMode('range')}><SI name="refresh" size={14} />Date range + weekdays</button>
                <button data-on={mode === 'pick'} onClick={() => setMode('pick')}><SI name="calendar" size={14} />Pick days on calendar</button>
              </div>
            </div>
            {mode === 'range' ? (<>
              <div className="ra-field col-4"><label>From date <span className="req">*</span></label><input className="ra-input" type="date" value={from} onChange={e => setFrom(e.target.value)} /></div>
              <div className="ra-field col-4"><label>To date <span className="req">*</span></label><input className="ra-input" type="date" value={to} onChange={e => setTo(e.target.value)} /></div>
              <div className="ra-field col-12"><label>Weekdays</label>
                <div className="gn-days">{GN_DOW.map((d, i) => <button key={d} className="gn-day" data-on={dows[i]} onClick={() => setDows(a => a.map((x, j) => j === i ? !x : x))}>{d}</button>)}</div>
              </div>
            </>) : (
              <div className="ra-field col-12"><label>Days <span className="req">*</span> <span className="opt">(click to toggle — supports irregular patterns)</span></label>
                <div className="gn-cal">
                  <div className="gn-cal__head">
                    <button className="ra-rowbtn" onClick={() => onToast('Previous month')}><SI name="chevLeft" size={14} /></button>
                    <b>June 2026</b>
                    <button className="ra-rowbtn" onClick={() => onToast('Next month')}><SI name="chevRight" size={14} /></button>
                  </div>
                  <div className="gn-cal__grid">
                    {GN_DOW.map(d => <span className="gn-cal__dow" key={d}>{d[0]}</span>)}
                    <span></span>
                    {Array.from({ length: 30 }, (_, i) => {
                      const iso = '2026-06-' + String(i + 1).padStart(2, '0');
                      const on = selDays.includes(iso);
                      return <button key={iso} className="gn-cal__day" data-on={on} onClick={() => setSelDays(s => on ? s.filter(x => x !== iso) : [...s, iso])}>{i + 1}</button>;
                    })}
                  </div>
                  <div className="gn-cal__hint">
                    <span><b style={{ fontFamily: 'var(--font-num)' }}>{selDays.length}</b> day{selDays.length === 1 ? '' : 's'} selected</span>
                    {selDays.length > 0 && <button onClick={() => setSelDays([])}>Clear</button>}
                  </div>
                </div>
              </div>
            )}
            <div className="ra-field col-12"><label>Time ranges <span className="req">*</span></label>
              {ranges.map((r, i) => (
                <div className="gn-range" key={i}>
                  <div className="ra-field"><label style={{ fontSize: 11.5 }}>From time</label><input className="ra-input" type="time" value={r.from} onChange={e => setRange(i, 'from', e.target.value)} /></div>
                  <div className="ra-field"><label style={{ fontSize: 11.5 }}>To time</label><input className="ra-input" type="time" value={r.to} onChange={e => setRange(i, 'to', e.target.value)} /></div>
                  <div className="ra-field"><label style={{ fontSize: 11.5 }}>Duration override <span className="opt">(min)</span></label><input className="ra-input" type="number" min="1" placeholder={String(dur)} value={r.dur} onChange={e => setRange(i, 'dur', e.target.value)} /></div>
                  <button className="ra-rowbtn danger" disabled={ranges.length <= 1} style={{ height: 42, width: 40, opacity: ranges.length <= 1 ? .4 : 1 }} onClick={() => setRanges(rs => rs.filter((_, j) => j !== i))}><SI name="x" size={14} /></button>
                </div>
              ))}
              <button className="af-btn af-btn--ghost af-btn--sm" onClick={() => setRanges(r => [...r, { from: '13:00', to: '16:00', dur: '' }])}><SI name="plus" size={13} />Add time range</button>
            </div>
            <div className="ra-field col-3"><label>Capacity <span className="req">*</span></label><input className="ra-input" type="number" min="1" value={cap} onChange={e => setCap(e.target.value)} /><div className="ra-hint">Appointments allowed per slot.</div></div>
            <div className="ra-field col-3"><label>Default duration (min) <span className="req">*</span></label><input className="ra-input" type="number" min="1" value={dur} onChange={e => setDur(e.target.value)} /></div>
            <div className="ra-field col-6"><label>Appointment types <span className="opt">(empty = any type)</span></label>
              <div className="gn-types">{window.MOCK.TYPES.map(t => <button key={t} className="gn-type" data-on={types.includes(t)} onClick={() => setTypes(ts => ts.includes(t) ? ts.filter(x => x !== t) : [...ts, t])}>{t}</button>)}</div>
            </div>
          </div>
          <div className="mp-editfoot">
            <button className="af-btn af-btn--ghost" onClick={() => { setPreview(null); setRanges([{ from: '08:30', to: '11:30', dur: '' }]); setTypes([]); setSelDays([]); onToast('Form reset.'); }}>Reset</button>
            <button className="af-btn af-btn--primary" onClick={genPreview}><SI name="refresh" size={15} />Generate preview</button>
          </div>
        </div>
      </div>

      {preview && (
        <>
          <div className={'gn-summary' + (conflicts ? ' conflict' : '')}>
            <SI name={conflicts ? 'alert' : 'check'} size={16} />
            <span><b>{totalSlots}</b> slots across <b>{preview.length}</b> days at {loc}.</span>
            {conflicts > 0 ? <span><b>{conflicts}</b> conflict{conflicts === 1 ? '' : 's'} — remove the red slots to enable submit.</span> : <span>No conflicts.</span>}
          </div>
          <div className="av-grid" style={{ gridTemplateColumns: 'repeat(' + Math.min(preview.length, 7) + ', 1fr)', marginBottom: 16 }}>
            {preview.map((d, di) => (
              <div className="av-day" key={d.lbl}>
                <div className="av-day__head"><div className="d1"><span><span className="dow">{d.lbl.split(' ')[0]}</span> <span className="num">{d.lbl.split(' ')[1]}</span></span></div><div className="ut">{d.slots.length} slots</div></div>
                <div className="av-day__body">
                  {d.slots.map(s => (
                    <div className={'av-slot ' + (s.conflict ? 'conflict' : 'available')} key={s.id} title={s.conflict ? 'Conflicts with an existing slot' : ''}>
                      <span className="t">{s.t}</span>
                      {s.conflict && <span className="c">Conflict — remove</span>}
                      <button className="x" onClick={() => removePrev(di, s.id)}><SI name="x" size={12} /></button>
                    </div>
                  ))}
                  {d.slots.length === 0 && <div className="av-day__empty">No slots</div>}
                </div>
              </div>
            ))}
          </div>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button className="af-btn af-btn--ghost" onClick={() => setPreview(null)}>Cancel</button>
            <button className="af-btn af-btn--green" disabled={conflicts > 0 || totalSlots === 0} style={conflicts > 0 || totalSlots === 0 ? { opacity: .5, cursor: 'not-allowed' } : null}
              onClick={() => { setPreview(null); onToast(totalSlots + ' slots created at ' + loc + '.'); }}>
              <SI name="check" size={16} />Submit {totalSlots} slots
            </button>
          </div>
        </>
      )}
    </>
  );
}

/* ---------------- Locations & WCAB ---------------- */
function LocPage({ onToast }) {
  const [rows, setRows] = useStateS(LW_LOCS0);
  const [edit, setEdit] = useStateS(null); // null | {row?}
  const blank = { name: '', address: '', city: '', zip: '', state: 'CA', fee: 0, active: true, types: [] };
  const [d, setD] = useStateS(blank);
  function open(row) { setD(row ? { ...row, types: row.types.slice() } : { ...blank }); setEdit(row ? row.id : 'new'); }
  function save() {
    if (!d.name.trim()) { onToast('Name is required.'); return; }
    setRows(rs => edit === 'new' ? [...rs, { ...d, id: Date.now() }] : rs.map(r => r.id === edit ? { ...d, id: edit } : r));
    setEdit(null); onToast('Location saved.');
  }
  return (
    <>
      <div className="ia-head">
        <div><h1>Locations</h1><p>{rows.length} clinic locations</p></div>
        <a className="af-btn af-btn--primary" onClick={() => open(null)}><SI name="plus" size={16} />New location</a>
      </div>
      <div className="ia-wrap">
        <table className="ia-table">
          <thead><tr><th>Name</th><th>Address</th><th>State</th><th>Parking fee</th><th>Appointment types</th><th>Status</th><th style={{ textAlign: 'right' }}>Actions</th></tr></thead>
          <tbody>
            {rows.map(r => (
              <tr key={r.id} onClick={() => open(r)}>
                <td><b style={{ color: 'var(--n-900)' }}>{r.name}</b></td>
                <td className="ia-sub">{r.address}, {r.city} {r.zip}</td>
                <td>{r.state}</td>
                <td className="num">${r.fee}</td>
                <td><span className="lw-chips">{r.types.map(t => <span className="lw-chip" key={t}>{t}</span>)}</span></td>
                <td><span className={'lw-active ' + (r.active ? 'on' : 'off')}>{r.active ? 'Active' : 'Inactive'}</span></td>
                <td style={{ textAlign: 'right' }} onClick={e => e.stopPropagation()}>
                  <span style={{ display: 'inline-flex', gap: 6 }}>
                    <button className="ra-rowbtn" onClick={() => open(r)} title="Edit"><SI name="doc" size={14} /></button>
                    <button className="ra-rowbtn danger" onClick={() => { setRows(rs => rs.filter(x => x.id !== r.id)); onToast('Location deleted.'); }} title="Delete"><SI name="x" size={14} /></button>
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {edit != null && (
        <div className="ra-scrim" onClick={() => setEdit(null)}>
          <div className="ra-modal ra-modal--lg" onClick={e => e.stopPropagation()}>
            <div className="ra-modal__head"><h3>{edit === 'new' ? 'New location' : 'Edit location'}</h3><button className="ext-iconbtn x" onClick={() => setEdit(null)}><SI name="x" size={17} /></button></div>
            <div className="ra-modal__body">
              <div className="ra-grid">
                <div className="ra-field col-6"><label>Name <span className="req">*</span></label><input className="ra-input" maxLength={50} value={d.name} onChange={e => setD({ ...d, name: e.target.value })} /></div>
                <div className="ra-field col-6"><label>Address</label><input className="ra-input" maxLength={100} value={d.address} onChange={e => setD({ ...d, address: e.target.value })} /></div>
                <div className="ra-field col-4"><label>City</label><input className="ra-input" maxLength={50} value={d.city} onChange={e => setD({ ...d, city: e.target.value })} /></div>
                <div className="ra-field col-4"><label>State</label><select className="ra-select" value={d.state} onChange={e => setD({ ...d, state: e.target.value })}>{window.RA.STATES.map(s => <option key={s}>{s}</option>)}</select></div>
                <div className="ra-field col-4"><label>Zip code</label><input className="ra-input" maxLength={15} value={d.zip} onChange={e => setD({ ...d, zip: e.target.value })} /></div>
                <div className="ra-field col-4"><label>Parking fee ($) <span className="req">*</span></label><input className="ra-input" type="number" min="0" value={d.fee} onChange={e => setD({ ...d, fee: e.target.value })} /></div>
                <div className="ra-field col-8"><label>Appointment types <span className="opt">(offered here)</span></label>
                  <div className="gn-types">{window.MOCK.TYPES.map(t => <button key={t} className="gn-type" data-on={d.types.includes(t)} onClick={() => setD({ ...d, types: d.types.includes(t) ? d.types.filter(x => x !== t) : [...d.types, t] })}>{t}</button>)}</div>
                </div>
                <div className="ra-field col-12">
                  <label className="ra-switch" style={{ marginTop: 4 }}>Active — bookable by external users
                    <input type="checkbox" checked={d.active} onChange={e => setD({ ...d, active: e.target.checked })} />
                    <span className="track" />
                  </label>
                </div>
              </div>
            </div>
            <div className="ra-modal__foot"><button className="af-btn af-btn--ghost" onClick={() => setEdit(null)}>Cancel</button><button className="af-btn af-btn--primary" onClick={save}><SI name="check" size={15} />Save</button></div>
          </div>
        </div>
      )}
    </>
  );
}

function WcabPage({ onToast }) {
  const [rows, setRows] = useStateS(LW_WCAB0);
  const [edit, setEdit] = useStateS(null);
  const [d, setD] = useStateS({ name: '', code: '' });
  function open(row) { setD(row ? { ...row } : { name: '', code: '' }); setEdit(row ? row.id : 'new'); }
  function save() {
    if (!d.name.trim()) { onToast('Name is required.'); return; }
    setRows(rs => edit === 'new' ? [...rs, { ...d, id: Date.now() }] : rs.map(r => r.id === edit ? { ...d, id: edit } : r));
    setEdit(null); onToast('WCAB office saved.');
  }
  return (
    <>
      <div className="ia-head">
        <div><h1>WCAB offices</h1><p>{rows.length} Workers' Compensation Appeals Board venues</p></div>
        <a className="af-btn af-btn--primary" onClick={() => open(null)}><SI name="plus" size={16} />New office</a>
      </div>
      <div className="ia-wrap" style={{ maxWidth: 640 }}>
        <table className="ia-table" style={{ minWidth: 0 }}>
          <thead><tr><th>Office name</th><th>Code</th><th style={{ textAlign: 'right' }}>Actions</th></tr></thead>
          <tbody>
            {rows.map(r => (
              <tr key={r.id} onClick={() => open(r)}>
                <td><b style={{ color: 'var(--n-900)' }}>{r.name}</b></td>
                <td className="num">{r.code}</td>
                <td style={{ textAlign: 'right' }} onClick={e => e.stopPropagation()}>
                  <span style={{ display: 'inline-flex', gap: 6 }}>
                    <button className="ra-rowbtn" onClick={() => open(r)}><SI name="doc" size={14} /></button>
                    <button className="ra-rowbtn danger" onClick={() => { setRows(rs => rs.filter(x => x.id !== r.id)); onToast('Office deleted.'); }}><SI name="x" size={14} /></button>
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {edit != null && (
        <div className="ra-scrim" onClick={() => setEdit(null)}>
          <div className="ra-modal ra-modal--md" onClick={e => e.stopPropagation()}>
            <div className="ra-modal__head"><h3>{edit === 'new' ? 'New WCAB office' : 'Edit WCAB office'}</h3><button className="ext-iconbtn x" onClick={() => setEdit(null)}><SI name="x" size={17} /></button></div>
            <div className="ra-modal__body">
              <div className="ra-grid">
                <div className="ra-field col-8"><label>Office name <span className="req">*</span></label><input className="ra-input" maxLength={100} value={d.name} onChange={e => setD({ ...d, name: e.target.value })} /></div>
                <div className="ra-field col-4"><label>Code</label><input className="ra-input" maxLength={10} value={d.code} onChange={e => setD({ ...d, code: e.target.value })} /></div>
              </div>
            </div>
            <div className="ra-modal__foot"><button className="af-btn af-btn--ghost" onClick={() => setEdit(null)}>Cancel</button><button className="af-btn af-btn--primary" onClick={save}><SI name="check" size={15} />Save</button></div>
          </div>
        </div>
      )}
    </>
  );
}

window.InSched = { AvailPage, GenPage, LocPage, WcabPage };
