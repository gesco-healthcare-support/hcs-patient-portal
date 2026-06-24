/* global React */
/* ============================================================
   Request-an-Appointment — shared primitives: field controls,
   section card, wizard stepper. Exported on window.RAC.
   A "ctx" object { f, set, errs, touched } threads form state through.
   ============================================================ */
const { useState: useStateRC } = React;

function RcIcon({ name, size }) { return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size || 18)} />; }

function colClass(col) { return col ? 'ra-field col-' + col : 'ra-field col-3'; }

/* Field shell: label + required/opt + control + error/hint */
function RaField({ label, required, optional, col, error, hint, children }) {
  return (
    <div className={colClass(col)}>
      {label && (
        <label>
          {label}
          {required && <span className="req">*</span>}
          {optional && <span className="opt">(optional)</span>}
        </label>
      )}
      {children}
      {error && <div className="ra-err"><RcIcon name="alert" size={13} />{error}</div>}
      {!error && hint && <div className="ra-hint">{hint}</div>}
    </div>
  );
}

function RaText({ ctx, name, label, required, optional, col, type, placeholder, hint, readOnly, maxLength }) {
  const err = ctx.errs[name];
  return (
    <RaField label={label} required={required} optional={optional} col={col} error={err} hint={hint}>
      <input
        className={'ra-input' + (err ? ' bad' : '')}
        type={type || 'text'}
        value={ctx.f[name] || ''}
        placeholder={placeholder}
        readOnly={readOnly}
        maxLength={maxLength}
        onChange={e => ctx.set(name, e.target.value)}
        onBlur={() => ctx.touch && ctx.touch(name)}
        style={readOnly ? { background: 'var(--n-50)', color: 'var(--n-500)' } : null}
      />
    </RaField>
  );
}

function RaTextarea({ ctx, name, label, required, optional, col, placeholder, hint, rows }) {
  const err = ctx.errs[name];
  return (
    <RaField label={label} required={required} optional={optional} col={col || 12} error={err} hint={hint}>
      <textarea className={'ra-input' + (err ? ' bad' : '')} rows={rows || 3} value={ctx.f[name] || ''} placeholder={placeholder} onChange={e => ctx.set(name, e.target.value)} />
    </RaField>
  );
}

function RaSelect({ ctx, name, label, required, optional, col, options, placeholder, hint }) {
  const err = ctx.errs[name];
  const opts = (options || []).map(o => (typeof o === 'string' ? { value: o, label: o } : o));
  return (
    <RaField label={label} required={required} optional={optional} col={col} error={err} hint={hint}>
      <select className={'ra-select' + (err ? ' bad' : '')} value={ctx.f[name] || ''} onChange={e => ctx.set(name, e.target.value)}>
        <option value="">{placeholder || 'Select'}</option>
        {opts.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
    </RaField>
  );
}

/* address-style field with a pin icon (represents autocomplete) */
function RaAddress({ ctx, name, label, required, col, placeholder }) {
  const err = ctx.errs[name];
  return (
    <RaField label={label} required={required} col={col} error={err}>
      <div className="ra-auto-ic">
        <input className={'ra-input' + (err ? ' bad' : '')} value={ctx.f[name] || ''} placeholder={placeholder || 'Start typing an address…'} onChange={e => ctx.set(name, e.target.value)} />
        <span className="pin"><RcIcon name="map" size={15} /></span>
      </div>
    </RaField>
  );
}

/* date field — native date input styled to match */
function RaDate({ ctx, name, label, required, col, hint }) {
  const err = ctx.errs[name];
  return (
    <RaField label={label} required={required} col={col} error={err} hint={hint}>
      <div className="ra-affix">
        <input className={'ra-input' + (err ? ' bad' : '')} type="date" value={ctx.f[name] || ''} onChange={e => ctx.set(name, e.target.value)} />
        <button type="button" tabIndex={-1}><RcIcon name="calendar" size={16} /></button>
      </div>
    </RaField>
  );
}

/* radio group (gender / yes-no) laid out horizontally */
function RaRadios({ ctx, name, label, required, col, options }) {
  const err = ctx.errs[name];
  return (
    <RaField label={label} required={required} col={col} error={err}>
      <div className="ra-radios">
        {options.map(o => (
          <label key={String(o.value)} className="ra-radio">
            <input type="radio" name={name} checked={ctx.f[name] === o.value} onChange={() => ctx.set(name, o.value)} />
            {o.label}
          </label>
        ))}
      </div>
    </RaField>
  );
}

/* SSN field — mask on type, never pre-filled */
function RaSsn({ ctx, name, label, col }) {
  function fmt(v) {
    const d = (v || '').replace(/\D/g, '').slice(0, 9);
    if (d.length <= 3) return d;
    if (d.length <= 5) return d.slice(0, 3) + '-' + d.slice(3);
    return d.slice(0, 3) + '-' + d.slice(3, 5) + '-' + d.slice(5);
  }
  return (
    <RaField label={label} col={col} hint="Stored securely; never shown back in full.">
      <input className="ra-input" inputMode="numeric" placeholder="•••-••-••••" value={ctx.f[name] || ''} onChange={e => ctx.set(name, fmt(e.target.value))} onCopy={e => e.preventDefault()} />
    </RaField>
  );
}

/* Section card with icon + title + optional right-slot (toggle) */
function RaCard({ icon, tint, title, sub, right, children }) {
  return (
    <section className="ra-card">
      <div className="ra-card__head">
        {icon && <span className={'ic ' + (tint || 'tint-blue')}><RcIcon name={icon} size={19} /></span>}
        <div>
          <h3>{title}</h3>
          {sub && <p>{sub}</p>}
        </div>
        {right && <div className="right">{right}</div>}
      </div>
      <div className="ra-card__body">{children}</div>
    </section>
  );
}

/* Include toggle (optional attorney sections) */
function RaSwitch({ checked, onChange, label }) {
  return (
    <label className="ra-switch">
      {label}
      <input type="checkbox" checked={checked} onChange={e => onChange(e.target.checked)} />
      <span className="track" />
    </label>
  );
}

/* Wizard stepper */
function RaStepper({ steps, current, furthest, errorSteps, onJump }) {
  return (
    <nav className="ra-stepper">
      {steps.map((s, i) => {
        let state = 'upcoming';
        if (errorSteps && errorSteps.has(i)) state = 'error';
        else if (i === current) state = 'current';
        else if (i < current || i <= furthest) state = 'done';
        const reachable = i <= furthest || i <= current;
        if (i > furthest && i !== current) state = state === 'error' ? 'error' : (reachable ? state : 'disabled');
        return (
          <button key={s.key} className="ra-step" data-state={i === current ? 'current' : (errorSteps && errorSteps.has(i) ? 'error' : (i <= furthest ? 'done' : 'disabled'))}
            onClick={() => (i <= furthest || i === current) && onJump(i)}>
            <span className="ra-step__num">
              {i < current || (i <= furthest && i !== current && !(errorSteps && errorSteps.has(i))) ? <RcIcon name="check" size={15} /> : (i + 1)}
            </span>
            <span className="ra-step__tx"><b>{s.title}</b><span>{s.sub}</span></span>
          </button>
        );
      })}
    </nav>
  );
}

window.RAC = { RcIcon, RaField, RaText, RaTextarea, RaSelect, RaAddress, RaDate, RaRadios, RaSsn, RaCard, RaSwitch, RaStepper };
