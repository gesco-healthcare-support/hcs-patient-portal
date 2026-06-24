import type { AppointmentTypeFieldConfigCreateDto, AppointmentTypeFieldConfigDto, AppointmentTypeFieldConfigUpdateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AppointmentTypeFieldConfigService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: AppointmentTypeFieldConfigCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentTypeFieldConfigDto>({
      method: 'POST',
      url: '/api/app/appointment-type-field-configs',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/appointment-type-field-configs/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentTypeFieldConfigDto>({
      method: 'GET',
      url: `/api/app/appointment-type-field-configs/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getByAppointmentTypeId = (appointmentTypeId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentTypeFieldConfigDto[]>({
      method: 'GET',
      url: `/api/app/appointment-type-field-configs/by-appointment-type/${appointmentTypeId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (appointmentTypeId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentTypeFieldConfigDto[]>({
      method: 'GET',
      url: '/api/app/appointment-type-field-configs',
      params: { appointmentTypeId },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: AppointmentTypeFieldConfigUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentTypeFieldConfigDto>({
      method: 'PUT',
      url: `/api/app/appointment-type-field-configs/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}