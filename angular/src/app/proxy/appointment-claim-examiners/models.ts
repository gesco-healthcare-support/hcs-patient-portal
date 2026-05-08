import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface AppointmentClaimExaminerCreateDto {
  appointmentInjuryDetailId?: string;
  name?: string | null;
  claimExaminerNumber?: string | null;
  email?: string | null;
  phoneNumber?: string | null;
  fax?: string | null;
  street?: string | null;
  city?: string | null;
  zip?: string | null;
  stateId?: string | null;
  isActive?: boolean;
}

export interface AppointmentClaimExaminerDto extends FullAuditedEntityDto<string> {
  appointmentInjuryDetailId?: string;
  name?: string | null;
  claimExaminerNumber?: string | null;
  email?: string | null;
  phoneNumber?: string | null;
  fax?: string | null;
  street?: string | null;
  city?: string | null;
  zip?: string | null;
  stateId?: string | null;
  isActive?: boolean;
  concurrencyStamp?: string;
}

export interface AppointmentClaimExaminerUpdateDto {
  appointmentInjuryDetailId?: string;
  name?: string | null;
  claimExaminerNumber?: string | null;
  email?: string | null;
  phoneNumber?: string | null;
  fax?: string | null;
  street?: string | null;
  city?: string | null;
  zip?: string | null;
  stateId?: string | null;
  isActive?: boolean;
  concurrencyStamp?: string;
}

export interface GetAppointmentClaimExaminersInput extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  appointmentInjuryDetailId?: string | null;
}
