import type { AppointmentPacketDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class AppointmentPacketService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  download = (appointmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: `/api/app/appointments/${appointmentId}/packet/download`,
    },
    { apiName: this.apiName,...config });
  

  getByAppointment = (appointmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentPacketDto>({
      method: 'GET',
      url: `/api/app/appointments/${appointmentId}/packet`,
    },
    { apiName: this.apiName,...config });
}