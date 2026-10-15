import type { CaseTrackerOfficePushStateDto } from './case-tracker/models';
import type { CaseTrackerPushToggleInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CaseTrackerOfficesService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getOffices = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, CaseTrackerOfficePushStateDto[]>({
      method: 'GET',
      url: '/api/app/case-tracker/offices',
    },
    { apiName: this.apiName,...config });
  

  returnToPush = (officeId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CaseTrackerOfficePushStateDto>({
      method: 'POST',
      url: `/api/app/case-tracker/offices/${officeId}/feed/return-to-push`,
    },
    { apiName: this.apiName,...config });
  

  setPushEnabled = (officeId: string, input: CaseTrackerPushToggleInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CaseTrackerOfficePushStateDto>({
      method: 'PUT',
      url: `/api/app/case-tracker/offices/${officeId}/push`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  startFeed = (officeId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CaseTrackerOfficePushStateDto>({
      method: 'POST',
      url: `/api/app/case-tracker/offices/${officeId}/feed/start`,
    },
    { apiName: this.apiName,...config });
}