import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { ExternalUserProfileDto, GetInvitesInput, InvitationDto, InviteExternalUserDto, InviteExternalUserResultDto } from '../external-signups/models';

@Injectable({
  providedIn: 'root',
})
export class ExternalUserService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getInvites = (input: GetInvitesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<InvitationDto>>({
      method: 'GET',
      url: '/api/app/external-users/invites',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMyProfile = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExternalUserProfileDto>({
      method: 'GET',
      url: '/api/app/external-users/me',
    },
    { apiName: this.apiName,...config });
  

  inviteExternalUser = (input: InviteExternalUserDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InviteExternalUserResultDto>({
      method: 'POST',
      url: '/api/app/external-users/invite',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  resendInvite = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InviteExternalUserResultDto>({
      method: 'POST',
      url: `/api/app/external-users/invites/${id}/resend`,
    },
    { apiName: this.apiName,...config });
  

  revokeInvite = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/external-users/invites/${id}/revoke`,
    },
    { apiName: this.apiName,...config });
}