import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { AppointmentDto } from '../appointments/models';
import type { StateDto } from '../states/models';

export interface AppointmentEmployerDetailCreateDto {
  employerName: string;
  occupation: string;
  phoneNumber?: string | null;
  street?: string | null;
  city?: string | null;
  zipCode?: string | null;
  appointmentId?: string;
  stateId?: string | null;
}

export interface AppointmentEmployerDetailDto extends FullAuditedEntityDto<string> {
  employerName?: string;
  occupation?: string;
  phoneNumber?: string | null;
  street?: string | null;
  city?: string | null;
  zipCode?: string | null;
  appointmentId?: string;
  stateId?: string | null;
  concurrencyStamp?: string;
}

export interface AppointmentEmployerDetailUpdateDto {
  employerName: string;
  occupation: string;
  phoneNumber?: string | null;
  street?: string | null;
  city?: string | null;
  zipCode?: string | null;
  appointmentId?: string;
  stateId?: string | null;
  concurrencyStamp?: string;
}

export interface AppointmentEmployerDetailWithNavigationPropertiesDto {
  appointmentEmployerDetail?: AppointmentEmployerDetailDto;
  appointment?: AppointmentDto | null;
  state?: StateDto | null;
}

export interface GetAppointmentEmployerDetailsInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  employerName?: string | null;
  phoneNumber?: string | null;
  street?: string | null;
  city?: string | null;
  appointmentId?: string | null;
  stateId?: string | null;
}
