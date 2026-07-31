import type { AppointmentStatusType } from '../enums/appointment-status-type.enum';
import type { CustomFieldValueInputDto } from '../custom-fields/models';
import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { PatientDto } from '../patients/models';
import type { IdentityUserDto } from '../volo/abp/identity/models';
import type { AppointmentTypeDto } from '../appointment-types/models';
import type { LocationDto } from '../locations/models';
import type { DoctorAvailabilityDto } from '../doctor-availabilities/models';
import type { AppointmentApplicantAttorneyWithNavigationPropertiesDto } from '../appointment-applicant-attorneys/models';
import type { AppointmentDefenseAttorneyWithNavigationPropertiesDto } from '../appointment-defense-attorneys/models';
import type { AppointmentEmployerDetailWithNavigationPropertiesDto } from '../appointment-employer-details/models';
import type { AppointmentInjuryDetailWithNavigationPropertiesDto } from '../appointment-injury-details/models';
import type { AppointmentAccessorDto } from '../appointment-accessors/models';
import type { AppointmentClaimExaminerDto } from '../appointment-claim-examiners/models';
import type { AppointmentPrimaryInsuranceDto } from '../appointment-primary-insurances/models';

export interface ApplicantAttorneyDetailsDto {
  applicantAttorneyId?: string | null;
  identityUserId?: string;
  firstName?: string;
  lastName?: string;
  email?: string;
  firmName?: string | null;
  webAddress?: string | null;
  phoneNumber?: string | null;
  faxNumber?: string | null;
  street?: string | null;
  city?: string | null;
  stateId?: string | null;
  zipCode?: string | null;
  concurrencyStamp?: string | null;
}

export interface AppointmentCreateDto {
  panelNumber?: string | null;
  appointmentDate?: string;
  requestConfirmationNumber?: string;
  dueDate?: string | null;
  appointmentStatus?: AppointmentStatusType;
  patientId?: string;
  identityUserId?: string | null;
  appointmentTypeId?: string;
  locationId?: string;
  doctorAvailabilityId?: string;
  patientEmail?: string | null;
  applicantAttorneyEmail?: string | null;
  defenseAttorneyEmail?: string | null;
  claimExaminerEmail?: string | null;
  refferedBy?: string | null;
  isPatientAlreadyExist?: boolean;
  customFieldValues?: CustomFieldValueInputDto[];
}

export interface AppointmentDto extends FullAuditedEntityDto<string> {
  panelNumber?: string | null;
  appointmentDate?: string;
  isPatientAlreadyExist?: boolean;
  requestConfirmationNumber?: string;
  dueDate?: string | null;
  internalUserComments?: string | null;
  appointmentApproveDate?: string | null;
  appointmentStatus?: AppointmentStatusType;
  rejectionNotes?: string | null;
  rejectedById?: string | null;
  patientId?: string;
  identityUserId?: string | null;
  appointmentTypeId?: string;
  locationId?: string;
  doctorAvailabilityId?: string;
  concurrencyStamp?: string;
  patientEmail?: string | null;
  applicantAttorneyEmail?: string | null;
  defenseAttorneyEmail?: string | null;
  claimExaminerEmail?: string | null;
  applicantAttorneyFirstName?: string | null;
  applicantAttorneyLastName?: string | null;
  applicantAttorneyFirmName?: string | null;
  applicantAttorneyWebAddress?: string | null;
  applicantAttorneyPhoneNumber?: string | null;
  applicantAttorneyFaxNumber?: string | null;
  applicantAttorneyStreet?: string | null;
  applicantAttorneyCity?: string | null;
  applicantAttorneyStateId?: string | null;
  applicantAttorneyZipCode?: string | null;
  defenseAttorneyFirstName?: string | null;
  defenseAttorneyLastName?: string | null;
  defenseAttorneyFirmName?: string | null;
  defenseAttorneyWebAddress?: string | null;
  defenseAttorneyPhoneNumber?: string | null;
  defenseAttorneyFaxNumber?: string | null;
  defenseAttorneyStreet?: string | null;
  defenseAttorneyCity?: string | null;
  defenseAttorneyStateId?: string | null;
  defenseAttorneyZipCode?: string | null;
  refferedBy?: string | null;
}

export interface AppointmentStatusCountDto {
  status?: AppointmentStatusType;
  count?: number;
}

export interface AppointmentUpdateDto {
  panelNumber?: string | null;
  appointmentDate?: string;
  requestConfirmationNumber?: string;
  dueDate?: string | null;
  patientId?: string;
  identityUserId?: string | null;
  appointmentTypeId?: string;
  locationId?: string;
  doctorAvailabilityId?: string;
  concurrencyStamp?: string;
  patientEmail?: string | null;
  applicantAttorneyEmail?: string | null;
  defenseAttorneyEmail?: string | null;
  claimExaminerEmail?: string | null;
  refferedBy?: string | null;
  customFieldValues?: CustomFieldValueInputDto[];
}

export interface AppointmentWithNavigationPropertiesDto {
  appointment?: AppointmentDto;
  patient?: PatientDto | null;
  identityUser?: IdentityUserDto | null;
  bookedByUser?: IdentityUserDto | null;
  appointmentType?: AppointmentTypeDto | null;
  location?: LocationDto | null;
  doctorAvailability?: DoctorAvailabilityDto | null;
  appointmentApplicantAttorney?: AppointmentApplicantAttorneyWithNavigationPropertiesDto | null;
  appointmentDefenseAttorney?: AppointmentDefenseAttorneyWithNavigationPropertiesDto | null;
  appointmentEmployerDetail?: AppointmentEmployerDetailWithNavigationPropertiesDto | null;
  appointmentInjuryDetails?: AppointmentInjuryDetailWithNavigationPropertiesDto[];
  appointmentAccessors?: AppointmentAccessorDto[];
  claimExaminer?: AppointmentClaimExaminerDto | null;
  primaryInsurance?: AppointmentPrimaryInsuranceDto | null;
}

export interface ApproveAppointmentInput {
  primaryResponsibleUserId: string;
  internalUserComments?: string | null;
}

export interface DefenseAttorneyDetailsDto {
  defenseAttorneyId?: string | null;
  identityUserId?: string;
  firstName?: string;
  lastName?: string;
  email?: string;
  firmName?: string | null;
  webAddress?: string | null;
  phoneNumber?: string | null;
  faxNumber?: string | null;
  street?: string | null;
  city?: string | null;
  stateId?: string | null;
  zipCode?: string | null;
  concurrencyStamp?: string | null;
}

export interface GetAppointmentsInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  panelNumber?: string | null;
  appointmentDateMin?: string | null;
  appointmentDateMax?: string | null;
  identityUserId?: string | null;
  accessorIdentityUserId?: string | null;
  appointmentTypeId?: string | null;
  locationId?: string | null;
  patientId?: string | null;
  appointmentStatus?: AppointmentStatusType | null;
  appointmentStatuses?: AppointmentStatusType[] | null;
}

export interface RejectAppointmentInput {
  reason: string;
}
