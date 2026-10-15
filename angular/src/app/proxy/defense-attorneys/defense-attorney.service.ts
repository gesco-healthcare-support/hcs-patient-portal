import type { DefenseAttorneyCreateDto, DefenseAttorneyDto, DefenseAttorneyUpdateDto, DefenseAttorneyWithNavigationPropertiesDto, GetDefenseAttorneysInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LookupDto, LookupRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class DefenseAttorneyService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: DefenseAttorneyCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DefenseAttorneyDto>({
      method: 'POST',
      url: '/api/app/defense-attorneys',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/defense-attorneys/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DefenseAttorneyDto>({
      method: 'GET',
      url: `/api/app/defense-attorneys/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getIdentityUserLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/defense-attorneys/identity-user-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetDefenseAttorneysInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DefenseAttorneyWithNavigationPropertiesDto>>({
      method: 'GET',
      url: '/api/app/defense-attorneys',
      params: { filterText: input.filterText, firmName: input.firmName, phoneNumber: input.phoneNumber, city: input.city, stateId: input.stateId, identityUserId: input.identityUserId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getStateLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/defense-attorneys/state-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getWithNavigationProperties = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DefenseAttorneyWithNavigationPropertiesDto>({
      method: 'GET',
      url: `/api/app/defense-attorneys/with-navigation-properties/${id}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: DefenseAttorneyUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DefenseAttorneyDto>({
      method: 'PUT',
      url: `/api/app/defense-attorneys/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}