import type { AppointmentChangeRequestDto, RequestCancellationDto, RequestRescheduleDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AppointmentChangeRequestService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  requestCancellation = (appointmentId: string, input: RequestCancellationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentChangeRequestDto>({
      method: 'POST',
      url: `/api/app/appointment-change-requests/cancel/${appointmentId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  requestReschedule = (appointmentId: string, input: RequestRescheduleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentChangeRequestDto>({
      method: 'POST',
      url: `/api/app/appointment-change-requests/reschedule/${appointmentId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}