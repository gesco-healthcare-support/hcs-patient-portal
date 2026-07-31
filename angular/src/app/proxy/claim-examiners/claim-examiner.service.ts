import type { ClaimExaminerCreateDto, ClaimExaminerDto, ClaimExaminerUpdateDto, ClaimExaminerWithNavigationPropertiesDto, GetClaimExaminersInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LookupDto, LookupRequestDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class ClaimExaminerService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: ClaimExaminerCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ClaimExaminerDto>({
      method: 'POST',
      url: '/api/app/claim-examiners',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/claim-examiners/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ClaimExaminerDto>({
      method: 'GET',
      url: `/api/app/claim-examiners/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getIdentityUserLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/claim-examiners/identity-user-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetClaimExaminersInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ClaimExaminerWithNavigationPropertiesDto>>({
      method: 'GET',
      url: '/api/app/claim-examiners',
      params: { filterText: input.filterText, email: input.email, phoneNumber: input.phoneNumber, city: input.city, stateId: input.stateId, identityUserId: input.identityUserId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getStateLookup = (input: LookupRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/claim-examiners/state-lookup',
      params: { filter: input.filter, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getWithNavigationProperties = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ClaimExaminerWithNavigationPropertiesDto>({
      method: 'GET',
      url: `/api/app/claim-examiners/with-navigation-properties/${id}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: ClaimExaminerUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ClaimExaminerDto>({
      method: 'PUT',
      url: `/api/app/claim-examiners/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}