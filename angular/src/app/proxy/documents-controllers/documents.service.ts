import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { DocumentCreateDto, DocumentDto, DocumentUpdateDto, GetDocumentsInput } from '../documents/models';
import type { IFormFile } from '../microsoft/asp-net-core/http/models';

@Injectable({
  providedIn: 'root',
})
export class DocumentsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: DocumentCreateDto, file: IFormFile, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DocumentDto>({
      method: 'POST',
      url: '/api/app/documents',
      body: file,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/documents/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DocumentDto>({
      method: 'GET',
      url: `/api/app/documents/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetDocumentsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DocumentDto>>({
      method: 'GET',
      url: '/api/app/documents',
      params: { filterText: input.filterText, isActive: input.isActive, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  replaceFile = (id: string, file: IFormFile, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DocumentDto>({
      method: 'POST',
      url: `/api/app/documents/${id}/file`,
      body: file,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: DocumentUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DocumentDto>({
      method: 'PUT',
      url: `/api/app/documents/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}