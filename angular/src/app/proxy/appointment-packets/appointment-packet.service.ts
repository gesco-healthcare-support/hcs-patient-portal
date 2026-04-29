import type { AppointmentPacketDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AppointmentPacketService {
  private restService = inject(RestService);
  apiName = 'Default';

  getByAppointment = (appointmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentPacketDto | null>(
      {
        method: 'GET',
        url: `/api/app/appointments/${appointmentId}/packet`,
      },
      { apiName: this.apiName, ...config },
    );

  buildDownloadUrl = (appointmentId: string): string =>
    `/api/app/appointments/${appointmentId}/packet/download`;
}
