import type { CaseTrackerDeadLetterDto, CaseTrackerDeadLetterRetryResultDto } from './case-tracker/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CaseTrackerDeadLetterService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getDeadLetters = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, CaseTrackerDeadLetterDto[]>({
      method: 'GET',
      url: '/api/app/case-tracker/dead-letters',
    },
    { apiName: this.apiName,...config });
  

  retryDeadLetter = (officeId: string, id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CaseTrackerDeadLetterRetryResultDto>({
      method: 'POST',
      url: `/api/app/case-tracker/offices/${officeId}/dead-letters/${id}/retry`,
    },
    { apiName: this.apiName,...config });
}