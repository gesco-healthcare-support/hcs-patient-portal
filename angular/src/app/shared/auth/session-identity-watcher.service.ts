import { Injectable, OnDestroy, inject } from '@angular/core';
import { ConfigStateService } from '@abp/ng.core';
import { OAuthService } from 'angular-oauth2-oidc';
import { Subscription } from 'rxjs';

/**
 * Bug D fix (2026-05-11) -- detect "AuthServer cookie was replaced under the
 * SPA's feet" and re-bootstrap.
 *
 * Threat model: user A is logged in to the SPA. Without clicking SPA logout,
 * the user opens AuthServer's Razor /Account/Login (port 44368), submits user
 * B's credentials. AuthServer's session cookie now identifies B; the SPA's
 * Bearer token + cached `currentUser` still belong to A. Bug = SPA continues
 * to render A's PHI and any user action gets attributed to A in audit logs.
 *
 * Detection: the only mechanism that actually reads AuthServer's cookie is a
 * `/connect/authorize?prompt=none` request. angular-oauth2-oidc implements
 * this as a hidden iframe (`silentRefresh()`). When it returns a token, the
 * library fires events; we compare the new `sub` claim to the cached one. On
 * mismatch we force a full page reload so every ABP service (ConfigStateService,
 * permissions, route guards, layout) re-bootstraps cleanly with the new
 * identity. Soft re-bootstrap was considered + rejected: too many cached
 * states in ABP services + LeptonX layout to refresh reliably.
 *
 * Triggers:
 *  - Periodic 5min interval -- catches background swaps when the user never
 *    refocuses the SPA tab.
 *  - `window.focus` + `document.visibilitychange` (debounced 2s) -- catches
 *    the typical swap pattern (user switches to another tab to do the
 *    AuthServer login, returns to SPA). Detection latency ~1-3s.
 *
 * What is NOT touched: OAuth state / PKCE_verifier / nonce. The library
 * manages these across silentRefresh internally. CSRF protections stay intact.
 *
 * Why not `/connect/userinfo`: per OIDC Core 5.3 userinfo authenticates via
 * Bearer token, not cookie, so it returns the token-holder's claims regardless
 * of cookie state -- useless for detecting cookie swap.
 */
@Injectable({ providedIn: 'root' })
export class SessionIdentityWatcherService implements OnDestroy {
  private static readonly PERIODIC_MS = 5 * 60 * 1000;
  private static readonly FOCUS_DEBOUNCE_MS = 2000;

  private readonly oauth = inject(OAuthService);
  private readonly configState = inject(ConfigStateService);
  private readonly subscription = new Subscription();
  private periodicHandle: ReturnType<typeof setInterval> | null = null;
  private lastFocusRefresh = 0;
  private lastKnownSub: string | null = null;
  private started = false;
  private readonly focusListener = () => this.triggerRefreshDebounced();
  private readonly visibilityListener = () => {
    if (typeof document !== 'undefined' && document.visibilityState === 'visible') {
      this.triggerRefreshDebounced();
    }
  };

  start(): void {
    if (this.started || typeof window === 'undefined') {
      return;
    }
    this.started = true;
    this.lastKnownSub = this.readCurrentSub() ?? null;

    this.subscription.add(this.oauth.events.subscribe(() => this.checkForSubChange()));

    window.addEventListener('focus', this.focusListener);
    document.addEventListener('visibilitychange', this.visibilityListener);

    this.periodicHandle = setInterval(
      () => this.tryRefresh(),
      SessionIdentityWatcherService.PERIODIC_MS,
    );
  }

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
    if (this.periodicHandle != null) {
      clearInterval(this.periodicHandle);
      this.periodicHandle = null;
    }
    if (typeof window !== 'undefined') {
      window.removeEventListener('focus', this.focusListener);
      document.removeEventListener('visibilitychange', this.visibilityListener);
    }
  }

  private triggerRefreshDebounced(): void {
    const now = Date.now();
    if (now - this.lastFocusRefresh < SessionIdentityWatcherService.FOCUS_DEBOUNCE_MS) {
      return;
    }
    this.lastFocusRefresh = now;
    this.tryRefresh();
  }

  private tryRefresh(): void {
    if (!this.oauth.hasValidAccessToken()) {
      return;
    }
    this.oauth.silentRefresh().catch(() => {
      // Common silent-refresh failure modes:
      //  - AuthServer cookie expired (user not logged in over there) -> 401
      //  - Network blip / iframe timeout
      // Either is non-actionable here; the next focus or interval retries.
    });
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
