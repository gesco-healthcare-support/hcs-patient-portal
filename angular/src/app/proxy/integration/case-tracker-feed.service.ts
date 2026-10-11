import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IActionResult } from '../microsoft/asp-net-core/mvc/models';

@Injectable({
  providedIn: 'root',
})
export class CaseTrackerFeedService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getFeed = (tenantId: string, cursor: string, skipped: string[], cancellationToken: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, IActionResult>({
      method: 'GET',
      url: `/api/integration/offices/${tenantId}/feed`,
      params: { cursor, skipped },
    },
    { apiName: this.apiName,...config });
}