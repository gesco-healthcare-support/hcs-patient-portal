import { mapEnumToOptions } from '@abp/ng.core';

export enum EmailStopScope {
  Case = 1,
  AllCases = 2,
}

export const emailStopScopeOptions = mapEnumToOptions(EmailStopScope);
