import type { EntityDto } from '@abp/ng.core';
import type { AppNotificationType } from './app-notification-type.enum';

export interface AppNotificationDto extends EntityDto<string> {
  notificationType?: AppNotificationType;
  title?: string;
  body?: string;
  url?: string | null;
  isRead?: boolean;
  readTime?: string | null;
  creationTime?: string;
}
