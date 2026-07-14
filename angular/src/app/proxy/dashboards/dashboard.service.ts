import type { DashboardRange } from './dashboard-range.enum';
import type { DashboardCountersDto, DashboardDto, DashboardTenantRowDto, GetOfficesInput, GetTenantBreakdownInput, OfficeListDto, TenantSummaryDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DashboardService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, DashboardCountersDto>({
      method: 'GET',
      url: '/api/app/dashboard',
    },
    { apiName: this.apiName,...config });
  

  getDashboard = (range: DashboardRange, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DashboardDto>({
      method: 'GET',
      url: '/api/app/dashboard/overview',
      params: { range },
    },
    { apiName: this.apiName,...config });
  

  getOffices = (input: GetOfficesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<OfficeListDto>>({
      method: 'GET',
      url: '/api/app/dashboard/offices',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getTenantBreakdown = (input: GetTenantBreakdownInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<DashboardTenantRowDto>>({
      method: 'GET',
      url: '/api/app/dashboard/tenant-breakdown',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getTenantSummaries = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantSummaryDto[]>({
      method: 'GET',
      url: '/api/app/dashboard/tenant-summaries',
    },
    { apiName: this.apiName,...config });
}