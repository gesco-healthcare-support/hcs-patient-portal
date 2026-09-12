import type { LinkDocumentsRequest } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { GetPackageDetailsInput, PackageDetailCreateDto, PackageDetailDto, PackageDetailUpdateDto, PackageDetailWithDocumentsDto } from '../package-details/models';

@Injectable({
  providedIn: 'root',
})
export class PackageDetailsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: PackageDetailCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PackageDetailDto>({
      method: 'POST',
      url: '/api/app/package-details',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/package-details/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PackageDetailDto>({
      method: 'GET',
      url: `/api/app/package-details/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetPackageDetailsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PackageDetailDto>>({
      method: 'GET',
      url: '/api/app/package-details',
      params: { filterText: input.filterText, appointmentTypeId: input.appointmentTypeId, isActive: input.isActive, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getWithDocuments = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PackageDetailWithDocumentsDto>({
      method: 'GET',
      url: `/api/app/package-details/${id}/with-documents`,
    },
    { apiName: this.apiName,...config });
  

  linkDocuments = (id: string, request: LinkDocumentsRequest, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PackageDetailWithDocumentsDto>({
      method: 'PUT',
      url: `/api/app/package-details/${id}/documents`,
      body: request,
    },
    { apiName: this.apiName,...config });
  

  unlinkDocument = (id: string, documentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/package-details/${id}/documents/${documentId}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: PackageDetailUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PackageDetailDto>({
      method: 'PUT',
      url: `/api/app/package-details/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}