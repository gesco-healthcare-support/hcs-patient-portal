import { mapEnumToOptions } from '@abp/ng.core';

export enum ChangeRequestType {
  Cancel = 1,
  Reschedule = 2,
}

export const changeRequestTypeOptions = mapEnumToOptions(ChangeRequestType);
