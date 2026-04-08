import type { Gender } from '../enums/gender.enum';
import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { IdentityUserDto } from '../volo/abp/identity/models';
import type { SaasTenantDto } from '../volo/saas/host/dtos/models';
import type { AppointmentTypeDto } from '../appointment-types/models';
import type { LocationDto } from '../locations/models';

export interface DoctorCreateDto {
  firstName: string;
  lastName: string;
  email: string;
  gender?: Gender;
  identityUserId?: string | null;
  tenantId?: string | null;
  appointmentTypeIds?: string[];
  locationIds?: string[];
}

export interface DoctorDto extends FullAuditedEntityDto<string> {
  firstName?: string;
  lastName?: string;
  email?: string;
  gender?: Gender;
  identityUserId?: string | null;
  tenantId?: string | null;
  concurrencyStamp?: string;
}

export interface DoctorUpdateDto {
  firstName: string;
  lastName: string;
  email: string;
  gender?: Gender;
  identityUserId?: string | null;
  tenantId?: string | null;
  appointmentTypeIds?: string[];
  locationIds?: string[];
  concurrencyStamp?: string;
}

export interface DoctorWithNavigationPropertiesDto {
  doctor?: DoctorDto;
  identityUser?: IdentityUserDto | null;
  tenant?: SaasTenantDto | null;
  appointmentTypes?: AppointmentTypeDto[];
  locations?: LocationDto[];
}

export interface GetDoctorsInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  email?: string | null;
  identityUserId?: string | null;
  appointmentTypeId?: string | null;
  locationId?: string | null;
}
