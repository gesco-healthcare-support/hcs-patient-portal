/**
 * #609 -- the internal shell must stay usable below 860px.
 *
 * Media queries follow the VIEWPORT, so the markup is put in an iframe of a chosen width that
 * carries the page's real stylesheets. The markup uses the shell's own class names and the same
 * nesting as `internal-shell-layout.component.html`; the content block is deliberately wide
 * (a table-like row) to prove the main column, not the page, is what scrolls.
 */
describe('internal shell at narrow widths (#609)', () => {
  let frame: HTMLIFrameElement;

  async function open(width: number): Promise<Document> {
    frame = document.createElement('iframe');
    frame.style.cssText = `width:${width}px;height:700px;border:0;position:absolute;left:0;top:0`;
    document.body.appendChild(frame);
    const doc = frame.contentDocument!;
    doc.open();
    doc.write(`<!doctype html><html><head></head><body style="margin:0">
      <div class="in" id="shell">
        <aside class="in-side">
          <div class="in-side__brand"><span class="tt"><b>Office</b></span></div>
          <nav class="in-nav">
            <div class="in-sect in-sect--static"><span>Operations</span></div>
            <a class="in-link on"><span class="i">i</span><span class="lbl">Appointments</span></a>
          </nav>
        </aside>
        <div class="in-main">
          <header class="in-top">
            <button class="in-collapse" type="button" aria-label="Toggle sidebar">=</button>
            <div class="in-crumb">Home &gt; <b>Appointments requiring attention this week</b></div>
            <div class="spacer"></div>
            <div class="in-tenantwrap"><div class="in-tenant"><span class="mk">F</span>Falkinstein Medical Evaluators</div></div>
            <span style="width:40px;flex:none">bell</span>
            <a class="ap-btn ap-btn--primary">New appointment</a>
            <div class="in-acctwrap">
              <button class="in-acct" type="button">
                <span class="ava">AB</span>
                <span class="who"><b>Ada Example</b><span>IT Admin</span></span>
                <span class="cv">v</span>
              </button>
            </div>
          </header>
          <main style="padding:24px 16px">
            <div class="cf"><div class="cf-rail">
              <a class="cf-railitem">Users</a><a class="cf-railitem">Invitations</a>
              <a class="cf-railitem">Offices</a><a class="cf-railitem">Roles and permissions</a>
            </div><div>content</div></div>
          </main>
        </div>
      </div>
    </body></html>`);
    doc.close();
    // Same stylesheets the app runs with (Karma injects them as <style> or <link>).
    const loads: Promise<void>[] = [];
    document.querySelectorAll('style, link[rel="stylesheet"]').forEach((n) => {
      const copy = n.cloneNode(true) as HTMLElement;
      if (copy.tagName === 'LINK') {
        loads.push(
          new Promise((resolve, reject) => {
            copy.addEventListener('load', () => resolve());
            copy.addEventListener('error', () => reject(new Error('stylesheet failed to load')));
          }),
        );
      }
      doc.head.appendChild(copy);
    });
    await Promise.all(loads);
    expect(doc.querySelectorAll('style, link').length).toBeGreaterThan(0);
    return doc;
  }

  afterEach(() => frame?.remove());

  function widthOf(doc: Document, selector: string): number {
    return (doc.querySelector(selector) as HTMLElement).getBoundingClientRect().width;
  }

  it('does not scroll the page sideways at 820px', async () => {
    const doc = await open(820);
    expect(doc.documentElement.scrollWidth).toBeLessThanOrEqual(doc.documentElement.clientWidth);
  });

  it('collapses the sidebar to the 72px rail and hides its labels at 820px', async () => {
    const doc = await open(820);
    expect(widthOf(doc, '.in-side')).toBe(72);
    expect(getComputedStyle(doc.querySelector('.in-link .lbl')!).display).toBe('none');
  });

  it('keeps the account button inside the viewport and still named at 820px', async () => {
    const doc = await open(820);
    const right = (doc.querySelector('.in-acct') as HTMLElement).getBoundingClientRect().right;
    expect(right).toBeLessThanOrEqual(820);
    // Hidden visually, not removed: the accessible name keeps the user's name and role.
    expect(doc.querySelector('.in-acct')!.textContent).toContain('Ada Example');
    expect(widthOf(doc, '.in-acct .who')).toBeLessThanOrEqual(1);
  });

  it('leaves the full sidebar and the account name alone on a wide viewport', async () => {
    const doc = await open(1100);
    expect(widthOf(doc, '.in-side')).toBe(256);
    expect(widthOf(doc, '.in-acct .who')).toBeGreaterThan(20);
    expect(getComputedStyle(doc.querySelector('.in-link .lbl')!).display).not.toBe('none');
  });
});
