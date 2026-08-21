import { Injectable, OnDestroy, inject } from '@angular/core';
import { ConfigStateService } from '@abp/ng.core';
import { OAuthService } from 'angular-oauth2-oidc';
import { Subscription } from 'rxjs';

/**
 * Bug D fix (2026-05-11) + Issue #107 (2026-05-13) -- detect that the
 * identity backing the SPA's Bearer token has changed underneath the
 * cached state, and re-bootstrap.
 *
 * Originally this service drove `oauth.silentRefresh()` from focus,
 * visibility, and a 5 min interval to actively probe the AuthServer's
 * session cookie. That iframe path was ripped in #107 (broken on the
 * @abp/ng.oauth interceptor + carried noteworthy infra cost; refresh-
 * token rotation covers the access_token-renewal use case without an
 * iframe). The watcher now degrades to a passive listener: it
 * subscribes to <see cref="OAuthService.events"/> and compares the
 * `sub` claim on each token-related event. If a token is rotated and
 * the `sub` changes, it forces a full reload so ConfigStateService,
 * permissions, route guards, and the LeptonX layout all re-bootstrap
 * against the new identity.
 *
 * Trade-off relative to the iframe approach: this no longer catches
 * the "user B logged in on AuthServer in another tab while user A's
 * SPA was idle" case until something else triggers a token refresh
 * (the library's automatic refresh_token rotation, an explicit
 * sign-in flow on the SPA, etc.). Documented as accepted risk -- the
 * cookie-swap scenario in our threat model is rare and the practical
 * mitigation is a normal sign-out + sign-in cycle.
 */
@Injectable({ providedIn: 'root' })
export class SessionIdentityWatcherService implements OnDestroy {
  private readonly oauth = inject(OAuthService);
  private readonly configState = inject(ConfigStateService);
  private readonly subscription = new Subscription();
  private lastKnownSub: string | null = null;
  private started = false;

  start(): void {
    if (this.started || typeof window === 'undefined') {
      return;
    }
    this.started = true;
    this.lastKnownSub = this.readCurrentSub() ?? null;

    this.subscription.add(this.oauth.events.subscribe(() => this.checkForSubChange()));
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
  }

  private checkForSubChange(): void {
    const currentSub = this.readCurrentSub();
    if (!currentSub) {
      return;
    }
    if (this.lastKnownSub == null) {
      this.lastKnownSub = currentSub;
      return;
    }
    if (currentSub !== this.lastKnownSub) {
      this.lastKnownSub = currentSub;
      // Full reload -- safest path. ConfigStateService, permissions, route
      // guards, and the LeptonX layout all re-bootstrap cleanly against the
      // new token in localStorage.
      window.location.reload();
    }
  }

  private readCurrentSub(): string | undefined {
    const claims = this.oauth.getIdentityClaims() as Record<string, unknown> | null;
    const claimSub = claims?.['sub'];
    if (typeof claimSub === 'string' && claimSub.length > 0) {
      return claimSub;
    }
    // Fallback: ConfigStateService's currentUser.id matches `sub` once the
    // post-login ConfigState fetch has run. Useful pre-token-refresh window.
    const cachedId = this.configState.getDeep('currentUser.id');
    return typeof cachedId === 'string' ? cachedId : undefined;
  }
}
