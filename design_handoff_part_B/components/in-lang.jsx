/* global React */
/* ============================================================
   Host / IT-Admin — Language Management. Two sections via cf-rail:
   Languages (table + New-language modal + set-default) and Language
   Texts (resource + culture selectors, searchable key/value table with
   editable override, save-per-row + reset-to-default). Loading + empty.
   Renders inside InternalShell (itadmin).
   ============================================================ */
const { useState: useStateLG } = React;
function LGX({ name, size }) { return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size || 18)} />; }

const LG_SEED = [
  { id: 'l1', name: 'English', culture: 'en', ui: 'en', flag: 'EN', def: true, enabled: true },
  { id: 'l2', name: 'Spanish', culture: 'es', ui: 'es', flag: 'ES', def: false, enabled: true },
  { id: 'l3', name: 'Chinese (Simplified)', culture: 'zh-Hans', ui: 'zh-Hans', flag: 'ZH', def: false, enabled: true },
  { id: 'l4', name: 'Vietnamese', culture: 'vi', ui: 'vi', flag: 'VI', def: false, enabled: true },
  { id: 'l5', name: 'Korean', culture: 'ko', ui: 'ko', flag: 'KO', def: false, enabled: false },
  { id: 'l6', name: 'Tagalog', culture: 'tl', ui: 'tl', flag: 'TL', def: false, enabled: false },
];
const LT_RESOURCES = ['CaseEvaluation', 'AbpUi', 'AbpIdentity'];
const LT_SEED = {
  CaseEvaluation: [
    { key: 'Menu:Home', base: 'Home', es: 'Inicio' },
    { key: 'Menu:Appointments', base: 'Appointments', es: 'Citas' },
    { key: 'Appointment:Approve', base: 'Approve', es: 'Aprobar' },
    { key: 'Appointment:Reject', base: 'Reject', es: 'Rechazar' },
    { key: 'Status:Pending', base: 'Pending', es: 'Pendiente' },
    { key: 'Status:InfoRequested', base: 'Info Requested', es: '' },
    { key: 'Document:PanelStrikeList', base: 'Panel Strike List', es: '' },
  ],
  AbpUi: [
    { key: 'Save', base: 'Save', es: 'Guardar' },
    { key: 'Cancel', base: 'Cancel', es: 'Cancelar' },
    { key: 'Delete', base: 'Delete', es: 'Eliminar' },
  ],
  AbpIdentity: [
    { key: 'DisplayName:UserName', base: 'User name', es: 'Nombre de usuario' },
    { key: 'DisplayName:Email', base: 'Email', es: 'Correo electrónico' },
  ],
};

const LG_SECTIONS = [['languages', 'Languages', 'globe'], ['texts', 'Language Texts', 'doc']];

function InLang({ onToast, view }) {
  const [section, setSection] = useStateLG('languages');
  return (
    <>
      <div className="ia-head"><div><h1>Language management</h1><p>Configure available languages and translate interface text per culture.</p></div></div>
      <div className="cf">
        <nav className="cf-rail">
          {LG_SECTIONS.map(([k, lbl, ic]) => (
            <button key={k} className="cf-railitem" data-on={section === k} onClick={() => setSection(k)}>
              <span className="i"><LGX name={ic} size={16} /></span>{lbl}
            </button>
          ))}
        </nav>
        <div>
          {section === 'languages' && <LangsPage onToast={onToast} view={view} />}
          {section === 'texts' && <TextsPage onToast={onToast} view={view} />}
        </div>
      </div>
    </>
  );
}

