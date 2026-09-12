import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { AppointmentDto } from '../appointments/models';
import type { ApplicantAttorneyDto } from '../applicant-attorneys/models';
import type { IdentityUserDto } from '../volo/abp/identity/models';

export interface AppointmentApplicantAttorneyCreateDto {
  appointmentId?: string;
  applicantAttorneyId?: string;
  identityUserId?: string;
}

export interface AppointmentApplicantAttorneyDto extends FullAuditedEntityDto<string> {
  appointmentId?: string;
  applicantAttorneyId?: string;
  identityUserId?: string;
  concurrencyStamp?: string;
}

export interface AppointmentApplicantAttorneyUpdateDto {
  appointmentId?: string;
  applicantAttorneyId?: string;
  identityUserId?: string;
  concurrencyStamp?: string;
}

export interface AppointmentApplicantAttorneyWithNavigationPropertiesDto {
  appointmentApplicantAttorney?: AppointmentApplicantAttorneyDto;
  appointment?: AppointmentDto | null;
  applicantAttorney?: ApplicantAttorneyDto | null;
  identityUser?: IdentityUserDto | null;
}

export interface GetAppointmentApplicantAttorneysInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  appointmentId?: string | null;
  applicantAttorneyId?: string | null;
  identityUserId?: string | null;
}
