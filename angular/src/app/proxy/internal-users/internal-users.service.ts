import type { CreateInternalUserDto, InternalUserCreatedDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
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
  

  getTenantOptions = (filter?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<LookupDto<string>>>({
      method: 'GET',
      url: '/api/app/internal-users/tenants',
      params: { filter },
    },
    { apiName: this.apiName,...config });
}