import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { ChangePasswordInput, ProfileDto, UpdateProfileDto } from '../volo/abp/account/models';
import type { NameValue } from '../volo/abp/models';

@Injectable({
  providedIn: 'root',
})
export class CaseEvaluationProfileService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  canEnableTwoFactor = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'POST',
      url: '/api/app/case-evaluation-profile/can-enable-two-factor',
    },
    { apiName: this.apiName,...config });
  

  changePassword = (input: ChangePasswordInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/case-evaluation-profile/change-password',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProfileDto>({
      method: 'GET',
      url: '/api/app/case-evaluation-profile',
    },
    { apiName: this.apiName,...config });
  

  getTimezones = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, NameValue[]>({
      method: 'GET',
      url: '/api/app/case-evaluation-profile/timezones',
    },
    { apiName: this.apiName,...config });
  

  getTwoFactorEnabled = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'GET',
      url: '/api/app/case-evaluation-profile/two-factor-enabled',
    },
    { apiName: this.apiName,...config });
  

  setTwoFactorEnabled = (enabled: boolean, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/case-evaluation-profile/set-two-factor-enabled',
      params: { enabled },
    },
    { apiName: this.apiName,...config });
  

  update = (input: UpdateProfileDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProfileDto>({
      method: 'PUT',
      url: '/api/app/case-evaluation-profile',
      body: input,
    },
    { apiName: this.apiName,...config });
}