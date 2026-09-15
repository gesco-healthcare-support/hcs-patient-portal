/* global React */
/* ============================================================
   Shared state screens — skeleton, empty, error, 404, session, offline.
   Message states reuse the public-page centered card (pp-*). Skeleton +
   empty render in-context with the external top navbar.
   ============================================================ */
function STI({ name, size }) { return <span className="i" dangerouslySetInnerHTML={window.Ico(name, size || 18)} />; }

function StNav() {
  return (
    <header className="ext-nav">
      <div className="ext-nav__in">
        <a className="ext-brand" href="#" onClick={e => e.preventDefault()} aria-label="Home">
          <img src="assets/header-logo.png" alt="Clinic" />
          <span className="ext-brand__div" />
          <span className="ext-brand__tag"><b>Appointment Portal</b><span>Patient &amp; case portal</span></span>
        </a>
        <div className="ext-nav__spacer" />
        <span className="sk sk--circle" style={{ width: 38, height: 38 }} />
      </div>
    </header>
  );
}

/* centered single-message state (reuses pp-* shell) */
function StMsg({ icon, tone, title, lead, actions }) {
  return (
    <div className="pp">
      <div className="pp-top">
        <img src="assets/header-logo.png" alt="Clinic logo" />
        <div className="tag"><b>Appointment Portal</b> · patient &amp; case portal</div>
      </div>
      <div className="pp-main">
        <div className="pp-card">
          <div className={'pp-ic ' + tone}><STI name={icon} size={30} /></div>
          <h1>{title}</h1>
          <p className="lead">{lead}</p>
          <div className={'pp-actions' + (actions.length === 1 ? ' pp-actions--single' : '')}>{actions}</div>
        </div>
      </div>
      <div className="pp-foot">Need help? Contact the clinic at <a href="#" onClick={e => e.preventDefault()}>support@clinic.example</a></div>
    </div>
  );
}

function AfterStates({ state, onToast }) {
  // ---- Skeleton (loading) ----
  if (state === 'skeleton') {
    return (
      <div className="st">
        <StNav />
        <div className="st-skel-hero">
          <div className="st-skel-hero__in">
            <span className="st-skel-onlight" style={{ display: 'block', width: 110, height: 12, marginBottom: 14 }} />
            <span className="st-skel-onlight" style={{ display: 'block', width: 260, height: 26, marginBottom: 10 }} />
            <span className="st-skel-onlight" style={{ display: 'block', width: 360, height: 13 }} />
          </div>
        </div>
        <div className="st-skel-wrap">
          <div className="st-skel-actions">
            {[0, 1].map(i => (
              <div className="st-skel-card" key={i}>
                <span className="sk" style={{ width: 46, height: 46, borderRadius: 13, flex: 'none' }} />
                <div style={{ flex: 1 }}><span className="sk" style={{ width: '55%', height: 14, marginBottom: 8 }} /><span className="sk" style={{ width: '80%', height: 11 }} /></div>
              </div>
            ))}
          </div>
          <span className="sk" style={{ width: 200, height: 20, marginBottom: 16 }} />
          {[0, 1, 2, 3].map(i => (
            <div className="st-skel-row" key={i}>
              <span className="sk when" />
              <div className="meta"><span className="sk" style={{ width: '40%', height: 14, marginBottom: 9 }} /><span className="sk" style={{ width: '70%', height: 11 }} /></div>
              <span className="sk" style={{ width: 90, height: 26, borderRadius: 99, flex: 'none' }} />
              <span className="sk" style={{ width: 104, height: 32, borderRadius: 9, flex: 'none' }} />
            </div>
          ))}
        </div>
      </div>
    );
  }

  // ---- Empty (in-context) ----
  if (state === 'empty') {
    return (
      <div className="st">
        <StNav />
        <div className="st-skel-hero" style={{ paddingBottom: 30 }}>
          <div className="st-skel-hero__in">
            <h1 style={{ color: '#fff', fontSize: 26, fontWeight: 800, margin: 0, letterSpacing: '-.02em' }}>Welcome back, Maria</h1>
            <p style={{ color: '#bcd6ed', fontSize: 14.5, margin: '5px 0 0' }}>Book a new evaluation or check the status of your existing requests.</p>
          </div>
        </div>
        <div className="st-skel-wrap">
          <div className="st-empty">
            <div className="st-empty__ic"><STI name="inbox" size={36} /></div>
            <h2>No appointment requests yet</h2>
            <p>When you request an evaluation, it’ll show up here so you can track its status and manage documents.</p>
            <button className="af-btn af-btn--primary af-btn--lg" onClick={() => onToast('Opening appointment request…')}><STI name="plus" size={16} />Request an appointment</button>
          </div>
        </div>
      </div>
    );
  }

  // ---- Message states ----
  if (state === 'error') return (
    <StMsg icon="alert" tone="red" title="Something went wrong"
      lead="We couldn’t load this page. This is usually temporary — please try again in a moment."
      actions={[<button key="r" className="af-btn af-btn--primary pp-btn-lg" onClick={() => onToast('Retrying…')}><STI name="refresh" size={16} />Try again</button>]} />
  );
  if (state === 'notfound') return (
    <StMsg icon="search" tone="blue" title="Page not found"
      lead="The page you’re looking for doesn’t exist or may have moved. Check the link, or head back to your home page."
      actions={[<button key="h" className="af-btn af-btn--primary pp-btn-lg" onClick={() => onToast('Going home…')}><STI name="home" size={16} />Back to home</button>]} />
  );
  if (state === 'session') return (
    <StMsg icon="clock" tone="amber" title="Your session has expired"
      lead="For your security, you’ve been signed out after a period of inactivity. Please sign in again to continue."
      actions={[<button key="s" className="af-btn af-btn--primary pp-btn-lg" onClick={() => onToast('Redirecting to secure sign-in…')}><STI name="logout" size={16} />Sign in again</button>]} />
  );
  if (state === 'offline') return (
    <StMsg icon="alert" tone="amber" title="You’re offline"
      lead="We can’t reach the clinic portal right now. Check your internet connection and try again."
      actions={[<button key="r" className="af-btn af-btn--primary pp-btn-lg" onClick={() => onToast('Reconnecting…')}><STI name="refresh" size={16} />Retry</button>]} />
  );
  return null;
}

window.AfterStates = AfterStates;
