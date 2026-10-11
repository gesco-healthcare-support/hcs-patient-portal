import type { CaseTrackerMissingIntakeReportDto } from './case-tracker/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CaseTrackerMissingIntakesService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getReport = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, CaseTrackerMissingIntakeReportDto>({
      method: 'GET',
      url: '/api/app/case-tracker/missing-intakes',
    },
    { apiName: this.apiName,...config });
}