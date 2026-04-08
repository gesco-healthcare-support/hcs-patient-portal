import type {
  AppointmentStatusCreateDto,
  AppointmentStatusDto,
  AppointmentStatusUpdateDto,
  GetAppointmentStatusesInput,
} from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AppointmentStatusService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: AppointmentStatusCreateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentStatusDto>(
      {
        method: 'POST',
        url: '/api/app/appointment-statuses',
        body: input,
      },
      { apiName: this.apiName, ...config },
    );

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>(
      {
        method: 'DELETE',
        url: `/api/app/appointment-statuses/${id}`,
      },
      { apiName: this.apiName, ...config },
    );

  deleteAll = (input: GetAppointmentStatusesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>(
      {
        method: 'DELETE',
        url: '/api/app/appointment-statuses/all',
        params: {
          filterText: input.filterText,
          sorting: input.sorting,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  deleteByIds = (appointmentstatusIds: string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>(
      {
        method: 'DELETE',
        url: '/api/app/appointment-statuses',
        params: { appointmentstatusIds },
      },
      { apiName: this.apiName, ...config },
    );

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentStatusDto>(
      {
        method: 'GET',
        url: `/api/app/appointment-statuses/${id}`,
      },
      { apiName: this.apiName, ...config },
    );

  getList = (input: GetAppointmentStatusesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppointmentStatusDto>>(
      {
        method: 'GET',
        url: '/api/app/appointment-statuses',
        params: {
          filterText: input.filterText,
          sorting: input.sorting,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  update = (id: string, input: AppointmentStatusUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentStatusDto>(
      {
        method: 'PUT',
        url: `/api/app/appointment-statuses/${id}`,
        body: input,
      },
      { apiName: this.apiName, ...config },
    );
}
