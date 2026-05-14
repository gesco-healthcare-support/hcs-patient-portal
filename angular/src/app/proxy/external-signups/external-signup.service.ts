import type { DeleteTestUsersResultDto, ExternalUserLookupDto, ExternalUserProfileDto, ExternalUserSignUpDto, InviteExternalUserDto, InviteExternalUserResultDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LookupDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class ExternalSignupService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  deleteTestUsers = (emails: string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeleteTestUsersResultDto>({
      method: 'DELETE',
      url: '/api/app/external-signup/test-users',
      params: { emails },
    },
    { apiName: this.apiName,...config });
  

  getExternalUserLookup = (filter?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<ExternalUserLookupDto>>({
      method: 'GET',
      url: '/api/app/external-signup/external-user-lookup',
      params: { filter },
    },
    { apiName: this.apiName,...config });
  

  getMyProfile = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExternalUserProfileDto>({
      method: 'GET',
      url: '/api/app/external-signup/my-profile',
    },
    { apiName: this.apiName,...config });
  

  getTenantOptions = (filter?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/external-signup/tenant-options',
      params: { filter },
    },
    { apiName: this.apiName,...config });
  

  inviteExternalUser = (input: InviteExternalUserDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InviteExternalUserResultDto>({
      method: 'POST',
      url: '/api/app/external-signup/invite-external-user',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  markEmailConfirmed = (email: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/external-signup/mark-email-confirmed',
      params: { email },
    },
    { apiName: this.apiName,...config });
  

  register = (input: ExternalUserSignUpDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/external-signup/register',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  resolveTenantByName = (name: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LookupDto<string>>({
      method: 'POST',
      url: '/api/app/external-signup/resolve-tenant-by-name',
      params: { name },
    },
    { apiName: this.apiName,...config });
}