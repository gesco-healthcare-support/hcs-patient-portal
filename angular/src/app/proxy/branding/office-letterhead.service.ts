import type { OfficeLetterheadDto, UpdateOfficeLetterheadInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class OfficeLetterheadService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, OfficeLetterheadDto>({
      method: 'GET',
      url: '/api/app/branding/letterhead',
    },
    { apiName: this.apiName,...config });
  

  update = (input: UpdateOfficeLetterheadInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OfficeLetterheadDto>({
      method: 'PUT',
      url: '/api/app/branding/letterhead',
      body: input,
    },
    { apiName: this.apiName,...config });
}