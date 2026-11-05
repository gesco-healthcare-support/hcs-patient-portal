import { mapEnumToOptions } from '@abp/ng.core';

export enum BookingSubmitMode {
  Create = 0,
  ReSubmit = 1,
  Reval = 2,
  ReBook = 3,
  RevalWithoutSource = 4,
}

export const bookingSubmitModeOptions = mapEnumToOptions(BookingSubmitMode);
