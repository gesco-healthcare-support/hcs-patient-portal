import { mapEnumToOptions } from '@abp/ng.core';

export enum AppointmentStatusType {
  Pending = 1,
  Approved = 2,
  Rejected = 3,
  NoShow = 4,
  CancelledNoBill = 5,
  CancelledLate = 6,
  RescheduledNoBill = 7,
  RescheduledLate = 8,
  CheckedIn = 9,
  CheckedOut = 10,
  Billed = 11,
  RescheduleRequested = 12,
  CancellationRequested = 13,
}

export const appointmentStatusTypeOptions = mapEnumToOptions(AppointmentStatusType);
