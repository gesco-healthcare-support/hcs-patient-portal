import type { DeleteTestUsersResultDto, ExternalUserLookupDto, ExternalUserProfileDto, ExternalUserSignUpDto, GetInvitesInput, InvitationDto, InvitationValidationDto, InviteExternalUserDto, InviteExternalUserResultDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto, PagedResultDto } from '@abp/ng.core';
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
  

  getActiveInvitedEmails = (emails: string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, string[]>({
      method: 'GET',
      url: '/api/app/external-signup/active-invited-emails',
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
  

  getInvites = (input: GetInvitesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<InvitationDto>>({
      method: 'GET',
      url: '/api/app/external-signup/invites',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
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
  

  resendInvite = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InviteExternalUserResultDto>({
      method: 'POST',
      url: `/api/app/external-signup/${id}/resend-invite`,
    },
    { apiName: this.apiName,...config });
  

  resolveTenantByName = (name: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LookupDto<string>>({
      method: 'POST',
      url: '/api/app/external-signup/resolve-tenant-by-name',
      params: { name },
    },
    { apiName: this.apiName,...config });
  

  revokeInvite = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/external-signup/${id}/revoke-invite`,
    },
    { apiName: this.apiName,...config });
  

  validateInvite = (token: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvitationValidationDto>({
      method: 'POST',
      url: '/api/app/external-signup/validate-invite',
      params: { token },
    },
    { apiName: this.apiName,...config });
}