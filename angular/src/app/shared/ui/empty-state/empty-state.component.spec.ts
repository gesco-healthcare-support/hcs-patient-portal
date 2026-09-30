import { EmptyStateComponent } from './empty-state.component';

/**
 * The shared empty-state block. It has no logic; what it promises is its defaults -- no title or
 * body text, and no call-to-action unless one is given.
 *
 * <p>It had no spec. It injects nothing, so it is built with `new`.</p>
 */
describe('EmptyStateComponent', () => {
  it('has no text and no call-to-action until given them', () => {
    const c = new EmptyStateComponent();
    expect(c.title).toBe('');
    expect(c.body).toBe('');
    expect(c.ctaLabel).toBeUndefined();
    expect(c.ctaIcon).toBeUndefined();
  });
});
