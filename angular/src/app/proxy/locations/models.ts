import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { StateDto } from '../states/models';
import type { AppointmentTypeDto } from '../appointment-types/models';

export interface GetLocationsInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  name?: string | null;
  city?: string | null;
  zipCode?: string | null;
  parkingFeeMin?: number | null;
  parkingFeeMax?: number | null;
  isActive?: boolean | null;
  stateId?: string | null;
  appointmentTypeId?: string | null;
}

export interface LocationCreateDto {
  name: string;
  address?: string | null;
  city?: string | null;
  zipCode?: string | null;
  parkingFee?: number;
  isActive?: boolean;
  stateId?: string | null;
  appointmentTypeId?: string | null;
}

export interface LocationDto extends FullAuditedEntityDto<string> {
  name?: string;
  address?: string | null;
  city?: string | null;
  zipCode?: string | null;
  parkingFee?: number;
  isActive?: boolean;
  stateId?: string | null;
  appointmentTypeId?: string | null;
  concurrencyStamp?: string;
}

export interface LocationUpdateDto {
  name: string;
  address?: string | null;
  city?: string | null;
  zipCode?: string | null;
  parkingFee?: number;
  isActive?: boolean;
  stateId?: string | null;
  appointmentTypeId?: string | null;
  concurrencyStamp?: string;
}

export interface LocationWithNavigationPropertiesDto {
  location?: LocationDto;
  state?: StateDto | null;
  appointmentType?: AppointmentTypeDto | null;
}
