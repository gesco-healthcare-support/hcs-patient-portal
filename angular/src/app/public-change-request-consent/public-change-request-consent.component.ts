import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { RestService } from '@abp/ng.core';

/** Mirrors the backend ChangeRequestConsentInfoDto. */
interface ConsentInfo {
  confirmationNumber: string;
  changeRequestType: number; // 1 = Cancel, 2 = Reschedule
  reason?: string | null;
  requestedNewDateTime?: string | null;
  consentStatus: number; // 0 NotRequired, 1 Pending, 2 Approved, 3 Rejected, 4 Expired
}

type PageState = 'loading' | 'ready' | 'submitting' | 'done' | 'error';

/**
 * Group D (2026-06-09) public opposing-side consent page. Anonymous (no login):
 * reached by the single-use token link in the consent email. Reads the change-request
 * details from the [AllowAnonymous] endpoint (GET = read-only, safe for email-scanner
 * prefetch), shows the requested change + reason, and records Yes/No via POST. Owns its
 * own outcome UI (skipHandleError) -- there is no app shell on this route
 * (eLayoutType.empty). Mirrors the public-document-upload page.
 */
@Component({
  selector: 'app-public-change-request-consent',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [CommonModule],
  templateUrl: './public-change-request-consent.component.html',
  styles: [
    `
      .consent-page {
        min-height: 100vh;
        display: flex;
        align-items: center;
        justify-content: center;
        background: #f1f3f5;
        padding: 24px;
      }
      .card {
        background: #fff;
        border-radius: 8px;
        box-shadow: 0 1px 4px rgba(0, 0, 0, 0.12);
        padding: 28px;
        max-width: 520px;
        width: 100%;
      }
      h1 {
        font-size: 1.4rem;
        margin-bottom: 12px;
        color: #2c3e8c;
      }
      .hint {
        color: #6c757d;
        font-size: 0.9rem;
        margin: 12px 0;
      }
      .actions {
        display: flex;
        gap: 12px;
        margin-top: 20px;
      }
      button {
        border: 0;
        border-radius: 4px;
        padding: 10px 18px;
        cursor: pointer;
        color: #fff;
      }
      button:disabled {
        opacity: 0.6;
        cursor: default;
      }
      button.yes {
        background: #198754;
      }
      button.no {
        background: #842029;
      }
      .msg {
        padding: 10px 12px;
        border-radius: 4px;
        font-size: 0.9rem;
        margin: 12px 0;
      }
      .msg.success {
        background: #d1e7dd;
        color: #0f5132;
      }
      .msg.error {
        background: #f8d7da;
        color: #842029;
      }
    `,
  ],
})
export class PublicChangeRequestConsentComponent {
  private route = inject(ActivatedRoute);
  private rest = inject(RestService);
  private readonly token = this.route.snapshot.paramMap.get('token') ?? '';

  state: PageState = 'loading';
  info: ConsentInfo | null = null;
  errorMessage = '';

  constructor() {
    this.load();
  }

  get isReschedule(): boolean {
    return this.info?.changeRequestType === 2;
  }

  get actionWord(): string {
    return this.isReschedule ? 'reschedule' : 'cancellation';
  }

  // Verb form for "A request to {verb}" -- "cancel", not the noun "cancellation" (F-015).
  get actionVerb(): string {
    return this.isReschedule ? 'reschedule' : 'cancel';
  }

  /** A decision (or expiry default) already recorded -> not still Pending. */
  get alreadyDecided(): boolean {
    return !!this.info && this.info.consentStatus !== 1;
  }

  get decisionMessage(): string {
    switch (this.info?.consentStatus) {
      case 2:
        return 'You agreed to this request. Our clinic staff will finalize it.';
      case 3:
        return 'You declined this request. Our clinic staff has been notified.';
      case 4:
        return 'This link has expired, so the request was referred to our clinic staff.';
      default:
        return 'Thank you -- your response has been recorded.';
    }
  }

  private load(): void {
    if (!this.token) {
      this.state = 'error';
      this.errorMessage = 'This consent link is invalid.';
      return;
    }
    this.rest
      .request<
        unknown,
        ConsentInfo
      >({ method: 'GET', url: `/api/public/change-request-consent/${this.token}` }, { apiName: 'Default', skipHandleError: true })
      .subscribe({
        next: (info) => {
          this.info = info;
          this.state = this.alreadyDecided ? 'done' : 'ready';
        },
        error: (err: HttpErrorResponse) => this.fail(err),
      });
  }

  respond(approved: boolean): void {
    if (this.state === 'submitting' || !this.token) {
      return;
    }
    this.state = 'submitting';
    this.rest
      .request<{ approved: boolean }, ConsentInfo>(
        {
          method: 'POST',
          url: `/api/public/change-request-consent/${this.token}`,
          body: { approved },
        },
        { apiName: 'Default', skipHandleError: true },
      )
      .subscribe({
        next: (info) => {
          this.info = info;
          this.state = 'done';
        },
        error: (err: HttpErrorResponse) => this.fail(err),
      });
  }

  private fail(err: HttpErrorResponse): void {
    this.state = 'error';
    switch (err.status) {
      case 403:
      case 404:
        this.errorMessage = 'This consent link is invalid or has expired.';
        break;
      case 429:
        this.errorMessage = 'Too many attempts. Please wait a while and try again.';
        break;
      case 0:
        this.errorMessage = 'Network error. Check your connection and try again.';
        break;
      default:
        this.errorMessage = 'Sorry, something went wrong. Please try again.';
    }
  }
}
