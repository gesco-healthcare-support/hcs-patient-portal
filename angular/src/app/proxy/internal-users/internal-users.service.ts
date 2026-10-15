import type { CreateInternalUserDto, GetInternalUsersInput, InternalUserCreatedDto, InternalUserListDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { LookupDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class InternalUsersService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateInternalUserDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InternalUserCreatedDto>({
      method: 'POST',
      url: '/api/app/internal-users',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getInternalUsers = (input: GetInternalUsersInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<InternalUserListDto>>({
      method: 'GET',
      url: '/api/app/internal-users',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getTenantOptions = (filter?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/internal-users/tenants',
      params: { filter },
    },
    { apiName: this.apiName,...config });
  

  sendPasswordResetEmail = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/internal-users/${id}/send-password-reset`,
    },
    { apiName: this.apiName,...config });
}