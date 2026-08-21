import type { AppointmentBodyPartCreateDto, AppointmentBodyPartDto, AppointmentBodyPartUpdateDto, GetAppointmentBodyPartsInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AppointmentBodyPartService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: AppointmentBodyPartCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentBodyPartDto>({
      method: 'POST',
      url: '/api/app/appointment-body-parts',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/appointment-body-parts/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentBodyPartDto>({
      method: 'GET',
      url: `/api/app/appointment-body-parts/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetAppointmentBodyPartsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppointmentBodyPartDto>>({
      method: 'GET',
      url: '/api/app/appointment-body-parts',
      params: { filterText: input.filterText, appointmentInjuryDetailId: input.appointmentInjuryDetailId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: AppointmentBodyPartUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentBodyPartDto>({
      method: 'PUT',
      url: `/api/app/appointment-body-parts/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}