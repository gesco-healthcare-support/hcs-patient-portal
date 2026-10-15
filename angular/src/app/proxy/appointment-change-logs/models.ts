import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface AppointmentChangeLogDto {
  appointmentId?: string | null;
  entityType?: string;
  propertyName?: string;
  oldValue?: string | null;
  newValue?: string | null;
  valueRedacted?: boolean;
  changeType?: string;
  changeTime?: string;
}

export interface GetAppointmentChangeLogsInput extends PagedAndSortedResultRequestDto {
  appointmentId?: string | null;
  requestConfirmationNumber?: string | null;
  entityType?: string | null;
  fieldName?: string | null;
  changeType?: string | null;
  startTime?: string | null;
  endTime?: string | null;
}
