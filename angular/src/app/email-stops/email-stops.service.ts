import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import { Observable } from 'rxjs';
import type {
  AppointmentEmailRecipientsDto,
  CreateGlobalEmailStopInput,
  EmailStopConsentImpactDto,
  EmailStopScope,
  EmailStopStatusDto,
  GetGlobalEmailStopsInput,
  GlobalEmailStopDto,
  PagedResult,
  StopAppointmentEmailsInput,
} from './email-stops.models';

const API = { apiName: 'Default' } as const;

/**
 * Hand-written client for the email-stops endpoints. Method names match the backend app service,
 * so when the generated proxy `EmailStopService` is regenerated this class is replaced by an
 * import swap. Errors are not handled here: ABP's HTTP error handler shows the server message.
 */
@Injectable({ providedIn: 'root' })
export class EmailStopsService {
  private readonly rest = inject(RestService);

  getAppointmentRecipients(appointmentId: string): Observable<AppointmentEmailRecipientsDto> {
    return this.rest.request<null, AppointmentEmailRecipientsDto>(
      { method: 'GET', url: `/api/app/appointments/${appointmentId}/email-recipients` },
      API,
    );
  }

  stopForAppointment(
    appointmentId: string,
    input: StopAppointmentEmailsInput,
  ): Observable<EmailStopStatusDto> {
    return this.rest.request<StopAppointmentEmailsInput, EmailStopStatusDto>(
      { method: 'POST', url: `/api/app/appointments/${appointmentId}/email-stops`, body: input },
      API,
    );
  }

  resumeForAppointment(appointmentId: string, email: string): Observable<void> {
    return this.rest.request<null, void>(
      {
        method: 'DELETE',
        url: `/api/app/appointments/${appointmentId}/email-stops`,
        params: { email },
      },
      API,
    );
  }

  getConsentImpact(
    appointmentId: string,
    email: string,
    scope: EmailStopScope,
  ): Observable<EmailStopConsentImpactDto> {
    return this.rest.request<null, EmailStopConsentImpactDto>(
      {
        method: 'GET',
        url: `/api/app/appointments/${appointmentId}/email-stops/consent-impact`,
        params: { email, scope },
      },
      API,
    );
  }

  getList(input: GetGlobalEmailStopsInput): Observable<PagedResult<GlobalEmailStopDto>> {
    return this.rest.request<null, PagedResult<GlobalEmailStopDto>>(
      { method: 'GET', url: '/api/app/email-stops', params: { ...input } },
      API,
    );
  }

  create(input: CreateGlobalEmailStopInput): Observable<GlobalEmailStopDto> {
    return this.rest.request<CreateGlobalEmailStopInput, GlobalEmailStopDto>(
      { method: 'POST', url: '/api/app/email-stops', body: input },
      API,
    );
  }

  delete(id: string): Observable<void> {
    return this.rest.request<null, void>(
      { method: 'DELETE', url: `/api/app/email-stops/${id}` },
      API,
    );
  }

  getGlobalConsentImpact(email: string): Observable<EmailStopConsentImpactDto> {
    return this.rest.request<null, EmailStopConsentImpactDto>(
      { method: 'GET', url: '/api/app/email-stops/consent-impact', params: { email } },
      API,
    );
  }
}
