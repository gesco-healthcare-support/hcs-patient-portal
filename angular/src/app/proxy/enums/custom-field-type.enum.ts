import { mapEnumToOptions } from '@abp/ng.core';

export enum CustomFieldType {
  Alphanumeric = 12,
  Numeric = 13,
  Picklist = 14,
  Tickbox = 15,
  Date = 16,
  Radio = 17,
  Time = 18,
}

export const customFieldTypeOptions = mapEnumToOptions(CustomFieldType);
