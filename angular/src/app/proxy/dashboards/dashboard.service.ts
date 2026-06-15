import type { DashboardRange } from './dashboard-range.enum';
import type { DashboardCountersDto, DashboardDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
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
}