import { mapEnumToOptions } from '@abp/ng.core';

export enum ChangeRequestSide {
  SideA = 1,
  SideB = 2,
}

export const changeRequestSideOptions = mapEnumToOptions(ChangeRequestSide);
