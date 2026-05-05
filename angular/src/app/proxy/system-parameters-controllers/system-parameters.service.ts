import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { SystemParameterDto, SystemParameterUpdateDto } from '../system-parameters/models';

@Injectable({
  providedIn: 'root',
})
export class SystemParametersService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SystemParameterDto>({
      method: 'GET',
      url: '/api/app/system-parameters',
    },
    { apiName: this.apiName,...config });
  

  update = (input: SystemParameterUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SystemParameterDto>({
      method: 'PUT',
      url: '/api/app/system-parameters',
      body: input,
    },
    { apiName: this.apiName,...config });
}