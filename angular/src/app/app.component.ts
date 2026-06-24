import { Component, Injector, OnDestroy, OnInit, inject } from '@angular/core';
import { ConfigStateService, DynamicLayoutComponent } from '@abp/ng.core';
import { Router, NavigationEnd } from '@angular/router';
import { GdprCookieConsentComponent } from '@volo/abp.ng.gdpr/config';
import { LoaderBarComponent } from '@abp/ng.theme.shared';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { hasAnyExternalRole } from './shared/auth/external-user-roles';
import { AppointmentPendingCountService } from './appointments/services/appointment-pending-count.service';
import { SessionIdentityWatcherService } from './shared/auth/session-identity-watcher.service';
import { performFullLogout } from './shared/auth/full-logout';

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

@Component({
  selector: 'app-root',
  template: `
    <abp-loader-bar />
    <abp-dynamic-layout />
    <abp-gdpr-cookie-consent />
  `,
  imports: [LoaderBarComponent, DynamicLayoutComponent, GdprCookieConsentComponent],
})
export class AppComponent implements OnInit, OnDestroy {
  private readonly configState = inject(ConfigStateService);
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);
  // Wave 4 / #6: kicks off the pending-appointments badge polling for
  // admin / staff users. Service is providedIn root and self-stops
  // when permission drops, so a single `start()` call here is enough.
  private readonly appointmentPendingCount = inject(AppointmentPendingCountService);
  // Bug D fix (2026-05-11): detects AuthServer cookie identity swap and
  // forces a full reload when sub changes. Same singleton-start pattern.
  private readonly sessionIdentityWatcher = inject(SessionIdentityWatcherService);
  private readonly subscription = new Subscription();

  ngOnInit(): void {
    this.handleAuthServerLogoutHandshake();
    this.updatePatientRoleClass();
    this.appointmentPendingCount.start();
    this.sessionIdentityWatcher.start();

    this.subscription.add(
      this.router.events
        .pipe(filter((event) => event instanceof NavigationEnd))
        .subscribe(() => this.updatePatientRoleClass()),
    );
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

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
    document.body.classList.remove('externaluser-role');
    document.documentElement.classList.remove('externaluser-role');
    this.applySidebarVisibility(false);
  }

  private updatePatientRoleClass(): void {
    // Phase 9 L7 (2026-05-04) -- shared canonical role list lives in
    // shared/auth/external-user-roles.ts so the post-login redirect
    // guard and this CSS-toggle stay aligned.
    const currentUser = this.configState.getOne('currentUser') as { roles?: string[] } | null;
    const isExternalUser = hasAnyExternalRole(currentUser?.roles ?? []);

    document.body.classList.toggle('externaluser-role', isExternalUser);
    document.documentElement.classList.toggle('externaluser-role', isExternalUser);
    this.applySidebarVisibility(isExternalUser);
  }

  private applySidebarVisibility(isExternalUser: boolean): void {
    const sidebarSelectors = [
      '.lpx-sidebar-container',
      '.lpx-sidebar',
      '.lpx-menu-container',
      '.lpx-menu',
      'aside',
    ];
    const mainSelectors = [
      '.lpx-content-container',
      '.lpx-main-container',
      '.lpx-main-content',
      '.lpx-page',
      'main',
    ];

    for (const selector of sidebarSelectors) {
      document.querySelectorAll<HTMLElement>(selector).forEach((el) => {
        el.classList.toggle('externaluser-sidebar-hidden', isExternalUser);
      });
    }

    for (const selector of mainSelectors) {
      document.querySelectorAll<HTMLElement>(selector).forEach((el) => {
        el.classList.toggle('externaluser-main-full', isExternalUser);
      });
    }
  }
}
