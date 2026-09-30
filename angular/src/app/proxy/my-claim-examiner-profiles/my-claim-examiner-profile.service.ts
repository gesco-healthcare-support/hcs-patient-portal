import type { MyClaimExaminerProfileDto, UpdateMyClaimExaminerProfileInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class MyClaimExaminerProfileService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, MyClaimExaminerProfileDto>({
      method: 'GET',
      url: '/api/app/my-claim-examiner-profile',
    },
    { apiName: this.apiName,...config });
  

  update = (input: UpdateMyClaimExaminerProfileInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MyClaimExaminerProfileDto>({
      method: 'PUT',
      url: '/api/app/my-claim-examiner-profile',
      body: input,
    },
    { apiName: this.apiName,...config });
}