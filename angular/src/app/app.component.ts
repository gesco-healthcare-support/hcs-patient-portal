import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { AuthService, ConfigStateService, DynamicLayoutComponent } from '@abp/ng.core';
import { Router, NavigationEnd } from '@angular/router';
import { GdprCookieConsentComponent } from '@volo/abp.ng.gdpr/config';
import { LoaderBarComponent } from '@abp/ng.theme.shared';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { hasAnyExternalRole } from './shared/auth/external-user-roles';

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
  private readonly authService = inject(AuthService);
  private readonly subscription = new Subscription();

  ngOnInit(): void {
    this.handleAuthServerLogoutHandshake();
    this.updatePatientRoleClass();

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
    // Local-only logout: clear tokens but do NOT redirect to AuthServer
    // again (we just came from there). AuthService.logout() with no
    // ConfigFlags option calls OAuthService.logOut() which clears
    // localStorage state. We swallow the observable to avoid leaving
    // a hanging subscription; cleanup is synchronous on the SPA side.
    this.authService.logout().subscribe({
      complete: () => {
        const cleanUrl = window.location.pathname;
        window.history.replaceState({}, '', cleanUrl);
        this.router.navigateByUrl('/login');
      },
      error: () => {
        // If logout fails (e.g. already logged out), still strip the
        // query param + navigate so we don't loop.
        const cleanUrl = window.location.pathname;
        window.history.replaceState({}, '', cleanUrl);
        this.router.navigateByUrl('/login');
      },
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
