import type { ExternalUserLookupDto, ExternalUserSignUpDto, InvitationValidationDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';
import type { LookupDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class ExternalSignupService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getExternalUserLookup = (filter?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<ExternalUserLookupDto>>({
      method: 'GET',
      url: '/api/public/external-signup/external-user-lookup',
      params: { filter },
    },
    { apiName: this.apiName,...config });
  

  getTenantOptions = (filter?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/public/external-signup/tenant-options',
      params: { filter },
    },
    { apiName: this.apiName,...config });
  

  register = (input: ExternalUserSignUpDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/public/external-signup/register',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  resolveTenantByName = (name: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/public/external-signup/resolve-tenant',
      params: { name },
    },
    { apiName: this.apiName,...config });
  

  validateInvite = (token: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvitationValidationDto>({
      method: 'GET',
      url: '/api/public/external-signup/validate-invite',
      params: { token },
    },
    { apiName: this.apiName,...config });
}