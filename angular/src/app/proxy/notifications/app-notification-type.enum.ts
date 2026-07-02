import { mapEnumToOptions } from '@abp/ng.core';

export enum AppNotificationType {
  AppointmentRequested = 1,
  ChangeRequestSubmitted = 2,
  QuerySubmitted = 3,
  DocumentUploaded = 4,
  InfoRequestResubmitted = 5,
}

export const appNotificationTypeOptions = mapEnumToOptions(AppNotificationType);
