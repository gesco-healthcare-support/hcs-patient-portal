import type {
  AppointmentTypeCreateDto,
  AppointmentTypeDto,
  AppointmentTypeUpdateDto,
  GetAppointmentTypesInput,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AppointmentTypeService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: AppointmentTypeCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentTypeDto>(
      {
        method: 'POST',
        url: '/api/app/appointment-types',
        body: input,
      },
      { apiName: this.apiName, ...config },
    );

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>(
      {
        method: 'DELETE',
        url: `/api/app/appointment-types/${id}`,
      },
      { apiName: this.apiName, ...config },
    );

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentTypeDto>(
      {
        method: 'GET',
        url: `/api/app/appointment-types/${id}`,
      },
      { apiName: this.apiName, ...config },
    );

  getList = (input: GetAppointmentTypesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppointmentTypeDto>>(
      {
        method: 'GET',
        url: '/api/app/appointment-types',
        params: {
          filterText: input.filterText,
          name: input.name,
          sorting: input.sorting,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  update = (id: string, input: AppointmentTypeUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentTypeDto>(
      {
        method: 'PUT',
        url: `/api/app/appointment-types/${id}`,
        body: input,
      },
      { apiName: this.apiName, ...config },
    );
}
