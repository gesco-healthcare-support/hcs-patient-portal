/**
 * #609 -- the configuration / admin / people / users rail (`.cf` + `.cf-rail` + `.cf-railitem`)
 * must become a self-scrolling horizontal strip below 860px, not a wide column that pushes the
 * page sideways. Sibling of `internal-shell-narrow.spec.ts`: media queries follow the VIEWPORT,
 * so the markup goes in an iframe of a chosen width carrying the app's real stylesheets, and the
 * assertions read computed style and measured geometry, not stylesheet text.
 */
describe('config rail at narrow widths (#609)', () => {
  let frame: HTMLIFrameElement;

  const LABELS = [
    'Users',
    'Invitations',
    'Offices and locations',
    'Roles and permissions',
    'Appointment types',
    'Notification templates',
    'Custom fields',
    'System parameters',
  ];

  async function open(width: number): Promise<Document> {
    frame = document.createElement('iframe');
    frame.style.cssText = `width:${width}px;height:700px;border:0;position:absolute;left:0;top:0`;
    document.body.appendChild(frame);
    const doc = frame.contentDocument!;
    const items = LABELS.map((l) => `<a class="cf-railitem">${l}</a>`).join('');
    doc.open();
    doc.write(`<!doctype html><html><head></head><body style="margin:0">
      <main style="padding:24px 16px">
        <div class="cf"><div class="cf-rail">${items}</div><div>content</div></div>
      </main>
    </body></html>`);
    doc.close();
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

  const rail = (doc: Document) => doc.querySelector('.cf-rail') as HTMLElement;
  const items = (doc: Document) =>
    Array.from(doc.querySelectorAll('.cf-railitem')) as HTMLElement[];

  it('lays the rail out as a horizontal strip that scrolls inside itself at 820px', async () => {
    const doc = await open(820);
    const r = rail(doc);
    expect(getComputedStyle(r).display).toBe('flex');
    expect(getComputedStyle(r).flexDirection).toBe('row');
    expect(getComputedStyle(r).position).toBe('static');
    // The strip is narrower than its content (so it genuinely scrolls) and fits the viewport.
    expect(r.getBoundingClientRect().right).toBeLessThanOrEqual(820);
    expect(r.scrollWidth).toBeGreaterThan(r.clientWidth);
    const first = items(doc)[0].getBoundingClientRect();
    const second = items(doc)[1].getBoundingClientRect();
    expect(Math.abs(first.top - second.top)).toBeLessThanOrEqual(1);
  });

  it('does not scroll the page sideways at 820px', async () => {
    const doc = await open(820);
    expect(doc.documentElement.scrollWidth).toBeLessThanOrEqual(
      doc.documentElement.clientWidth + 1,
    );
  });

  it('keeps every rail label on one line at 820px', async () => {
    const doc = await open(820);
    for (const el of items(doc)) {
      expect(getComputedStyle(el).whiteSpace).toBe('nowrap');
      const lineHeight = parseFloat(getComputedStyle(el).lineHeight) || 20;
      // One line plus the 10px vertical padding each side; a wrapped label would be taller.
      expect(el.getBoundingClientRect().height).toBeLessThan(lineHeight + 20 + 8);
    }
  });

  it('keeps every item reachable by scrolling the rail at 820px', async () => {
    const doc = await open(820);
    const r = rail(doc);
    const last = items(doc)[LABELS.length - 1];
    r.scrollLeft = r.scrollWidth;
    const lastBox = last.getBoundingClientRect();
    const railBox = r.getBoundingClientRect();
    expect(lastBox.right).toBeLessThanOrEqual(railBox.right + 1);
    expect(lastBox.left).toBeGreaterThanOrEqual(railBox.left);
    // Scrolling the rail did not move the page.
    expect(doc.documentElement.scrollLeft).toBe(0);
  });

  it('keeps the two-column rail on a wide viewport', async () => {
    const doc = await open(1100);
    expect(getComputedStyle(rail(doc)).display).not.toBe('flex');
    expect(rail(doc).getBoundingClientRect().width).toBeLessThan(260);
  });
});
