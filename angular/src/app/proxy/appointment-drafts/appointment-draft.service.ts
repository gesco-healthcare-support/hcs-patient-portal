import type { AppointmentDraftDto, UpsertAppointmentDraftInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AppointmentDraftService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  discardMine = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/appointment-draft/discard-mine',
    },
    { apiName: this.apiName,...config });
  

  getMine = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDraftDto>({
      method: 'GET',
      url: '/api/app/appointment-draft/mine',
    },
    { apiName: this.apiName,...config });
  

  upsert = (input: UpsertAppointmentDraftInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDraftDto>({
      method: 'POST',
      url: '/api/app/appointment-draft/upsert',
      body: input,
    },
    { apiName: this.apiName,...config });
}