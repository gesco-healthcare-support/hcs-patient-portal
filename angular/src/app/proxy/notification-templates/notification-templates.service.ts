import type { GetNotificationTemplatesInput, NotificationTemplateDto, NotificationTemplateTypeDto, NotificationTemplateUpdateDto, NotificationTemplateWithNavigationPropertiesDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class NotificationTemplatesService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NotificationTemplateWithNavigationPropertiesDto>({
      method: 'GET',
      url: `/api/app/notification-templates/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getByCode = (templateCode: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NotificationTemplateWithNavigationPropertiesDto>({
      method: 'GET',
      url: `/api/app/notification-templates/by-code/${templateCode}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetNotificationTemplatesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<NotificationTemplateWithNavigationPropertiesDto>>({
      method: 'GET',
      url: '/api/app/notification-templates',
      params: { filterText: input.filterText, templateTypeId: input.templateTypeId, isActive: input.isActive, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getTypeLookup = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<NotificationTemplateTypeDto>>({
      method: 'GET',
      url: '/api/app/notification-templates/template-type-lookup',
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: NotificationTemplateUpdateDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NotificationTemplateDto>({
      method: 'PUT',
      url: `/api/app/notification-templates/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}