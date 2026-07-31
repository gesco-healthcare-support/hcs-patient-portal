import { mapEnumToOptions } from '@abp/ng.core';

export enum ChangeRequestConsentStatus {
  NotRequired = 0,
  Pending = 1,
  Approved = 2,
  Rejected = 3,
  Expired = 4,
}

export const changeRequestConsentStatusOptions = mapEnumToOptions(ChangeRequestConsentStatus);
