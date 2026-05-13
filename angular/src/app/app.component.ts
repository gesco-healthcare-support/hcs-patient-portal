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
   * AuthService.logout() drives the underlying angular-oauth2-oidc
   * client to clear the state; we then strip the query param so a
   * page reload doesn't repeat the cleanup.
   */
  private handleAuthServerLogoutHandshake(): void {
    if (typeof window === 'undefined') {
      return;
    }
    const params = new URLSearchParams(window.location.search);
    if (params.get('logout') !== 'true') {
      return;
    }
    // Issue #106a (2026-05-13) -- delegate to performFullLogout so the
    // SPA matches the AuthServer's cookie cleanup. Prior implementation
    // called AuthService.logout() directly which clears only OAuth keys;
    // leftover __tenant + XSRF-TOKEN cookies could leak the previous
    // user's tenant into the next registration. Also fixes the stray
    // navigateByUrl('/login') -- the SPA route is '/account/login'.
    void performFullLogout(this.injector).then(() => {
      const cleanUrl = window.location.pathname;
      window.history.replaceState({}, '', cleanUrl);
      this.router.navigateByUrl('/account/login');
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
