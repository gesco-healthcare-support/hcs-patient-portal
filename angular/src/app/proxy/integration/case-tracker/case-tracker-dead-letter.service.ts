import type { CaseTrackerDeadLetterDto, CaseTrackerDeadLetterRetryResultDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CaseTrackerDeadLetterService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, CaseTrackerDeadLetterDto[]>({
      method: 'GET',
      url: '/api/app/case-tracker-dead-letter',
    },
    { apiName: this.apiName,...config });
  

  retry = (officeId: string, outboxItemId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CaseTrackerDeadLetterRetryResultDto>({
      method: 'POST',
      url: '/api/app/case-tracker-dead-letter/retry',
      params: { officeId, outboxItemId },
    },
    { apiName: this.apiName,...config });
}