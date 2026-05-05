import type { DoctorAvailabilityCreateDto, DoctorAvailabilityDeleteByDateInputDto, DoctorAvailabilityDeleteBySlotInputDto, DoctorAvailabilityDto, DoctorAvailabilityGenerateInputDto, DoctorAvailabilitySlotsPreviewDto, DoctorAvailabilityUpdateDto, DoctorAvailabilityWithNavigationPropertiesDto, GetDoctorAvailabilitiesInput, GetDoctorAvailabilityLookupInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LookupDto, LookupRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class DoctorAvailabilityService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: DoctorAvailabilityCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoctorAvailabilityDto>({
      method: 'POST',
      url: '/api/app/doctor-availabilities',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/doctor-availabilities/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteByDate = (input: DoctorAvailabilityDeleteByDateInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: '/api/app/doctor-availabilities/by-date',
      params: { locationId: input.locationId, availableDate: input.availableDate },
    },
    { apiName: this.apiName,...config });
  

  deleteBySlot = (input: DoctorAvailabilityDeleteBySlotInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: '/api/app/doctor-availabilities/by-slot',
      params: { locationId: input.locationId, availableDate: input.availableDate, fromTime: input.fromTime, toTime: input.toTime },
    },
    { apiName: this.apiName,...config });
  

  generatePreview = (input: DoctorAvailabilityGenerateInputDto[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoctorAvailabilitySlotsPreviewDto[]>({
      method: 'POST',
      url: '/api/app/doctor-availabilities/preview',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoctorAvailabilityDto>({
      method: 'GET',
      url: `/api/app/doctor-availabilities/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAppointmentTypeLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/doctor-availabilities/appointment-type-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getDoctorAvailabilityLookup = (input: GetDoctorAvailabilityLookupInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoctorAvailabilityDto[]>({
      method: 'GET',
      url: '/api/app/doctor-availabilities/lookup',
      params: { locationId: input.locationId, appointmentTypeId: input.appointmentTypeId, availableDateFrom: input.availableDateFrom, availableDateTo: input.availableDateTo },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetDoctorAvailabilitiesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DoctorAvailabilityWithNavigationPropertiesDto>>({
      method: 'GET',
      url: '/api/app/doctor-availabilities',
      params: { filterText: input.filterText, availableDateMin: input.availableDateMin, availableDateMax: input.availableDateMax, fromTimeMin: input.fromTimeMin, fromTimeMax: input.fromTimeMax, toTimeMin: input.toTimeMin, toTimeMax: input.toTimeMax, bookingStatusId: input.bookingStatusId, locationId: input.locationId, appointmentTypeId: input.appointmentTypeId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getLocationLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/doctor-availabilities/location-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getWithNavigationProperties = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoctorAvailabilityWithNavigationPropertiesDto>({
      method: 'GET',
      url: `/api/app/doctor-availabilities/with-navigation-properties/${id}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: DoctorAvailabilityUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DoctorAvailabilityDto>({
      method: 'PUT',
      url: `/api/app/doctor-availabilities/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}