import type { GetLocationsInput, LocationCreateDto, LocationDto, LocationUpdateDto, LocationWithNavigationPropertiesDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LookupDto, LookupRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class LocationService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: LocationCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LocationDto>({
      method: 'POST',
      url: '/api/app/locations',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/locations/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteAll = (input: GetLocationsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: '/api/app/locations/all',
      params: { filterText: input.filterText, name: input.name, city: input.city, zipCode: input.zipCode, parkingFeeMin: input.parkingFeeMin, parkingFeeMax: input.parkingFeeMax, isActive: input.isActive, stateId: input.stateId, appointmentTypeId: input.appointmentTypeId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  deleteByIds = (locationIds: string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: '/api/app/locations',
      params: { locationIds },
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LocationDto>({
      method: 'GET',
      url: `/api/app/locations/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAppointmentTypeLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/locations/appointment-type-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetLocationsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LocationWithNavigationPropertiesDto>>({
      method: 'GET',
      url: '/api/app/locations',
      params: { filterText: input.filterText, name: input.name, city: input.city, zipCode: input.zipCode, parkingFeeMin: input.parkingFeeMin, parkingFeeMax: input.parkingFeeMax, isActive: input.isActive, stateId: input.stateId, appointmentTypeId: input.appointmentTypeId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getStateLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/locations/state-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getWithNavigationProperties = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LocationWithNavigationPropertiesDto>({
      method: 'GET',
      url: `/api/app/locations/with-navigation-properties/${id}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: LocationUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, LocationDto>({
      method: 'PUT',
      url: `/api/app/locations/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}