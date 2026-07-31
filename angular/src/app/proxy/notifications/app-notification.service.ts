import type { AppNotificationDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AppNotificationService {
  private restService = inject(RestService);
  apiName = 'Default';

  getMyNotifications = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AppNotificationDto>>(
      {
        method: 'GET',
        url: '/api/app/app-notification/my-notifications',
        params: {
          sorting: input.sorting,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: this.apiName, ...config },
    );

  getMyUnreadCount = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, number>(
      {
        method: 'GET',
        url: '/api/app/app-notification/my-unread-count',
      },
      { apiName: this.apiName, ...config },
    );

  markRead = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>(
      {
        method: 'POST',
        url: `/api/app/app-notification/${id}/mark-read`,
      },
      { apiName: this.apiName, ...config },
    );

  markAllRead = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>(
      {
        method: 'POST',
        url: '/api/app/app-notification/mark-all-read',
      },
      { apiName: this.apiName, ...config },
    );
}
