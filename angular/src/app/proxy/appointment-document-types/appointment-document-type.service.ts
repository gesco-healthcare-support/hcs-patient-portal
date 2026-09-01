import type { AppointmentDocumentTypeCreateDto, AppointmentDocumentTypeDto, AppointmentDocumentTypeUpdateDto, GetAppointmentDocumentTypesInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AppointmentDocumentTypeService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: AppointmentDocumentTypeCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDocumentTypeDto>({
      method: 'POST',
      url: '/api/app/appointment-document-types',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/appointment-document-types/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteAll = (input: GetAppointmentDocumentTypesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: '/api/app/appointment-document-types/all',
      params: { filterText: input.filterText, appointmentTypeId: input.appointmentTypeId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  deleteByIds = (appointmentDocumentTypeIds: string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: '/api/app/appointment-document-types',
      params: { appointmentDocumentTypeIds },
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDocumentTypeDto>({
      method: 'GET',
      url: `/api/app/appointment-document-types/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetAppointmentDocumentTypesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppointmentDocumentTypeDto>>({
      method: 'GET',
      url: '/api/app/appointment-document-types',
      params: { filterText: input.filterText, appointmentTypeId: input.appointmentTypeId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: AppointmentDocumentTypeUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentDocumentTypeDto>({
      method: 'PUT',
      url: `/api/app/appointment-document-types/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}