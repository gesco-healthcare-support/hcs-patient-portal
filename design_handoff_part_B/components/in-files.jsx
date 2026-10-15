/* global React */
/* ============================================================
   Host / IT-Admin — File Management (blob storage explorer).
   Breadcrumb + folder tree + file list. New folder, Upload (drag-drop
   zone with per-file progress), search, per-row Download/Rename/Delete.
   Empty-folder + loading states. Renders inside InternalShell (itadmin).
   ============================================================ */
const { useState: useStateFM, useEffect: useEffectFM, useRef: useRefFM } = React;
function FMX({ name, size }) { return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size || 18)} />; }

/* tree (folders only): appointment-documents → {month} → {appointment} */
const FM_TREE = [
  { id: 'docs', name: 'appointment-documents', children: [
    { id: 'd2606', name: '2026-06', children: [
      { id: 'a24817', name: 'PQ-24817' }, { id: 'a24830', name: 'PQ-24830' },
    ] },
    { id: 'd2605', name: '2026-05', children: [
      { id: 'a24710', name: 'PQ-24710' },
    ] },
  ] },
  { id: 'reports', name: 'reports' },
  { id: 'templates', name: 'templates' },
  { id: 'temp', name: 'temp-uploads' },
];
/* files per folder id — documents live in the per-appointment folders */
const FM_FILES = {
  root: [], docs: [], d2606: [], d2605: [],
  a24817: [
    { id: 'f1', name: 'medical-records.pdf', size: '2.4 MB', type: 'pdf', mod: 'Jun 11, 2026 · 10:38' },
    { id: 'f2', name: 'cover-letter.docx', size: '48 KB', type: 'docx', mod: 'Jun 11, 2026 · 10:39' },
  ],
  a24830: [
    { id: 'f3', name: 'panel-strike-list.pdf', size: '180 KB', type: 'pdf', mod: 'Jun 10, 2026 · 16:02' },
  ],
  a24710: [
    { id: 'f4', name: 'imaging.pdf', size: '6.1 MB', type: 'pdf', mod: 'May 28, 2026 · 09:14' },
    { id: 'f5', name: 'deposition.pdf', size: '820 KB', type: 'pdf', mod: 'May 27, 2026 · 13:50' },
  ],
  reports: [
    { id: 'f6', name: 'monthly-summary-2026-05.pdf', size: '312 KB', type: 'pdf', mod: 'Jun 1, 2026 · 02:00' },
    { id: 'f7', name: 'utilization-export.csv', size: '24 KB', type: 'csv', mod: 'Jun 1, 2026 · 02:00' },
  ],
  templates: [
    { id: 'f8', name: 'approval-email.html', size: '12 KB', type: 'html', mod: 'May 19, 2026 · 11:22' },
    { id: 'f9', name: 'tenant-logo.png', size: '88 KB', type: 'png', mod: 'May 19, 2026 · 11:25' },
  ],
  temp: [],
};
const FM_TYPE_TINT = { pdf: 'tint-red', docx: 'tint-blue', csv: 'tint-green', html: 'tint-purple', png: 'tint-teal' };

function findPath(nodes, id, trail) {
  for (const n of nodes) {
    const t = [...trail, n];
    if (n.id === id) return t;
    if (n.children) { const r = findPath(n.children, id, t); if (r) return r; }
  }
  return null;
}

