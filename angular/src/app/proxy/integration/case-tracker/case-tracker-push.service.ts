import type { CaseTrackerPushQueuedDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CaseTrackerPushService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  pushAppointment = (appointmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CaseTrackerPushQueuedDto>({
      method: 'POST',
      url: `/api/app/case-tracker-push/push-appointment/${appointmentId}`,
    },
    { apiName: this.apiName,...config });
}