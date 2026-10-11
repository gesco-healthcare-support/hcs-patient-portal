import { inject, Injectable } from '@angular/core';
import { ListResultDto, RestService } from '@abp/ng.core';
import { Observable } from 'rxjs';
import { ChangeRequestConsentStatus } from '../../proxy/appointment-change-requests/change-request-consent-status.enum';
import { ChangeRequestType } from '../../proxy/appointment-change-requests/change-request-type.enum';

/** One change request waiting for the signed-in party's answer (mirrors PartyConsentRequestDto). */
export interface PartyConsentRequest {
  changeRequestId: string;
  appointmentId: string;
  confirmationNumber: string;
  patientName: string;
  appointmentDate: string;
  changeRequestType: ChangeRequestType;
  reason?: string | null;
  requestedNewDateTime?: string | null;
  consentStatus: ChangeRequestConsentStatus;
}

/**
 * 2026-10-10 -- the in-portal Agree / Decline for a reschedule date or a cancellation, the
 * alternative to the emailed consent link. Hand-written over RestService (the same pattern as
 * shared/branding/branding.service.ts) because the endpoint is new and the generated proxy is
 * only regenerated against a running API. Who may answer is decided entirely on the server.
 */
@Injectable({ providedIn: 'root' })
export class PartyConsentService {
  private readonly rest = inject(RestService);

  /** Requests waiting for my answer -- on one appointment, or on any I can see. */
  getMine(appointmentId?: string | null): Observable<ListResultDto<PartyConsentRequest>> {
    return this.rest.request<void, ListResultDto<PartyConsentRequest>>(
      {
        method: 'GET',
        url: '/api/app/change-request-consents/mine',
        params: appointmentId ? { appointmentId } : {},
      },
      { apiName: 'Default' },
    );
  }

  decide(changeRequestId: string, approved: boolean): Observable<PartyConsentRequest> {
    return this.rest.request<{ approved: boolean }, PartyConsentRequest>(
      {
        method: 'POST',
        url: `/api/app/change-request-consents/${changeRequestId}/decision`,
        body: { approved },
      },
      { apiName: 'Default' },
    );
  }
}
