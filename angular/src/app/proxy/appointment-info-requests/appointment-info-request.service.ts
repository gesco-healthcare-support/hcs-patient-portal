import type { AppointmentInfoRequestDto, SaveInfoRequestCorrectionsInput, SendBackAppointmentInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AppointmentInfoRequestService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getOpen = (appointmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentInfoRequestDto>({
      method: 'GET',
      url: `/api/app/appointment-info-requests/open/${appointmentId}`,
    },
    { apiName: this.apiName,...config });
  

  resubmit = (appointmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/appointment-info-requests/resubmit/${appointmentId}`,
    },
    { apiName: this.apiName,...config });
  

  saveCorrections = (appointmentId: string, input: SaveInfoRequestCorrectionsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/appointment-info-requests/corrections/${appointmentId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  sendBack = (appointmentId: string, input: SendBackAppointmentInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentInfoRequestDto>({
      method: 'POST',
      url: `/api/app/appointment-info-requests/send-back/${appointmentId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}