/* ---------------- Section 1: Languages ---------------- */
function LangsPage({ onToast, view }) {
  const [rows, setRows] = useStateLG(LG_SEED);
  const [edit, setEdit] = useStateLG(null);
  const [d, setD] = useStateLG({});
  const loading = view === 'loading';
  const empty = view === 'empty';
  const list = empty ? [] : rows;

  function open(row) { setD(row ? { ...row } : { name: '', culture: '', ui: '', flag: '', enabled: true }); setEdit(row ? row.id : 'new'); }
  function save() {
    if (!d.name.trim() || !d.culture.trim()) { onToast('Display name and culture are required.'); return; }
    const flag = (d.flag || d.culture.slice(0, 2)).toUpperCase();
    setRows(rs => edit === 'new' ? [...rs, { ...d, flag, ui: d.ui || d.culture, id: 'l' + Date.now(), def: false }] : rs.map(r => r.id === edit ? { ...r, ...d, flag } : r));
    setEdit(null); onToast('Language saved.');
  }
  function setDefault(id) { setRows(rs => rs.map(r => ({ ...r, def: r.id === id, enabled: r.id === id ? true : r.enabled }))); onToast('Default language updated.'); }
  function setF(k, v) { setD(p => ({ ...p, [k]: v })); }

  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 12 }}>
        {!loading && <button className="af-btn af-btn--primary" onClick={() => open(null)}><LGX name="plus" size={15} />New language</button>}
      </div>
      <div className="ia-wrap">
        <table className="ia-table" style={{ minWidth: 0 }}>
          <thead><tr><th>Language</th><th>Culture code</th><th>UI culture</th><th>Default</th><th>Status</th><th style={{ textAlign: 'right' }}>Actions</th></tr></thead>
          <tbody>
            {loading && [0, 1, 2, 3].map(i => (
              <tr key={i} style={{ cursor: 'default' }}>
                <td><div className="lg-name"><span className="lg-flag" style={{ background: 'var(--n-100)' }} /><div className="sk-bar" style={{ width: 110 }} /></div></td>
                <td><div className="sk-bar" style={{ width: 50 }} /></td><td><div className="sk-bar" style={{ width: 50 }} /></td>
                <td><div className="sk-bar" style={{ width: 48 }} /></td><td><div className="sk-bar" style={{ width: 56 }} /></td><td><div className="sk-bar" style={{ width: 70, marginLeft: 'auto' }} /></td>
              </tr>
            ))}
            {!loading && list.map(r => (
              <tr key={r.id} onClick={() => open(r)}>
                <td><span className="lg-name"><span className="lg-flag">{r.flag}</span>{r.name}</span></td>
                <td><span className="lg-culture">{r.culture}</span></td>
                <td><span className="lg-culture">{r.ui}</span></td>
                <td>{r.def ? <span className="lg-default"><LGX name="check" size={12} />Default</span> : <span className="ia-sub">—</span>}</td>
                <td><span className={'lw-active ' + (r.enabled ? 'on' : 'off')}>{r.enabled ? 'Enabled' : 'Disabled'}</span></td>
                <td style={{ textAlign: 'right' }} onClick={e => e.stopPropagation()}>
                  <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}>
                    {!r.def && <button className="lg-setdef" title="Make default" onClick={() => setDefault(r.id)}>Set default</button>}
                    <button className="ra-rowbtn" title="Edit" onClick={() => open(r)}><LGX name="edit" size={14} /></button>
                    {!r.def && <button className="ra-rowbtn danger" title="Delete" onClick={() => { setRows(rs => rs.filter(x => x.id !== r.id)); onToast('Language removed.'); }}><LGX name="trash" size={14} /></button>}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {empty && (
          <div className="ia-empty"><LGX name="globe" size={30} /><b>No languages configured</b>Add a language to make it available across the portal.
            <div style={{ marginTop: 16 }}><button className="af-btn af-btn--primary" onClick={() => open(null)}><LGX name="plus" size={15} />New language</button></div>
          </div>
        )}
      </div>

      {edit != null && (
        <div className="ra-scrim" onClick={() => setEdit(null)}>
          <div className="ra-modal ra-modal--md" onClick={e => e.stopPropagation()}>
            <div className="ra-modal__head">
              <span className="ic tint-blue" style={{ width: 38, height: 38, borderRadius: 11, display: 'flex', alignItems: 'center', justifyContent: 'center' }}><LGX name="globe" size={18} /></span>
              <h3>{edit === 'new' ? 'New language' : 'Edit language'}</h3>
              <button className="ext-iconbtn x" onClick={() => setEdit(null)}><LGX name="x" size={17} /></button>
            </div>
            <div className="ra-modal__body">
              <div className="ra-grid">
                <div className="ra-field col-8"><label>Display name <span className="req">*</span></label><input className="ra-input" maxLength={64} placeholder="e.g. Spanish" value={d.name || ''} onChange={e => setF('name', e.target.value)} /></div>
                <div className="ra-field col-4"><label>Flag code <span className="opt">(optional)</span></label><input className="ra-input" style={{ fontFamily: 'var(--font-num)', textTransform: 'uppercase' }} maxLength={3} placeholder="ES" value={d.flag || ''} onChange={e => setF('flag', e.target.value)} /></div>
                <div className="ra-field col-6"><label>Culture <span className="req">*</span></label><input className="ra-input" style={{ fontFamily: 'var(--font-num)' }} maxLength={20} placeholder="es / zh-Hans" value={d.culture || ''} onChange={e => setF('culture', e.target.value)} /></div>
                <div className="ra-field col-6"><label>UI culture <span className="opt">(defaults to culture)</span></label><input className="ra-input" style={{ fontFamily: 'var(--font-num)' }} maxLength={20} placeholder="es" value={d.ui || ''} onChange={e => setF('ui', e.target.value)} /></div>
                <div className="ra-field col-12">
                  <label className="ra-switch" style={{ marginTop: 4 }}>Enabled — selectable by users
                    <input type="checkbox" checked={!!d.enabled} onChange={e => setF('enabled', e.target.checked)} />
                    <span className="track" />
                  </label>
                </div>
              </div>
            </div>
            <div className="ra-modal__foot"><button className="af-btn af-btn--ghost" onClick={() => setEdit(null)}>Cancel</button><button className="af-btn af-btn--primary" onClick={save}><LGX name="check" size={15} />Save language</button></div>
          </div>
        </div>
      )}
    </>
  );
}

