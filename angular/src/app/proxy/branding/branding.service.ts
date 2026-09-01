import type { BrandingDto, GetOfficeBrandingInput, OfficeBrandingDto, SetBrandingDisplayNameInput, UploadBrandingLogoForm } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class BrandingService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getBranding = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, BrandingDto>({
      method: 'GET',
      url: '/api/app/branding',
    },
    { apiName: this.apiName,...config });
  

  getLogo = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/app/branding/logo',
    },
    { apiName: this.apiName,...config });
  

  getOfficeBrandings = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<OfficeBrandingDto>>({
      method: 'GET',
      url: '/api/app/branding/offices',
    },
    { apiName: this.apiName,...config });
  

  getOfficeLogo = (officeId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: `/api/app/branding/offices/${officeId}/logo`,
    },
    { apiName: this.apiName,...config });
  

  getPagedOfficeBrandings = (input: GetOfficeBrandingInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<OfficeBrandingDto>>({
      method: 'GET',
      url: '/api/app/branding/offices-paged',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  removeLogo = (officeId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: '/api/app/branding/logo',
      params: { officeId },
    },
    { apiName: this.apiName,...config });
  

  setDisplayName = (officeId: string, input: SetBrandingDisplayNameInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'PUT',
      url: '/api/app/branding/display-name',
      params: { officeId },
      body: input,
    },
    { apiName: this.apiName,...config });
  

  uploadLogo = (form: UploadBrandingLogoForm, officeId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BrandingDto>({
      method: 'POST',
      url: '/api/app/branding/logo',
      params: { officeId },
      body: form.file,
    },
    { apiName: this.apiName,...config });
}