import type { CaseTrackerOfficePushStateDto } from './case-tracker/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CaseTrackerOfficesService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  connect = (officeId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CaseTrackerOfficePushStateDto>({
      method: 'POST',
      url: `/api/app/case-tracker/offices/${officeId}/connect`,
    },
    { apiName: this.apiName,...config });
  

  disconnect = (officeId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CaseTrackerOfficePushStateDto>({
      method: 'POST',
      url: `/api/app/case-tracker/offices/${officeId}/disconnect`,
    },
    { apiName: this.apiName,...config });
  

  getOffices = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, CaseTrackerOfficePushStateDto[]>({
      method: 'GET',
      url: '/api/app/case-tracker/offices',
    },
    { apiName: this.apiName,...config });
}