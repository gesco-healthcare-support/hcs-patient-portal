import type { AppointmentDefenseAttorneyCreateDto, AppointmentDefenseAttorneyDto, AppointmentDefenseAttorneyUpdateDto, AppointmentDefenseAttorneyWithNavigationPropertiesDto, GetAppointmentDefenseAttorneysInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LookupDto, LookupRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class AppointmentDefenseAttorneyService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: AppointmentDefenseAttorneyCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDefenseAttorneyDto>({
      method: 'POST',
      url: '/api/app/appointment-defense-attorneys',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/appointment-defense-attorneys/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDefenseAttorneyDto>({
      method: 'GET',
      url: `/api/app/appointment-defense-attorneys/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAppointmentLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/appointment-defense-attorneys/appointment-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getDefenseAttorneyLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/appointment-defense-attorneys/defense-attorney-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getIdentityUserLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/appointment-defense-attorneys/identity-user-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetAppointmentDefenseAttorneysInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppointmentDefenseAttorneyWithNavigationPropertiesDto>>({
      method: 'GET',
      url: '/api/app/appointment-defense-attorneys',
      params: { filterText: input.filterText, appointmentId: input.appointmentId, defenseAttorneyId: input.defenseAttorneyId, identityUserId: input.identityUserId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getWithNavigationProperties = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDefenseAttorneyWithNavigationPropertiesDto>({
      method: 'GET',
      url: `/api/app/appointment-defense-attorneys/with-navigation-properties/${id}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: AppointmentDefenseAttorneyUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDefenseAttorneyDto>({
      method: 'PUT',
      url: `/api/app/appointment-defense-attorneys/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}