import type { AppointmentChangeLogDto, GetAppointmentChangeLogsInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AppointmentChangeLogService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getByAppointment = (appointmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AppointmentChangeLogDto[]>({
      method: 'GET',
      url: `/api/app/appointment-change-logs/by-appointment/${appointmentId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetAppointmentChangeLogsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppointmentChangeLogDto>>({
      method: 'GET',
      url: '/api/app/appointment-change-logs',
      params: { appointmentId: input.appointmentId, requestConfirmationNumber: input.requestConfirmationNumber, entityType: input.entityType, fieldName: input.fieldName, changeType: input.changeType, startTime: input.startTime, endTime: input.endTime, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}