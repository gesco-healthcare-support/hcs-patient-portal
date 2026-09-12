import type { CaseTrackerPushQueuedDto } from './case-tracker/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CaseTrackerPushService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  pushAppointment = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CaseTrackerPushQueuedDto>({
      method: 'POST',
      url: `/api/app/case-tracker/appointments/${id}/push`,
    },
    { apiName: this.apiName,...config });
}