function InFiles({ onToast, view }) {
  const [sel, setSel] = useStateFM('a24817');
  const [openTree, setOpenTree] = useStateFM({ docs: true, d2606: true });
  const [files, setFiles] = useStateFM(FM_FILES);
  const [q, setQ] = useStateFM('');
  const [modal, setModal] = useStateFM(null); // 'upload' | 'newfolder' | {rename} | {delete}
  const [fname, setFname] = useStateFM('');
  const [ups, setUps] = useStateFM([]);
  const loading = view === 'loading';

  const path = findPath(FM_TREE, sel, [{ id: 'root', name: 'storage' }]) || [{ id: 'root', name: 'storage' }];
  const node = path[path.length - 1];
  const childFolders = (node.children || (sel === 'root' ? FM_TREE : []));
  const rawFiles = files[sel] || [];
  const shownFiles = q ? rawFiles.filter(f => f.name.toLowerCase().includes(q.toLowerCase())) : rawFiles;
  const isEmpty = !loading && childFolders.length === 0 && shownFiles.length === 0;

  function renderTree(nodes, depth) {
    return nodes.map(n => (
      <React.Fragment key={n.id}>
        <button className="fm-tnode" data-on={sel === n.id} style={{ paddingLeft: 10 + depth * 16 }}
          onClick={() => { setSel(n.id); if (n.children) setOpenTree(o => ({ ...o, [n.id]: true })); }}>
          <span className={'tw' + (openTree[n.id] ? ' open' : '')} onClick={e => { if (n.children) { e.stopPropagation(); setOpenTree(o => ({ ...o, [n.id]: !o[n.id] })); } }}>
            {n.children ? <FMX name="chevRight" size={14} /> : null}
          </span>
          <span className="fi"><FMX name={sel === n.id || openTree[n.id] ? 'folderOpen' : 'folder'} size={16} /></span>
          <span className="lbl">{n.name}</span>
        </button>
        {n.children && openTree[n.id] && renderTree(n.children, depth + 1)}
      </React.Fragment>
    ));
  }

  // simulated upload progress
  useEffectFM(() => {
    if (!ups.some(u => u.pct < 100)) return;
    const t = setInterval(() => {
      setUps(list => list.map(u => u.pct >= 100 ? u : { ...u, pct: Math.min(100, u.pct + Math.round(8 + Math.random() * 22)) }));
    }, 320);
    return () => clearInterval(t);
  }, [ups]);

  function startUpload() {
    const demo = [
      { id: 'u' + Date.now(), name: 'PQ-24905_medical-records.pdf', size: '3.1 MB', type: 'pdf', pct: 0 },
      { id: 'u' + (Date.now() + 1), name: 'PQ-24905_cover-letter.docx', size: '52 KB', type: 'docx', pct: 0 },
    ];
    setUps(demo);
  }
  function commitUploads() {
    const done = ups.filter(u => u.pct >= 100).map(u => ({ id: u.id, name: u.name, size: u.size, type: u.type, mod: 'Jun 16, 2026 · just now' }));
    setFiles(f => ({ ...f, [sel]: [...done, ...(f[sel] || [])] }));
    setUps([]); setModal(null); onToast(done.length + ' file' + (done.length === 1 ? '' : 's') + ' uploaded to ' + node.name + '.');
  }
  function createFolder() {
    if (!fname.trim()) { onToast('Folder name is required.'); return; }
    onToast('Folder “' + fname.trim() + '” created in ' + node.name + '.'); setModal(null); setFname('');
  }
  function doRename() {
    if (!fname.trim()) { onToast('Name is required.'); return; }
    setFiles(f => ({ ...f, [sel]: f[sel].map(x => x.id === modal.id ? { ...x, name: fname.trim() } : x) }));
    onToast('Renamed.'); setModal(null); setFname('');
  }
  function doDelete() {
    setFiles(f => ({ ...f, [sel]: f[sel].filter(x => x.id !== modal.id) }));
    onToast('“' + modal.name + '” deleted.'); setModal(null);
  }

  return (
    <>
      <div className="ia-head"><div><h1>File management</h1><p>Browse and manage files in tenant blob storage.</p></div></div>

      {/* breadcrumb */}
      <div className="fm-crumbs">
        {path.map((p, i) => (
          <React.Fragment key={p.id}>
            {i > 0 && <span className="sep"><FMX name="chevRight" size={13} /></span>}
            {i === path.length - 1 ? <span className="cur">{p.name}</span> : <button onClick={() => setSel(p.id)}>{p.name}</button>}
          </React.Fragment>
        ))}
      </div>

      <div className="fm">
        <aside className="fm-tree">
          <button className="fm-tnode" data-on={sel === 'root'} onClick={() => setSel('root')}>
            <span className="tw" /><span className="fi"><FMX name="grid" size={15} /></span><span className="lbl">storage</span>
          </button>
          {renderTree(FM_TREE, 0)}
        </aside>

        <div>
          <div className="fm-tools">
            <div className="ia-search"><FMX name="search" size={16} /><input placeholder="Search this folder…" value={q} onChange={e => setQ(e.target.value)} /></div>
            <button className="ia-fbtn" onClick={() => { setFname(''); setModal('newfolder'); }}><FMX name="folder" size={15} />New folder</button>
            <button className="af-btn af-btn--primary" onClick={() => { startUpload(); setModal('upload'); }}><FMX name="upload" size={15} />Upload</button>
          </div>

          <div className="ia-wrap">
            <div className="ia-scroll">
              <table className="ia-table" style={{ minWidth: 640 }}>
                <thead><tr><th>Name</th><th>Size</th><th>Type</th><th>Modified</th><th style={{ textAlign: 'right' }}>Actions</th></tr></thead>
                <tbody>
                  {loading && [0, 1, 2, 3].map(i => (
                    <tr key={i} style={{ cursor: 'default' }}>
                      <td><div className="fm-fname"><span className="fi file" style={{ background: 'var(--n-100)' }} /><div className="sk-bar" style={{ width: 200 }} /></div></td>
                      <td><div className="sk-bar" style={{ width: 50 }} /></td><td><div className="sk-bar" style={{ width: 44 }} /></td>
                      <td><div className="sk-bar" style={{ width: 130 }} /></td><td><div className="sk-bar" style={{ width: 80, marginLeft: 'auto' }} /></td>
                    </tr>
                  ))}
                  {!loading && childFolders.map(cf => (
                    <tr key={cf.id} onClick={() => { setSel(cf.id); setOpenTree(o => ({ ...o, [node.id]: true })); }}>
                      <td><span className="fm-fname"><span className="fi folder"><FMX name="folder" size={16} /></span>{cf.name}</span></td>
                      <td className="ia-sub">—</td><td><span className="fm-type">folder</span></td><td className="ia-sub">—</td>
                      <td style={{ textAlign: 'right' }}><span className="ia-sub" style={{ fontSize: 12 }}>Open <FMX name="chevRight" size={12} /></span></td>
                    </tr>
                  ))}
                  {!loading && shownFiles.map(f => (
                    <tr key={f.id} style={{ cursor: 'default' }}>
                      <td><span className="fm-fname"><span className={'fi ' + (FM_TYPE_TINT[f.type] || 'file')}><FMX name="file" size={15} /></span>{f.name}</span></td>
                      <td className="ia-sub" style={{ fontFamily: 'var(--font-num)' }}>{f.size}</td>
                      <td><span className="fm-type">{f.type}</span></td>
                      <td className="ia-sub" style={{ fontFamily: 'var(--font-num)' }}>{f.mod}</td>
                      <td style={{ textAlign: 'right' }}>
                        <span style={{ display: 'inline-flex', gap: 6 }}>
                          <button className="ra-rowbtn" title="Download" onClick={() => onToast('Downloading ' + f.name + '…')}><FMX name="download" size={14} /></button>
                          <button className="ra-rowbtn" title="Rename" onClick={() => { setFname(f.name); setModal({ kind: 'rename', id: f.id, name: f.name }); }}><FMX name="edit" size={14} /></button>
                          <button className="ra-rowbtn danger" title="Delete" onClick={() => setModal({ kind: 'delete', id: f.id, name: f.name })}><FMX name="trash" size={14} /></button>
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {isEmpty && (
              <div className="ia-empty">
                <FMX name="folderOpen" size={30} />
                <b>This folder is empty</b>
                {q ? 'No files match your search.' : 'Upload files or create a subfolder to get started.'}
                {!q && <div style={{ marginTop: 16 }}><button className="af-btn af-btn--primary" onClick={() => { startUpload(); setModal('upload'); }}><FMX name="upload" size={15} />Upload files</button></div>}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Upload modal */}
      {modal === 'upload' && (
        <div className="ra-scrim" onClick={() => { setUps([]); setModal(null); }}>
          <div className="ra-modal ra-modal--md" onClick={e => e.stopPropagation()}>
            <div className="ra-modal__head"><h3>Upload to {node.name}</h3><button className="ext-iconbtn x" onClick={() => { setUps([]); setModal(null); }}><FMX name="x" size={17} /></button></div>
            <div className="ra-modal__body">
              <div className="fm-drop" onClick={startUpload}>
                <span className="ic"><FMX name="upload" size={26} /></span>
                <b>Drag &amp; drop files here</b>
                <span>or click to browse · up to 50 MB each</span>
              </div>
              {ups.map(u => {
                const done = u.pct >= 100;
                return (
                  <div className="fm-up" key={u.id}>
                    <span className="fi"><FMX name="file" size={16} /></span>
                    <div className="nm">
                      <b>{u.name}</b>
                      <div className="track"><span className={done ? 'done' : ''} style={{ width: u.pct + '%' }} /></div>
                      <span className="pct">{done ? u.size + ' · done' : u.pct + '%'}</span>
                    </div>
                    <span className={'st ' + (done ? 'done' : 'up')}><FMX name={done ? 'check' : 'arrowUp'} size={16} /></span>
                  </div>
                );
              })}
            </div>
            <div className="ra-modal__foot">
              <button className="af-btn af-btn--ghost" onClick={() => { setUps([]); setModal(null); }}>Cancel</button>
              <button className="af-btn af-btn--primary" disabled={!ups.length || ups.some(u => u.pct < 100)} style={(!ups.length || ups.some(u => u.pct < 100)) ? { opacity: .5, cursor: 'not-allowed' } : null} onClick={commitUploads}><FMX name="check" size={15} />Done</button>
            </div>
          </div>
        </div>
      )}

      {/* New folder */}
      {modal === 'newfolder' && (
        <div className="ra-scrim" onClick={() => setModal(null)}>
          <div className="ra-modal ra-modal--md" onClick={e => e.stopPropagation()}>
            <div className="ra-modal__head"><h3>New folder in {node.name}</h3><button className="ext-iconbtn x" onClick={() => setModal(null)}><FMX name="x" size={17} /></button></div>
            <div className="ra-modal__body"><div className="ra-field col-12"><label>Folder name <span className="req">*</span></label><input className="ra-input" autoFocus value={fname} onChange={e => setFname(e.target.value)} placeholder="e.g. 2026-07" /></div></div>
            <div className="ra-modal__foot"><button className="af-btn af-btn--ghost" onClick={() => setModal(null)}>Cancel</button><button className="af-btn af-btn--primary" onClick={createFolder}><FMX name="check" size={15} />Create</button></div>
          </div>
        </div>
      )}

      {/* Rename */}
      {modal && modal.kind === 'rename' && (
        <div className="ra-scrim" onClick={() => setModal(null)}>
          <div className="ra-modal ra-modal--md" onClick={e => e.stopPropagation()}>
            <div className="ra-modal__head"><h3>Rename file</h3><button className="ext-iconbtn x" onClick={() => setModal(null)}><FMX name="x" size={17} /></button></div>
            <div className="ra-modal__body"><div className="ra-field col-12"><label>File name <span className="req">*</span></label><input className="ra-input" style={{ fontFamily: 'var(--font-num)' }} autoFocus value={fname} onChange={e => setFname(e.target.value)} /></div></div>
            <div className="ra-modal__foot"><button className="af-btn af-btn--ghost" onClick={() => setModal(null)}>Cancel</button><button className="af-btn af-btn--primary" onClick={doRename}><FMX name="check" size={15} />Save</button></div>
          </div>
        </div>
      )}

      {/* Delete */}
      {modal && modal.kind === 'delete' && (
        <div className="ra-scrim" onClick={() => setModal(null)}>
          <div className="ra-modal ra-modal--md" onClick={e => e.stopPropagation()}>
            <div className="ra-modal__head"><h3>Delete “{modal.name}”?</h3><button className="ext-iconbtn x" onClick={() => setModal(null)}><FMX name="x" size={17} /></button></div>
            <div className="ra-modal__body"><p style={{ margin: 0, fontSize: 14, color: 'var(--n-600)', lineHeight: 1.55 }}>This permanently removes the file from blob storage. This can’t be undone.</p></div>
            <div className="ra-modal__foot"><button className="af-btn af-btn--ghost" onClick={() => setModal(null)}>Cancel</button><button className="af-btn af-btn--primary" style={{ background: 'var(--st-rejected-fg)' }} onClick={doDelete}><FMX name="trash" size={15} />Delete</button></div>
          </div>
        </div>
      )}
    </>
  );
}

window.InFiles = InFiles;
