import type { UploadUserSignatureForm, UserSignatureInfoDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class UserSignatureService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  delete = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: '/api/app/user-signatures/me',
    },
    { apiName: this.apiName,...config });
  

  download = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/app/user-signatures/me/download',
    },
    { apiName: this.apiName,...config });
  

  getInfo = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, UserSignatureInfoDto>({
      method: 'GET',
      url: '/api/app/user-signatures/me',
    },
    { apiName: this.apiName,...config });
  

  upload = (form: UploadUserSignatureForm, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UserSignatureInfoDto>({
      method: 'POST',
      url: '/api/app/user-signatures/me',
      body: form.file,
    },
    { apiName: this.apiName,...config });
}