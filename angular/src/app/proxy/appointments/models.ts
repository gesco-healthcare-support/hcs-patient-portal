import type { AppointmentStatusType } from '../enums/appointment-status-type.enum';
import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { PatientDto } from '../patients/models';
import type { IdentityUserDto } from '../volo/abp/identity/models';
import type { AppointmentTypeDto } from '../appointment-types/models';
import type { LocationDto } from '../locations/models';
import type { DoctorAvailabilityDto } from '../doctor-availabilities/models';

export interface AppointmentCreateDto {
  panelNumber?: string;
  appointmentDate?: string;
  isPatientAlreadyExist?: boolean;
  requestConfirmationNumber: string;
  dueDate?: string;
  internalUserComments?: string;
  appointmentApproveDate?: string;
  appointmentStatus?: AppointmentStatusType;
  patientId: string;
  identityUserId: string;
  appointmentTypeId: string;
  locationId: string;
  doctorAvailabilityId: string;
}

export interface AppointmentDto extends FullAuditedEntityDto<string> {
  panelNumber?: string;
  appointmentDate?: string;
  isPatientAlreadyExist?: boolean;
  requestConfirmationNumber: string;
  dueDate?: string;
  internalUserComments?: string;
  appointmentApproveDate?: string;
  appointmentStatus?: AppointmentStatusType;
  patientId: string;
  identityUserId: string;
  appointmentTypeId: string;
  locationId: string;
  doctorAvailabilityId: string;
  concurrencyStamp?: string;
}

export interface AppointmentUpdateDto {
  panelNumber?: string;
  appointmentDate?: string;
  isPatientAlreadyExist?: boolean;
  requestConfirmationNumber: string;
  dueDate?: string;
  internalUserComments?: string;
  appointmentApproveDate?: string;
  appointmentStatus?: AppointmentStatusType;
  patientId: string;
  identityUserId: string;
  appointmentTypeId: string;
  locationId: string;
  doctorAvailabilityId: string;
  concurrencyStamp?: string;
}

export interface AppointmentApplicantAttorneyWithNavigationPropertiesDto {
  appointmentApplicantAttorney?: { id?: string; appointmentId?: string; applicantAttorneyId?: string; identityUserId?: string };
  applicantAttorney?: { id?: string; firmName?: string; webAddress?: string; phoneNumber?: string; faxNumber?: string; street?: string; city?: string; stateId?: string; zipCode?: string; identityUserId?: string };
  identityUser?: { id?: string; name?: string; surname?: string; email?: string };
}

export interface AppointmentWithNavigationPropertiesDto {
  appointment?: AppointmentDto;
  patient: PatientDto;
  identityUser: IdentityUserDto;
  appointmentType: AppointmentTypeDto;
  location: LocationDto;
  doctorAvailability: DoctorAvailabilityDto;
  appointmentApplicantAttorney?: AppointmentApplicantAttorneyWithNavigationPropertiesDto;
}

export interface RejectAppointmentInput {
  reason?: string;
}

export interface SendBackAppointmentInput {
  flaggedFields: string[];
  note?: string;
}

export interface AppointmentSendBackInfoDto {
  id: string;
  tenantId?: string;
  appointmentId: string;
  flaggedFields: string[];
  note?: string;
  sentBackAt: string;
  sentBackByUserId?: string;
  isResolved: boolean;
  resolvedAt?: string;
}

export interface GetAppointmentsInput extends PagedAndSortedResultRequestDto {
  filterText?: string;
  panelNumber?: string;
  appointmentDateMin?: string;
  appointmentDateMax?: string;
  isPatientAlreadyExist?: boolean;
  requestConfirmationNumber?: string;
  dueDateMin?: string;
  dueDateMax?: string;
  internalUserComments?: string;
  appointmentApproveDateMin?: string;
  appointmentApproveDateMax?: string;
  appointmentStatus?: AppointmentStatusType;
  patientId?: string;
  identityUserId?: string;
  accessorIdentityUserId?: string;
  appointmentTypeId?: string;
  locationId?: string;
  doctorAvailabilityId?: string;
}
