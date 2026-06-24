import type { GetStatesInput, StateCreateDto, StateDto, StateUpdateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StateService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: StateCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StateDto>({
      method: 'POST',
      url: '/api/app/states',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/states/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StateDto>({
      method: 'GET',
      url: `/api/app/states/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetStatesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<StateDto>>({
      method: 'GET',
      url: '/api/app/states',
      params: { filterText: input.filterText, name: input.name, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: StateUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StateDto>({
      method: 'PUT',
      url: `/api/app/states/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}