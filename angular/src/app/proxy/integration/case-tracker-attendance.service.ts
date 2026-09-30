import type { CaseTrackerAttendanceRequest } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class CaseTrackerAttendanceService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  recordAttendance = (tenantId: string, appointmentId: string, request: CaseTrackerAttendanceRequest, cancellationToken: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'POST',
      url: `/api/integration/offices/${tenantId}/appointments/${appointmentId}/attendance`,
      body: request,
    },
    { apiName: this.apiName,...config });
}