/* ---------------- Section 2: Language Texts ---------------- */
function TextsPage({ onToast, view }) {
  const [resource, setResource] = useStateLG('CaseEvaluation');
  const [culture, setCulture] = useStateLG('es');
  const [q, setQ] = useStateLG('');
  const [data, setData] = useStateLG(LT_SEED);
  const [draft, setDraft] = useStateLG({}); // key -> editing value
  const loading = view === 'loading';

  const rows = (data[resource] || []).filter(r => !q || r.key.toLowerCase().includes(q.toLowerCase()) || r.base.toLowerCase().includes(q.toLowerCase()));
  const cultureName = { es: 'Spanish', 'zh-Hans': 'Chinese (Simplified)', vi: 'Vietnamese' }[culture] || culture;

  function val(r) { return draft[r.key] != null ? draft[r.key] : r.es; }
  function saveRow(r) {
    setData(d => ({ ...d, [resource]: d[resource].map(x => x.key === r.key ? { ...x, es: val(r) } : x) }));
    setDraft(p => { const n = { ...p }; delete n[r.key]; return n; });
    onToast('Translation saved for ' + r.key + '.');
  }
  function resetRow(r) {
    setData(d => ({ ...d, [resource]: d[resource].map(x => x.key === r.key ? { ...x, es: '' } : x) }));
    setDraft(p => { const n = { ...p }; delete n[r.key]; return n; });
    onToast('Reset to default — falls back to base text.');
  }

  return (
    <>
      <div className="lt-sel">
        <div className="ra-field"><label>Resource</label><select className="ra-select" value={resource} onChange={e => setResource(e.target.value)}>{LT_RESOURCES.map(r => <option key={r}>{r}</option>)}</select></div>
        <div className="ra-field"><label>Target culture</label><select className="ra-select" value={culture} onChange={e => setCulture(e.target.value)}><option value="es">Spanish (es)</option><option value="zh-Hans">Chinese (zh-Hans)</option><option value="vi">Vietnamese (vi)</option></select></div>
        <div className="ia-search" style={{ minWidth: 220 }}><LGX name="search" size={16} /><input placeholder="Search key or text…" value={q} onChange={e => setQ(e.target.value)} /></div>
      </div>

      <div className="ia-wrap">
        <div className="ia-scroll">
          <table className="ia-table" style={{ minWidth: 760 }}>
            <thead><tr><th style={{ width: '24%' }}>Key</th><th style={{ width: '24%' }}>Base (English)</th><th>{cultureName} override</th><th style={{ width: 90 }}>State</th><th style={{ textAlign: 'right', width: 120 }}>Actions</th></tr></thead>
            <tbody>
              {loading && [0, 1, 2, 3, 4].map(i => (
                <tr key={i} style={{ cursor: 'default' }}>
                  <td><div className="sk-bar" style={{ width: 150 }} /></td><td><div className="sk-bar" style={{ width: 100 }} /></td>
                  <td><div className="sk-bar" style={{ width: '90%' }} /></td><td><div className="sk-bar" style={{ width: 60 }} /></td><td><div className="sk-bar" style={{ width: 90, marginLeft: 'auto' }} /></td>
                </tr>
              ))}
              {!loading && rows.map(r => {
                const cur = val(r);
                const dirty = draft[r.key] != null && draft[r.key] !== r.es;
                const customized = !!r.es;
                return (
                  <tr key={r.key} style={{ cursor: 'default' }}>
                    <td><span className="lt-key">{r.key}</span></td>
                    <td><span className="lt-base">{r.base}</span></td>
                    <td><input className={'lt-ovr-in' + (cur ? '' : ' fallback')} value={cur} placeholder={r.base + '  (falls back to base)'} onChange={e => setDraft(p => ({ ...p, [r.key]: e.target.value }))} /></td>
                    <td><span className={'lt-state ' + (customized ? 'custom' : 'def')}>{customized ? 'Customized' : 'Default'}</span></td>
                    <td style={{ textAlign: 'right' }}>
                      <span className="lt-rowbtns">
                        <button className="ra-rowbtn" title="Save" disabled={!dirty} style={!dirty ? { opacity: .4, cursor: 'not-allowed' } : null} onClick={() => saveRow(r)}><LGX name="check" size={14} /></button>
                        <button className="ra-rowbtn" title="Reset to default" disabled={!customized && !dirty} style={(!customized && !dirty) ? { opacity: .4, cursor: 'not-allowed' } : null} onClick={() => resetRow(r)}><LGX name="refresh" size={14} /></button>
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        {!loading && rows.length === 0 && (
          <div className="ia-empty"><LGX name="doc" size={30} /><b>No matching keys</b>Try a different search or resource.</div>
        )}
      </div>
      <div className="ra-hint" style={{ marginTop: 10 }}>An empty override falls back to the base (English) text. “Reset to default” clears the override.</div>
    </>
  );
}

window.InLang = InLang;
