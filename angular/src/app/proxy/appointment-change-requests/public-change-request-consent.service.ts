import type { ChangeRequestConsentInfoDto, SubmitChangeRequestConsentDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PublicChangeRequestConsentService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  get = (token: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ChangeRequestConsentInfoDto>({
      method: 'GET',
      url: `/api/public/change-request-consent/${token}`,
    },
    { apiName: this.apiName,...config });
  

  submit = (token: string, input: SubmitChangeRequestConsentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ChangeRequestConsentInfoDto>({
      method: 'POST',
      url: `/api/public/change-request-consent/${token}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}