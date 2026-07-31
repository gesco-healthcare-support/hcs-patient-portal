import type { AppointmentInjuryDetailCreateDto, AppointmentInjuryDetailDto, AppointmentInjuryDetailUpdateDto, AppointmentInjuryDetailWithNavigationPropertiesDto, GetAppointmentInjuryDetailsInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LookupDto, LookupRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class AppointmentInjuryDetailService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: AppointmentInjuryDetailCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentInjuryDetailDto>({
      method: 'POST',
      url: '/api/app/appointment-injury-details',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/appointment-injury-details/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentInjuryDetailDto>({
      method: 'GET',
      url: `/api/app/appointment-injury-details/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getByAppointmentId = (appointmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentInjuryDetailWithNavigationPropertiesDto[]>({
      method: 'GET',
      url: `/api/app/appointment-injury-details/by-appointment/${appointmentId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetAppointmentInjuryDetailsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppointmentInjuryDetailWithNavigationPropertiesDto>>({
      method: 'GET',
      url: '/api/app/appointment-injury-details',
      params: { filterText: input.filterText, appointmentId: input.appointmentId, claimNumber: input.claimNumber, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getWcabOfficeLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/appointment-injury-details/wcab-office-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getWithNavigationProperties = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentInjuryDetailWithNavigationPropertiesDto>({
      method: 'GET',
      url: `/api/app/appointment-injury-details/with-navigation-properties/${id}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: AppointmentInjuryDetailUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentInjuryDetailDto>({
      method: 'PUT',
      url: `/api/app/appointment-injury-details/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}