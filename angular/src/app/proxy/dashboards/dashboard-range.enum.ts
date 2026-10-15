import { mapEnumToOptions } from '@abp/ng.core';

export enum DashboardRange {
  Week = 0,
  Month = 1,
  Quarter = 2,
}

export const dashboardRangeOptions = mapEnumToOptions(DashboardRange);
