import type { DoctorCreateDto, DoctorDto, DoctorUpdateDto, DoctorWithNavigationPropertiesDto, GetDoctorsInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LookupDto, LookupRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class DoctorService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: DoctorCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoctorDto>({
      method: 'POST',
      url: '/api/app/doctors',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/doctors/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoctorDto>({
      method: 'GET',
      url: `/api/app/doctors/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAppointmentTypeLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/doctors/appointment-type-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetDoctorsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DoctorWithNavigationPropertiesDto>>({
      method: 'GET',
      url: '/api/app/doctors',
      params: { filterText: input.filterText, firstName: input.firstName, lastName: input.lastName, email: input.email, appointmentTypeId: input.appointmentTypeId, locationId: input.locationId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getLocationLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/doctors/location-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getTenantLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/doctors/tenant-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getWithNavigationProperties = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoctorWithNavigationPropertiesDto>({
      method: 'GET',
      url: `/api/app/doctors/with-navigation-properties/${id}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: DoctorUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoctorDto>({
      method: 'PUT',
      url: `/api/app/doctors/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}