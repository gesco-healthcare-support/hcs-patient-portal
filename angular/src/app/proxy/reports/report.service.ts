import type { AppointmentReportRowDto, GetAppointmentReportInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class ReportService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  exportPdf = (input: GetAppointmentReportInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: '/api/app/reports/export-pdf',
      params: { filterText: input.filterText, appointmentTypeId: input.appointmentTypeId, locationId: input.locationId, appointmentStatus: input.appointmentStatus, appointmentDateMin: input.appointmentDateMin, appointmentDateMax: input.appointmentDateMax, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetAppointmentReportInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppointmentReportRowDto>>({
      method: 'GET',
      url: '/api/app/reports',
      params: { filterText: input.filterText, appointmentTypeId: input.appointmentTypeId, locationId: input.locationId, appointmentStatus: input.appointmentStatus, appointmentDateMin: input.appointmentDateMin, appointmentDateMax: input.appointmentDateMax, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}