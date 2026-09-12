import type { AppointmentStatusType } from '../enums/appointment-status-type.enum';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface AppointmentReportRowDto {
  appointmentId?: string;
  requestConfirmationNumber?: string;
  appointmentTypeName?: string | null;
  locationName?: string | null;
  appointmentDate?: string;
  appointmentStatus?: AppointmentStatusType;
  patientName?: string | null;
  dateOfBirth?: string | null;
  email?: string | null;
  phoneNumber?: string | null;
  socialSecurityNumber?: string | null;
}

export interface GetAppointmentReportInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  appointmentTypeId?: string | null;
  locationId?: string | null;
  appointmentStatus?: AppointmentStatusType | null;
  appointmentDateMin?: string | null;
  appointmentDateMax?: string | null;
}
