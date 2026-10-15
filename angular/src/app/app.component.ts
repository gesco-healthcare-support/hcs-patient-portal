import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { GdprCookieConsentComponent } from '@volo/abp.ng.gdpr/config';
import { LoaderBarComponent } from '@abp/ng.theme.shared';
import { AppointmentPendingCountService } from './appointments/services/appointment-pending-count.service';
import { SessionIdentityWatcherService } from './shared/auth/session-identity-watcher.service';
import { OfflineDetectionService } from './shared/services/offline-detection.service';
import { OfflineOverlayComponent } from './shared/ui/offline/offline-overlay.component';

/**
 * Root component. Renders a bare `<router-outlet>` -- the redesign drops the
 * LeptonX layout entirely (see feat/redesign-app-shell), so each page owns its
 * own chrome (external navbar / internal sidebar). The always-on globals live
 * here: loader bar, GDPR cookie consent, and the offline overlay.
 *
 * Note: ABP's page-alert container (`<abp-page-alert-container>`) used to live
 * inside the LeptonX layout. PageAlertService currently has zero callers; if it
 * gains any, mount the container here alongside `<abp-loader-bar>`.
 *
 * 2026-07-17 -- the `?logout=true` bootstrap handshake was removed. Logout now
 * uses the standard OIDC end-session flow (see `shared/auth/full-logout.ts`),
 * which lands the user on the AuthServer `/Account/Login` and never returns to
 * the SPA with a `?logout=true` marker, so there is nothing to handle here.
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
    this.appointmentPendingCount.start();
    this.sessionIdentityWatcher.start();
    this.offlineDetection.start();
  }
}
