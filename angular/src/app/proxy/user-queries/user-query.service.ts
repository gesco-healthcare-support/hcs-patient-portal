import type { UserQueryCreateDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class UserQueryService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: UserQueryCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/user-queries',
      body: input,
    },
    { apiName: this.apiName,...config });
}