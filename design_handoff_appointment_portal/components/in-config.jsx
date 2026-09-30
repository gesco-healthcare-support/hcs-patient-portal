/* global React */
/* ============================================================
   Group 3 — Configuration hub. Left sub-nav (Types · Statuses ·
   Document types · Languages · States) + shared CRUD table with
   system locks, usage counts, and the per-type Field Configuration
   (Visible/Hidden · Editable/Read-only · default value, grouped by
   booking-form step). Renders inside InternalShell.
   ============================================================ */
const { useState: useStateCF } = React;
function CFI({ name, size }) { return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size || 18)} />; }

/* ---------------- seed data ---------------- */
const CF_TYPES0 = [
  { id: 1, name: 'Panel QME', desc: 'Panel Qualified Medical Evaluation', usage: 128, system: true },
  { id: 2, name: 'AME Evaluation', desc: 'Agreed Medical Evaluation', usage: 96, system: true },
  { id: 3, name: 'QME Re-Evaluation', desc: 'Follow-up re-evaluation', usage: 41, system: false },
  { id: 4, name: 'QME Follow-up', desc: '', usage: 22, system: false },
  { id: 5, name: 'Supplemental Report', desc: 'Records-only supplemental', usage: 9, system: false },
];
const CF_STATUSES0 = [
  { id: 1, name: 'Pending', usage: 18, system: true }, { id: 2, name: 'Approved', usage: 42, system: true },
  { id: 3, name: 'Rejected', usage: 5, system: true }, { id: 4, name: 'Cancelled', usage: 11, system: true },
  { id: 5, name: 'Rescheduled', usage: 7, system: true },
];
const CF_DOCTYPES0 = [
  { id: 1, name: 'Medical Records', usage: 214, system: false, required: true },
  { id: 2, name: 'Cover Letter', usage: 102, system: false, required: true },
  { id: 3, name: 'Panel Strike List', usage: 67, system: true, required: true },
  { id: 4, name: 'Deposition Transcript', usage: 31, system: false, required: false },
  { id: 5, name: 'Correspondence', usage: 88, system: false, required: false },
];
const CF_LANGS0 = [
  { id: 1, name: 'English', usage: 301, system: true }, { id: 2, name: 'Spanish', usage: 122, system: false },
  { id: 3, name: 'Mandarin', usage: 18, system: false }, { id: 4, name: 'Vietnamese', usage: 12, system: false },
  { id: 5, name: 'Tagalog', usage: 7, system: false }, { id: 6, name: 'Korean', usage: 5, system: false }, { id: 7, name: 'Armenian', usage: 4, system: false },
];
const CF_STATES0 = [
  { id: 1, name: 'CA — California', usage: 412, system: true }, { id: 2, name: 'NV — Nevada', usage: 9, system: false },
  { id: 3, name: 'AZ — Arizona', usage: 6, system: false }, { id: 4, name: 'OR — Oregon', usage: 3, system: false },
];

/* booking-form fields configurable per type */
/* booking-form fields configurable per type: [name, hidden, readOnly, default, required] */
const CF_FIELDS0 = [
  ['Schedule', [['Panel number', false, false, 'P-', true], ['Appointment time', false, false, '', true]]],
  ['Patient', [['Middle name', false, false, '', false], ['Social Security #', false, false, '', false], ['Interpreter', false, false, 'No', false], ['Referred by', true, false, '', false], ['Appointment language', false, false, 'English', false]]],
  ['Parties', [['Applicant attorney', false, false, '', true], ['Defense attorney', false, false, '', true], ['Insurance', false, false, '', true], ['Fax numbers', true, false, '', false]]],
  ['Claim', [['WCAB office', false, false, '', false], ['ADJ #', false, false, '', true]]],
  ['Documents', [['Panel strike list flag', false, true, '', true]]],
];
function mkFieldState() {
  const o = {};
  CF_FIELDS0.forEach(([g, fields]) => fields.forEach(([nm, hidden, ro, dv, req]) => { o[nm] = { hidden, ro, dv, req: !!req }; }));
  return o;
}

const CF_SECTIONS = [
  ['types', 'Appointment Types', 'list'], ['statuses', 'Appointment Statuses', 'check'],
  ['doctypes', 'Document Types', 'doc'], ['languages', 'Appointment Languages', 'list'], ['states', 'States', 'map'],
];

