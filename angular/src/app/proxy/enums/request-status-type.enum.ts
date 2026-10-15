import { mapEnumToOptions } from '@abp/ng.core';

export enum RequestStatusType {
  Pending = 25,
  Accepted = 26,
  Rejected = 27,
}

export const requestStatusTypeOptions = mapEnumToOptions(RequestStatusType);
