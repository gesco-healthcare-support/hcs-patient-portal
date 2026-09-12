import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class CaseTrackerReconcileService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getAppointment = (tenantId: string, appointmentId: string, cancellationToken: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: `/api/integration/offices/${tenantId}/appointments/${appointmentId}`,
    },
    { apiName: this.apiName,...config });
}