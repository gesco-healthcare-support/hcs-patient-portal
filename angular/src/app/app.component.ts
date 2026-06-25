import { Component, Injector, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { GdprCookieConsentComponent } from '@volo/abp.ng.gdpr/config';
import { LoaderBarComponent } from '@abp/ng.theme.shared';
import { AppointmentPendingCountService } from './appointments/services/appointment-pending-count.service';
import { SessionIdentityWatcherService } from './shared/auth/session-identity-watcher.service';
import { performFullLogout } from './shared/auth/full-logout';
import { OfflineDetectionService } from './shared/services/offline-detection.service';
import { OfflineOverlayComponent } from './shared/ui/offline/offline-overlay.component';

const AUTH_SERVER_PORT = '44368';

/**
 * Build the AuthServer Razor /Account/Login URL on the same tenant
 * subdomain as the current SPA request. The host (e.g.
 * `falkinstein.localhost`) is preserved verbatim; only the port is
 * swapped to the AuthServer's 44368 and the path is `/Account/Login`.
 * Used after the AuthServer logout handshake clears the SPA tokens
 * (see `handleAuthServerLogoutHandshake` in `AppComponent`).
 */
function buildAuthServerLoginUrl(): string {
  const scheme = window.location.protocol;
  const host = window.location.hostname;
  return `${scheme}//${host}:${AUTH_SERVER_PORT}/Account/Login`;
}

/**
 * Root component. Renders a bare `<router-outlet>` -- the redesign drops the
 * LeptonX layout entirely (see feat/redesign-app-shell), so each page owns its
 * own chrome (external navbar / internal sidebar). The always-on globals live
 * here: loader bar, GDPR cookie consent, and the offline overlay.
 *
 * Note: ABP's page-alert container (`<abp-page-alert-container>`) used to live
 * inside the LeptonX layout. PageAlertService currently has zero callers; if it
 * gains any, mount the container here alongside `<abp-loader-bar>`.
 */
@Component({
  selector: 'app-root',
  template: `
    <abp-loader-bar />
    <router-outlet />
    <abp-gdpr-cookie-consent />
    @if (offline()) {
      <app-offline-overlay />
    }
  `,
  imports: [LoaderBarComponent, RouterOutlet, GdprCookieConsentComponent, OfflineOverlayComponent],
})
export class AppComponent implements OnInit {
  private readonly injector = inject(Injector);
  // Wave 4 / #6: kicks off the pending-appointments badge polling for
  // admin / staff users. Service is providedIn root and self-stops
  // when permission drops, so a single `start()` call here is enough.
  private readonly appointmentPendingCount = inject(AppointmentPendingCountService);
  // Bug D fix (2026-05-11): detects AuthServer cookie identity swap and
  // forces a full reload when sub changes. Same singleton-start pattern.
  private readonly sessionIdentityWatcher = inject(SessionIdentityWatcherService);
  // Redesign (2026-06-14): app-wide offline overlay (state-screens Task 5).
  // Started in ngOnInit; the template renders the overlay while offline() is true.
  private readonly offlineDetection = inject(OfflineDetectionService);
  protected readonly offline = this.offlineDetection.offline;

  ngOnInit(): void {
    this.handleAuthServerLogoutHandshake();
    this.appointmentPendingCount.start();
    this.sessionIdentityWatcher.start();
    this.offlineDetection.start();
  }

  /**
   * B5 (2026-05-07) -- when the AuthServer's /Account/Logout page is
   * hit (manually or via redirect), our custom LogoutModel signs out
   * its cookie and bounces back to the SPA root with `?logout=true`.
   * On that bootstrap we forcibly clear the SPA's OAuth state so any
   * cached access_token / refresh_token / id_token in localStorage is
   * invalidated. Without this, the SPA would keep using the stale
   * tokens until they expire even though the AuthServer cookie is
   * gone -- the original user-visible bug Adrian flagged.
   *
   * 2026-05-15 -- after performFullLogout finishes, navigate to the
   * AuthServer Razor Login page (not the SPA `/account/login`, which
   * was deleted along with the rest of the SPA auth routes). We build
   * the URL from the current SPA host so the tenant subdomain is
   * preserved (e.g. http://falkinstein.localhost:4200/?logout=true ->
   * http://falkinstein.localhost:44368/Account/Login).
   */
  private handleAuthServerLogoutHandshake(): void {
    if (typeof window === 'undefined') {
      return;
    }
    const params = new URLSearchParams(window.location.search);
    if (params.get('logout') !== 'true') {
      return;
    }
    void performFullLogout(this.injector).then(() => {
      window.location.replace(buildAuthServerLoginUrl());
    });
  }
}
