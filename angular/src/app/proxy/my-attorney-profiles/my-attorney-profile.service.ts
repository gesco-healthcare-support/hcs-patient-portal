import type { MyAttorneyProfileDto, UpdateMyAttorneyProfileInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class MyAttorneyProfileService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, MyAttorneyProfileDto>({
      method: 'GET',
      url: '/api/app/my-attorney-profile',
    },
    { apiName: this.apiName,...config });
  

  update = (input: UpdateMyAttorneyProfileInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MyAttorneyProfileDto>({
      method: 'PUT',
      url: '/api/app/my-attorney-profile',
      body: input,
    },
    { apiName: this.apiName,...config });
}