import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface GetNotificationTemplatesInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  templateTypeId?: string | null;
  isActive?: boolean | null;
}

export interface NotificationTemplateDto extends FullAuditedEntityDto<string> {
  tenantId?: string | null;
  templateCode?: string;
  templateTypeId?: string;
  subject?: string | null;
  bodyEmail?: string;
  bodySms?: string;
  description?: string | null;
  isActive?: boolean;
  concurrencyStamp?: string;
}

export interface NotificationTemplateTypeDto extends FullAuditedEntityDto<string> {
  name?: string;
  isActive?: boolean;
}

export interface NotificationTemplateUpdateDto {
  subject?: string | null;
  bodyEmail: string;
  bodySms: string;
  isActive?: boolean;
  concurrencyStamp?: string;
}

export interface NotificationTemplateWithNavigationPropertiesDto {
  notificationTemplate?: NotificationTemplateDto;
  notificationTemplateType?: NotificationTemplateTypeDto | null;
}
