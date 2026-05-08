import type { AccessType } from '../enums/access-type.enum';
import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { IdentityUserDto } from '../volo/abp/identity/models';
import type { AppointmentDto } from '../appointments/models';

export interface AppointmentAccessorCreateDto {
  accessTypeId?: AccessType;
  identityUserId?: string;
  appointmentId?: string;
}

export interface AppointmentAccessorDto extends FullAuditedEntityDto<string> {
  accessTypeId?: AccessType;
  identityUserId?: string;
  appointmentId?: string;
}

export interface AppointmentAccessorUpdateDto {
  accessTypeId?: AccessType;
  identityUserId?: string;
  appointmentId?: string;
}

export interface AppointmentAccessorWithNavigationPropertiesDto {
  appointmentAccessor?: AppointmentAccessorDto;
  identityUser?: IdentityUserDto | null;
  appointment?: AppointmentDto | null;
}

export interface GetAppointmentAccessorsInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  accessTypeId?: AccessType | null;
  identityUserId?: string | null;
  appointmentId?: string | null;
}
