/**
 * OBS-29 (#566) -- the GDPR consent bar reserves its own strip.
 *
 * @volo/abp.ng.gdpr renders `<div id="cookieConsent" class="position-fixed
 * z-9999">` app-wide. A fixed element does not participate in flow, so nothing
 * below it can be scrolled into view: until Accept is clicked the bar
 * permanently covers the bottom of every page. Measured live on 2026-09-04 it
 * covers five controls on the booking wizard, including the wizard's own Back
 * and Continue buttons.
 *
 * The fix is one global rule in styles.scss. Global CSS is worth a test
 * precisely because its blast radius is every page, and because the two things
 * that could go wrong are opposite: the padding not applying when the bar is
 * present (the bug is not fixed), or applying when it is absent (dead space at
 * the bottom of every page for every user, forever).
 *
 * This reads computed style rather than asserting the rule text, so it fails if
 * the selector stops matching for any reason -- a renamed id upstream included.
 */
describe('GDPR consent bar strip reservation (#566)', () => {
  const BAR_ID = 'cookieConsent';

  function bottomPadding(): number {
    return parseFloat(getComputedStyle(document.body).paddingBottom) || 0;
  }

  afterEach(() => {
    document.getElementById(BAR_ID)?.remove();
  });

  it('reserves no space when the bar is absent', () => {
    expect(document.getElementById(BAR_ID)).toBeNull();
    expect(bottomPadding()).toBe(0);
  });

  it('reserves space while the bar is present', () => {
    const before = bottomPadding();

    const bar = document.createElement('div');
    bar.id = BAR_ID;
    document.body.appendChild(bar);

    const after = bottomPadding();
    expect(after).toBeGreaterThan(before);
    // 5rem at the default 16px root. Measured bar height was 45px, so the
    // reserve clears it with room for one line of wrap.
    expect(after).toBeGreaterThanOrEqual(45);
  });

  /**
   * The reserve must disappear once consent is recorded, or every page carries
   * dead space forever. This is the half a plain "does the padding apply" test
   * would miss.
   */
  it('releases the space again when the bar is dismissed', () => {
    const bar = document.createElement('div');
    bar.id = BAR_ID;
    document.body.appendChild(bar);
    expect(bottomPadding()).toBeGreaterThan(0);

    bar.remove();

    expect(bottomPadding()).toBe(0);
  });
});
