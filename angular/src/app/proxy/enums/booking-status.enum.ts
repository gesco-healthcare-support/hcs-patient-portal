import { mapEnumToOptions } from '@abp/ng.core';

export enum BookingStatus {
  Available = 8,
  Booked = 9,
  Reserved = 10,
}

export const bookingStatusOptions = mapEnumToOptions(BookingStatus);
