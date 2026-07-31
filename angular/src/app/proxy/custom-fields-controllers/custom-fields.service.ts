import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CustomFieldCreateDto, CustomFieldDto, CustomFieldUpdateDto, GetCustomFieldsInput } from '../custom-fields/models';

@Injectable({
  providedIn: 'root',
})
export class CustomFieldsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CustomFieldCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomFieldDto>({
      method: 'POST',
      url: '/api/app/custom-fields',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/custom-fields/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomFieldDto>({
      method: 'GET',
      url: `/api/app/custom-fields/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getActiveForAppointmentType = (appointmentTypeId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomFieldDto[]>({
      method: 'GET',
      url: `/api/app/custom-fields/by-appointment-type/${appointmentTypeId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetCustomFieldsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CustomFieldDto>>({
      method: 'GET',
      url: '/api/app/custom-fields',
      params: { filterText: input.filterText, appointmentTypeId: input.appointmentTypeId, isActive: input.isActive, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CustomFieldUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomFieldDto>({
      method: 'PUT',
      url: `/api/app/custom-fields/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}