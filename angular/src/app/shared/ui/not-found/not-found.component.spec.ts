import { NotFoundComponent } from './not-found.component';

/**
 * The catch-all 404 page. Its one action goes to `/`, which the post-login guard sends to the
 * right home for the viewer's role.
 *
 * <p>It had no spec. It injects nothing, so it is built with `new`.</p>
 */
describe('NotFoundComponent', () => {
  it('offers a single way back to the home page', () => {
    const c = new NotFoundComponent() as unknown as {
      actions: { label: string; icon: string; routerLink: string }[];
    };
    expect(c.actions).toEqual([{ label: 'Back to home', icon: 'home', routerLink: '/' }]);
  });
});
