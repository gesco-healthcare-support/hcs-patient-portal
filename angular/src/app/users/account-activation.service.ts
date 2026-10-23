import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import { Observable } from 'rxjs';

/**
 * Staff "Resend activation email" actions (`/api/app/account-activation`). Hand-written so the
 * generated `proxy/` stays untouched until the next `abp generate-proxy`; the routes are pinned by
 * the hand-written controller on the backend.
 *
 * Errors come back as ABP business errors with a readable message (already active, switched off,
 * sent less than a minute ago), which the global error handler shows.
 */
@Injectable({ providedIn: 'root' })
export class AccountActivationService {
  private readonly rest = inject(RestService);
  readonly apiName = 'Default';

  /** Resends the activation email to an account on the caller's identity Users screen. */
  resendToUser(userId: string): Observable<void> {
    return this.rest.request<void, void>(
      { method: 'POST', url: `/api/app/account-activation/users/${userId}/resend` },
      { apiName: this.apiName },
    );
  }

  /** Host only: resends the activation email to a practice's office administrator. */
  resendToOfficeAdministrator(officeId: string): Observable<void> {
    return this.rest.request<void, void>(
      {
        method: 'POST',
        url: `/api/app/account-activation/offices/${officeId}/resend-administrator`,
      },
      { apiName: this.apiName },
    );
  }
}
