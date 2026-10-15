import type { AppointmentEmployerDetailCreateDto, AppointmentEmployerDetailDto, AppointmentEmployerDetailUpdateDto, AppointmentEmployerDetailWithNavigationPropertiesDto, GetAppointmentEmployerDetailsInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LookupDto, LookupRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class AppointmentEmployerDetailService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: AppointmentEmployerDetailCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentEmployerDetailDto>({
      method: 'POST',
      url: '/api/app/appointment-employer-details',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/appointment-employer-details/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentEmployerDetailDto>({
      method: 'GET',
      url: `/api/app/appointment-employer-details/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAppointmentLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/appointment-employer-details/appointment-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetAppointmentEmployerDetailsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppointmentEmployerDetailWithNavigationPropertiesDto>>({
      method: 'GET',
      url: '/api/app/appointment-employer-details',
      params: { filterText: input.filterText, employerName: input.employerName, phoneNumber: input.phoneNumber, street: input.street, city: input.city, appointmentId: input.appointmentId, stateId: input.stateId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getStateLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/appointment-employer-details/state-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getWithNavigationProperties = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentEmployerDetailWithNavigationPropertiesDto>({
      method: 'GET',
      url: `/api/app/appointment-employer-details/with-navigation-properties/${id}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: AppointmentEmployerDetailUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentEmployerDetailDto>({
      method: 'PUT',
      url: `/api/app/appointment-employer-details/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}