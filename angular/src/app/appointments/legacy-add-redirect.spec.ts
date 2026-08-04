import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { legacyAddRedirect } from './legacy-add-redirect';

/**
 * Phase 4a (2026-08-03). The redirect that retires the legacy add form.
 *
 * <p>Worth its own spec because the failure mode is silent and severe: `?type=2` is what tells the
 * wizard this is a RE-EVALUATION, so a redirect that drops query params would quietly turn every
 * re-evaluation deep link into a new booking. Extracted from the route array purely so this can be
 * asserted -- an inline arrow inside `APP_ROUTES` is unreachable from a test.</p>
 */
describe('legacyAddRedirect', () => {
  /** The `RedirectFunction` argument is a narrow Pick of ActivatedRouteSnapshot. */
  function redirectData(
    queryParams: Record<string, string>,
    fragment: string | null = null,
  ): Parameters<typeof legacyAddRedirect>[0] {
    return {
      queryParams,
      fragment,
      routeConfig: null,
      url: [],
      params: {},
      data: {},
      outlet: 'primary',
      title: undefined,
    } as unknown as Parameters<typeof legacyAddRedirect>[0];
  }

  function redirectTo(...args: Parameters<typeof redirectData>): string {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const tree = TestBed.runInInjectionContext(() => legacyAddRedirect(redirectData(...args)));
    return TestBed.inject(Router).serializeUrl(tree as never);
  }

  it('sends a plain visit to the wizard', () => {
    expect(redirectTo({})).toBe('/appointments/request');
  });

  it('CARRIES ?type across, so a re-evaluation link stays a re-evaluation', () => {
    expect(redirectTo({ type: '2' })).toBe('/appointments/request?type=2');
  });

  it('carries every query param, not just type', () => {
    expect(redirectTo({ type: '1', appointmentId: 'abc' })).toBe(
      '/appointments/request?type=1&appointmentId=abc',
    );
  });

  it('carries a fragment when present', () => {
    expect(redirectTo({}, 'schedule')).toBe('/appointments/request#schedule');
  });
});
