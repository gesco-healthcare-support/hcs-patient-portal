import type { AppointmentAccessorCreateDto, AppointmentAccessorDto, AppointmentAccessorUpdateDto, AppointmentAccessorWithNavigationPropertiesDto, GetAppointmentAccessorsInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LookupDto, LookupRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class AppointmentAccessorService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: AppointmentAccessorCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentAccessorDto>({
      method: 'POST',
      url: '/api/app/appointment-accessors',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/appointment-accessors/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentAccessorDto>({
      method: 'GET',
      url: `/api/app/appointment-accessors/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAppointmentLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/appointment-accessors/appointment-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getIdentityUserLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/appointment-accessors/identity-user-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetAppointmentAccessorsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppointmentAccessorWithNavigationPropertiesDto>>({
      method: 'GET',
      url: '/api/app/appointment-accessors',
      params: { filterText: input.filterText, accessTypeId: input.accessTypeId, identityUserId: input.identityUserId, appointmentId: input.appointmentId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getWithNavigationProperties = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentAccessorWithNavigationPropertiesDto>({
      method: 'GET',
      url: `/api/app/appointment-accessors/with-navigation-properties/${id}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: AppointmentAccessorUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentAccessorDto>({
      method: 'PUT',
      url: `/api/app/appointment-accessors/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}