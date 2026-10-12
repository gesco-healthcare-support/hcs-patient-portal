import type { PartyConsentRequestDto, SubmitChangeRequestConsentDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PartyChangeRequestConsentService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getMyPending = (appointmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<PartyConsentRequestDto>>({
      method: 'GET',
      url: '/api/app/change-request-consents/mine',
      params: { appointmentId },
    },
    { apiName: this.apiName,...config });
  

  submitMyDecision = (changeRequestId: string, input: SubmitChangeRequestConsentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PartyConsentRequestDto>({
      method: 'POST',
      url: `/api/app/change-request-consents/${changeRequestId}/decision`,
      body: input,
    },
    { apiName: this.apiName,...config });
}