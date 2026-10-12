import type { SendPortalLinkInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LookupDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class ExternalSignupStaffService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getActiveInvitedEmails = (emails: string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, string[]>({
      method: 'GET',
      url: '/api/app/external-signup/active-invited-emails',
      params: { emails },
    },
    { apiName: this.apiName,...config });
  

  getTenantOptions = (filter?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/external-signup/tenant-options',
      params: { filter },
    },
    { apiName: this.apiName,...config });
  

  sendPortalLink = (input: SendPortalLinkInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/external-signup/send-portal-link',
      body: input,
    },
    { apiName: this.apiName,...config });
}