function InConfig({ onToast }) {
  const [section, setSection] = useStateCF('types');
  const [data, setData] = useStateCF({ types: CF_TYPES0, statuses: CF_STATUSES0, doctypes: CF_DOCTYPES0, languages: CF_LANGS0, states: CF_STATES0 });
  const [edit, setEdit] = useStateCF(null);          // null | 'new' | row id
  const [d, setD] = useStateCF({});
  const [fcOpen, setFcOpen] = useStateCF(null);      // type id with field config expanded
  const [fields, setFields] = useStateCF(() => ({ 1: mkFieldState() }));

  const rows = data[section];
  const isTypes = section === 'types';
  const isDocs = section === 'doctypes';
  const label = CF_SECTIONS.find(s => s[0] === section)[1];
  const singular = { types: 'appointment type', statuses: 'status', doctypes: 'document type', languages: 'language', states: 'state' }[section];

  function setRows(fn) { setData(prev => ({ ...prev, [section]: fn(prev[section]) })); }
  function open(row) { setD(row ? { ...row } : { name: '', desc: '', required: false }); setEdit(row ? row.id : 'new'); }
  function save() {
    if (!d.name.trim()) { onToast('Name is required.'); return; }
    setRows(rs => edit === 'new' ? [...rs, { ...d, id: Date.now(), usage: 0, system: false }] : rs.map(r => r.id === edit ? { ...r, ...d } : r));
    setEdit(null); onToast(label.slice(0, -1) + ' saved.');
  }
  function tryDelete(row) {
    if (row.system) { onToast('System ' + singular + 's can\u2019t be deleted.'); return; }
    if (row.usage > 0) { onToast('In use by ' + row.usage + ' appointments — can\u2019t delete.'); return; }
    setRows(rs => rs.filter(r => r.id !== row.id)); onToast(label.slice(0, -1) + ' deleted.');
  }
  function toggleFc(id) {
    setFcOpen(fcOpen === id ? null : id);
    setFields(f => f[id] ? f : { ...f, [id]: mkFieldState() });
  }
  function setField(typeId, nm, patch) {
    setFields(f => ({ ...f, [typeId]: { ...f[typeId], [nm]: { ...f[typeId][nm], ...patch } } }));
  }

  return (
    <>
      <div className="ia-head">
        <div><h1>Configuration</h1><p>Lookups and form rules used across the appointment workflow.</p></div>
        <a className="af-btn af-btn--primary" onClick={() => open(null)}><CFI name="plus" size={16} />New {singular}</a>
      </div>

      <div className="cf">
        <nav className="cf-rail">
          {CF_SECTIONS.map(([k, lbl, ic]) => (
            <button key={k} className="cf-railitem" data-on={section === k} onClick={() => { setSection(k); setEdit(null); setFcOpen(null); }}>
              <span className="i"><CFI name={ic} size={16} /></span>{lbl}<span className="cnt">{data[k].length}</span>
            </button>
          ))}
        </nav>

        <div className="ia-wrap">
          <table className="ia-table" style={{ minWidth: 0 }}>
            <thead><tr>
              {isTypes && <th style={{ width: 38 }}></th>}
              <th>Name</th>
              {isTypes && <th>Description</th>}
              <th>Usage</th><th>Flags</th><th style={{ textAlign: 'right' }}>Actions</th>
            </tr></thead>
            <tbody>
              {rows.map(r => (
                <React.Fragment key={r.id}>
                  <tr onClick={() => isTypes ? toggleFc(r.id) : open(r)}>
                    {isTypes && <td><span style={{ color: 'var(--n-400)', display: 'inline-flex', transform: fcOpen === r.id ? 'rotate(90deg)' : 'none', transition: 'transform .15s' }}><CFI name="chevRight" size={15} /></span></td>}
                    <td><b style={{ color: 'var(--n-900)' }}>{r.name}</b></td>
                    {isTypes && <td className="ia-sub">{r.desc || '—'}</td>}
                    <td><span className="cf-usage">{r.usage} appointment{r.usage === 1 ? '' : 's'}</span></td>
                    <td>
                      <span style={{ display: 'inline-flex', gap: 6 }}>
                        {r.system && <span className="cf-lock"><CFI name="settings" size={11} />System</span>}
                        {isDocs && r.required && <span className="cf-req"><CFI name="doc" size={11} />Required</span>}
                      </span>
                    </td>
                    <td style={{ textAlign: 'right' }} onClick={e => e.stopPropagation()}>
                      <span style={{ display: 'inline-flex', gap: 6 }}>
                        {isTypes && <button className="ra-rowbtn" title="Field configuration" onClick={() => toggleFc(r.id)}><CFI name="settings" size={14} /></button>}
                        <button className="ra-rowbtn" title="Edit" onClick={() => open(r)}><CFI name="doc" size={14} /></button>
                        <button className="ra-rowbtn danger" title={r.system ? 'System — locked' : r.usage > 0 ? 'In use — locked' : 'Delete'}
                          style={(r.system || r.usage > 0) ? { opacity: .35, cursor: 'not-allowed' } : null}
                          onClick={() => tryDelete(r)}><CFI name="x" size={14} /></button>
                      </span>
                    </td>
                  </tr>
                  {isTypes && fcOpen === r.id && (
                    <tr><td colSpan="6" style={{ padding: 0 }}>
                      <div className="cf-fc">
                        <div className="cf-fc__head">
                          <h4><CFI name="settings" size={15} />Field configuration — {r.name}</h4>
                          <button className="af-btn af-btn--primary af-btn--sm" onClick={() => { setFcOpen(null); onToast('Field configuration saved for ' + r.name + '.'); }}><CFI name="check" size={14} />Save configuration</button>
                        </div>
                        <div style={{ fontSize: 12, color: 'var(--n-500)', margin: '0 0 6px' }}>The toggles control behavior (visibility · editability · requiredness); the text box only <b>pre-fills</b> the field's value on the booking form — it is never parsed.</div>
                        {CF_FIELDS0.map(([group, flds]) => (
                          <React.Fragment key={group}>
                            <div className="cf-fc__group">{group}</div>
                            {flds.map(([nm]) => {
                              const st = (fields[r.id] || {})[nm] || { hidden: false, ro: false, dv: '', req: false };
                              return (
                                <div className="cf-fc__row" key={nm}>
                                  <span className="nm">{nm}</span>
                                  <span className="cf-seg">
                                    <button data-on={!st.hidden} onClick={() => setField(r.id, nm, { hidden: false })}>Visible</button>
                                    <button data-on={st.hidden} onClick={() => setField(r.id, nm, { hidden: true })}>Hidden</button>
                                  </span>
                                  <span className="cf-seg warn">
                                    <button data-on={!st.ro} onClick={() => setField(r.id, nm, { ro: false })} disabled={st.hidden}>Editable</button>
                                    <button data-on={st.ro} onClick={() => setField(r.id, nm, { ro: true })} disabled={st.hidden}>Read-only</button>
                                  </span>
                                  <span className="cf-seg green">
                                    <button data-on={st.req} onClick={() => setField(r.id, nm, { req: true })} disabled={st.hidden}>Required</button>
                                    <button data-on={!st.req} onClick={() => setField(r.id, nm, { req: false })} disabled={st.hidden}>Optional</button>
                                  </span>
                                  <input className="dv" placeholder="Pre-filled value — blank for none" value={st.dv} disabled={st.hidden}
                                    onChange={e => setField(r.id, nm, { dv: e.target.value })} />
                                </div>
                              );
                            })}
                          </React.Fragment>
                        ))}
                      </div>
                    </td></tr>
                  )}
                </React.Fragment>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {edit != null && (
        <div className="ra-scrim" onClick={() => setEdit(null)}>
          <div className="ra-modal ra-modal--md" onClick={e => e.stopPropagation()}>
            <div className="ra-modal__head"><h3>{edit === 'new' ? 'New ' + singular : 'Edit ' + singular}</h3><button className="ext-iconbtn x" onClick={() => setEdit(null)}><CFI name="x" size={17} /></button></div>
            <div className="ra-modal__body">
              <div className="ra-grid">
                <div className={'ra-field ' + (isTypes ? 'col-6' : 'col-12')}><label>Name <span className="req">*</span></label>
                  <input className="ra-input" maxLength={100} value={d.name || ''} onChange={e => setD({ ...d, name: e.target.value })} autoFocus /></div>
                {isTypes && <div className="ra-field col-6"><label>Description</label>
                  <input className="ra-input" maxLength={200} value={d.desc || ''} onChange={e => setD({ ...d, desc: e.target.value })} /></div>}
                {isDocs && (
                  <div className="ra-field col-12">
                    <label className="ra-switch" style={{ marginTop: 4 }}>Required by default — tracked in the Document Manager
                      <input type="checkbox" checked={!!d.required} onChange={e => setD({ ...d, required: e.target.checked })} />
                      <span className="track" />
                    </label>
                  </div>
                )}
                {d.system && <div className="ra-field col-12"><div className="ra-note"><span className="i"><CFI name="alert" size={15} /></span><span>This is a <b>system {singular}</b> — it can be renamed but not deleted.</span></div></div>}
              </div>
            </div>
            <div className="ra-modal__foot"><button className="af-btn af-btn--ghost" onClick={() => setEdit(null)}>Cancel</button><button className="af-btn af-btn--primary" onClick={save}><CFI name="check" size={15} />Save</button></div>
          </div>
        </div>
      )}
    </>
  );
}

window.InConfig = InConfig;
