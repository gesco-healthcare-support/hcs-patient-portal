import type { EmailStopScope } from './email-stop-scope.enum';
import type { AppointmentEmailRecipientsDto, CreateGlobalEmailStopInput, EmailStopConsentImpactDto, EmailStopStatusDto, GetGlobalEmailStopsInput, GlobalEmailStopDto, StopAppointmentEmailsInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class EmailStopsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateGlobalEmailStopInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GlobalEmailStopDto>({
      method: 'POST',
      url: '/api/app/email-stops',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/email-stops/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAppointmentRecipients = (appointmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentEmailRecipientsDto>({
      method: 'GET',
      url: `/api/app/appointments/${appointmentId}/email-recipients`,
    },
    { apiName: this.apiName,...config });
  

  getConsentImpact = (appointmentId: string, email: string, scope: EmailStopScope, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmailStopConsentImpactDto>({
      method: 'GET',
      url: `/api/app/appointments/${appointmentId}/email-stops/consent-impact`,
      params: { email, scope },
    },
    { apiName: this.apiName,...config });
  

  getGlobalConsentImpact = (email: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmailStopConsentImpactDto>({
      method: 'GET',
      url: '/api/app/email-stops/consent-impact',
      params: { email },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetGlobalEmailStopsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<GlobalEmailStopDto>>({
      method: 'GET',
      url: '/api/app/email-stops',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  resumeForAppointment = (appointmentId: string, email: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/appointments/${appointmentId}/email-stops`,
      params: { email },
    },
    { apiName: this.apiName,...config });
  

  stopForAppointment = (appointmentId: string, input: StopAppointmentEmailsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmailStopStatusDto>({
      method: 'POST',
      url: `/api/app/appointments/${appointmentId}/email-stops`,
      body: input,
    },
    { apiName: this.apiName,...config });
}