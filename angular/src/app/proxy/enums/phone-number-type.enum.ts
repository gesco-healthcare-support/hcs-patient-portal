import { mapEnumToOptions } from '@abp/ng.core';

export enum PhoneNumberType {
  Work = 28,
  Home = 29,
}

export const phoneNumberTypeOptions = mapEnumToOptions(PhoneNumberType);
