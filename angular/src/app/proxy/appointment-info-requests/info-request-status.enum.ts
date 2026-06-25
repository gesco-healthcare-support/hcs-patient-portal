import { mapEnumToOptions } from '@abp/ng.core';

export enum InfoRequestStatus {
  Open = 1,
  Resolved = 2,
}

export const infoRequestStatusOptions = mapEnumToOptions(InfoRequestStatus);
