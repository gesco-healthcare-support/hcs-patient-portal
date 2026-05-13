import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Issue #105 (2026-05-13) -- 404 page rendered when an anonymous visitor
 * hits a SPA route that has been intentionally removed (today: only
 * `/account/register`). Authed users are intercepted upstream by the
 * route's CanMatchFn and never load this component.
 *
 * The OLD SPA's /account/* tree is dead code -- the live registration,
 * login, password-reset, and email-confirmation flows all live on the
 * AuthServer Razor pages at port 44368. Per Adrian's directive
 * (2026-05-13), users navigating to /account/register on the SPA
 * should see a 404, not a silent redirect, so legitimate flows go
 * through the email-invite URL and malicious pokers get nothing.
 */
@Component({
  selector: 'app-route-not-found',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <main
      class="d-flex flex-column align-items-center justify-content-center text-center"
      style="min-height: 60vh; padding: 2rem;"
    >
      <h1 class="display-1 fw-bold mb-3">404</h1>
      <h2 class="h4 mb-3">Page not found</h2>
      <p class="text-muted mb-4" style="max-width: 32rem;">
        The page you're looking for doesn't exist on the patient portal. If you're trying to
        register, use the invitation link emailed to you by your practice.
      </p>
      <a routerLink="/" class="btn btn-primary">Go to home</a>
    </main>
  `,
})
export class RouteNotFoundComponent {}
