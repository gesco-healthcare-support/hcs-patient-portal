import type { AppointmentDto, AppointmentPartiesUpdateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AppointmentPartyService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  update = (appointmentId: string, input: AppointmentPartiesUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDto>({
      method: 'PUT',
      url: `/api/app/appointments/${appointmentId}/parties`,
      body: input,
    },
    { apiName: this.apiName,...config });
}