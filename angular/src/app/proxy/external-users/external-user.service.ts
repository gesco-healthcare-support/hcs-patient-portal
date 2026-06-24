import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { ExternalUserProfileDto, InviteExternalUserDto, InviteExternalUserResultDto } from '../external-signups/models';

@Injectable({
  providedIn: 'root',
})
export class ExternalUserService {
  private restService = inject(RestService);
  apiName = 'Default';
  

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
}