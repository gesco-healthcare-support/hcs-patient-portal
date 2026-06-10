import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class AppointmentDemographicsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getPdf = (appointmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: `/api/app/appointment-demographics/${appointmentId}`,
    },
    { apiName: this.apiName,...config });
}