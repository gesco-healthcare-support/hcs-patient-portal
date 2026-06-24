import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface AppointmentPrimaryInsuranceCreateDto {
  appointmentId?: string;
  name?: string | null;
  suite?: string | null;
  phoneNumber?: string | null;
  faxNumber?: string | null;
  street?: string | null;
  city?: string | null;
  zip?: string | null;
  stateId?: string | null;
  isActive?: boolean;
}

export interface AppointmentPrimaryInsuranceDto extends FullAuditedEntityDto<string> {
  appointmentId?: string;
  name?: string | null;
  suite?: string | null;
  phoneNumber?: string | null;
  faxNumber?: string | null;
  street?: string | null;
  city?: string | null;
  zip?: string | null;
  stateId?: string | null;
  isActive?: boolean;
  concurrencyStamp?: string;
}

export interface AppointmentPrimaryInsuranceUpdateDto {
  appointmentId?: string;
  name?: string | null;
  suite?: string | null;
  phoneNumber?: string | null;
  faxNumber?: string | null;
  street?: string | null;
  city?: string | null;
  zip?: string | null;
  stateId?: string | null;
  isActive?: boolean;
  concurrencyStamp?: string;
}

export interface GetAppointmentPrimaryInsurancesInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  appointmentId?: string | null;
}
