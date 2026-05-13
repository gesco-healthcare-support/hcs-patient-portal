import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * Issue 1.4 (2026-05-12) -- custom replacement for ABP's stock
 * `@volo/abp.ng.account/public` email-confirmation route. The stock
 * component verifies the token but does not surface a Resend
 * Verification button. We intercept the SPA route at
 * `/account/email-confirmation` (declared BEFORE `createRoutes()` in
 * `app.routes.ts`) and render this component instead.
 *
 * Flow:
 *  1. Reads `userId` + `confirmationToken` from the route query string
 *     (same contract ABP uses).
 *  2. POSTs to the stock ABP verify endpoint
 *     `/api/account/verify-email` (Account Pro module surface).
 *  3. Renders success / failure status.
 *  4. Always shows a "Resend Verification" button that POSTs to the
 *     same `/api/account/send-email-confirmation-token` ABP endpoint so
 *     the user can request a fresh link if needed.
 *
 * Reference: ABP route-override guidance at
 * https://docs.abp.io/en/abp/latest/UI/Angular/Component-Replacement
 * (route precedence is first-match-wins).
 */
@Component({
  selector: 'app-custom-email-confirmation',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="container py-5" style="max-width: 540px;">
      <h2 class="mb-3">Email Verification</h2>

      <div *ngIf="status() === 'pending'" class="alert alert-info" role="status">
        Verifying your email address…
      </div>

      <div *ngIf="status() === 'success'" class="alert alert-success" role="status">
        <strong>Email verified.</strong>
        <div>Your email address has been confirmed. You can now sign in.</div>
      </div>

      <div *ngIf="status() === 'error'" class="alert alert-danger" role="alert">
        <strong>We could not verify your email.</strong>
        <div>
          {{
            errorMessage() ||
              'The link may have expired. Use the button below to request a new verification email.'
          }}
        </div>
      </div>

      <div *ngIf="status() === 'no-token'" class="alert alert-warning" role="alert">
        <strong>Your email is not verified.</strong>
        <div>Use the button below to send a fresh verification email.</div>
      </div>

      <div *ngIf="resendStatus() === 'sent'" class="alert alert-info" role="status">
        Verification email sent. Check your inbox.
      </div>

      <div *ngIf="resendStatus() === 'error'" class="alert alert-danger" role="alert">
        Could not send verification email. Please try again in a moment.
      </div>

      <div class="d-grid gap-2 mt-3">
        <button
          type="button"
          class="btn btn-primary"
          [disabled]="!email || resending()"
          (click)="onResendClick()"
        >
          {{ resending() ? 'Sending…' : 'Resend Verification' }}
        </button>
        <a class="btn btn-outline-secondary" href="/Account/Login">Sign In</a>
      </div>
    </div>
  `,
})
export class CustomEmailConfirmationComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly http = inject(HttpClient);

  readonly status = signal<'pending' | 'success' | 'error' | 'no-token'>('pending');
  readonly errorMessage = signal<string | null>(null);
  readonly resending = signal<boolean>(false);
  readonly resendStatus = signal<'idle' | 'sent' | 'error'>('idle');

  email = '';
  userId = '';
  confirmationToken = '';

  async ngOnInit(): Promise<void> {
    const params = this.route.snapshot.queryParamMap;
    this.userId = params.get('userId') ?? '';
    this.confirmationToken = params.get('confirmationToken') ?? '';
    this.email = params.get('email') ?? '';

    if (!this.userId || !this.confirmationToken) {
      // No token means the user landed here from a "your email is not
      // verified" flow without a link. Skip verify and offer Resend.
      this.status.set('no-token');
      return;
    }

    try {
      await firstValueFrom(
        this.http.post(`${environment.apis.default.url}/api/account/verify-email`, {
          userId: this.userId,
          token: this.confirmationToken,
        }),
      );
      this.status.set('success');
    } catch (err) {
      this.status.set('error');
      if (err instanceof HttpErrorResponse) {
        const apiMsg = (err.error as { error?: { message?: string } } | undefined)?.error?.message;
        if (apiMsg) this.errorMessage.set(apiMsg);
      }
    }
  }

  async onResendClick(): Promise<void> {
    if (!this.email) return;
    this.resending.set(true);
    this.resendStatus.set('idle');
    try {
      await firstValueFrom(
        this.http.post(
          `${environment.apis.default.url}/api/account/send-email-confirmation-token`,
          {
            email: this.email,
            appName: 'Angular',
            returnUrl: window.location.origin,
          },
        ),
      );
      this.resendStatus.set('sent');
      // 5-second cooldown before allowing re-click.
      setTimeout(() => this.resending.set(false), 5000);
    } catch {
      this.resendStatus.set('error');
      this.resending.set(false);
    }
  }
}
