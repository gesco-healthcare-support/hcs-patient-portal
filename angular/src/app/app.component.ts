import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { ConfigStateService, DynamicLayoutComponent } from '@abp/ng.core';
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
  private readonly subscription = new Subscription();

  ngOnInit(): void {
    this.updatePatientRoleClass();

    this.subscription.add(
      this.router.events
        .pipe(filter((event) => event instanceof NavigationEnd))
        .subscribe(() => this.updatePatientRoleClass()),
    );
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
