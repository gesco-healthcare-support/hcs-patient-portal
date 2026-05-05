import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface AppointmentPrimaryInsuranceCreateDto {
  appointmentInjuryDetailId?: string;
  name?: string | null;
  insuranceNumber?: string | null;
  attention?: string | null;
  phoneNumber?: string | null;
  faxNumber?: string | null;
  street?: string | null;
  city?: string | null;
  zip?: string | null;
  stateId?: string | null;
  isActive?: boolean;
}

export interface AppointmentPrimaryInsuranceDto extends FullAuditedEntityDto<string> {
  appointmentInjuryDetailId?: string;
  name?: string | null;
  insuranceNumber?: string | null;
  attention?: string | null;
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
  appointmentInjuryDetailId?: string;
  name?: string | null;
  insuranceNumber?: string | null;
  attention?: string | null;
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
  appointmentInjuryDetailId?: string | null;
}
