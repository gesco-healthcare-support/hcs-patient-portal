import type { CaseTrackerOfficePushStateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CaseTrackerPushSettingsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getOffices = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, CaseTrackerOfficePushStateDto[]>({
      method: 'GET',
      url: '/api/app/case-tracker-push-settings/offices',
    },
    { apiName: this.apiName,...config });
  

  setPushEnabled = (officeId: string, enabled: boolean, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CaseTrackerOfficePushStateDto>({
      method: 'POST',
      url: `/api/app/case-tracker-push-settings/set-push-enabled/${officeId}`,
      params: { enabled },
    },
    { apiName: this.apiName,...config });
}