import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { AppointmentDto } from '../appointments/models';
import type { DefenseAttorneyDto } from '../defense-attorneys/models';
import type { IdentityUserDto } from '../volo/abp/identity/models';

export interface AppointmentDefenseAttorneyCreateDto {
  appointmentId?: string;
  defenseAttorneyId?: string;
  identityUserId?: string;
}

export interface AppointmentDefenseAttorneyDto extends FullAuditedEntityDto<string> {
  appointmentId?: string;
  defenseAttorneyId?: string;
  identityUserId?: string;
  concurrencyStamp?: string;
}

export interface AppointmentDefenseAttorneyUpdateDto {
  appointmentId?: string;
  defenseAttorneyId?: string;
  identityUserId?: string;
  concurrencyStamp?: string;
}

export interface AppointmentDefenseAttorneyWithNavigationPropertiesDto {
  appointmentDefenseAttorney?: AppointmentDefenseAttorneyDto;
  appointment?: AppointmentDto | null;
  defenseAttorney?: DefenseAttorneyDto | null;
  identityUser?: IdentityUserDto | null;
}

export interface GetAppointmentDefenseAttorneysInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  appointmentId?: string | null;
  defenseAttorneyId?: string | null;
  identityUserId?: string | null;
}
