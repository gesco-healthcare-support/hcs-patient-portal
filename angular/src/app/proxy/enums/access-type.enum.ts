import { mapEnumToOptions } from '@abp/ng.core';

export enum AccessType {
  View = 23,
  Edit = 24,
}

export const accessTypeOptions = mapEnumToOptions(AccessType);
