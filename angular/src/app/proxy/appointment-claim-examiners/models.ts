import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface AppointmentClaimExaminerCreateDto {
  appointmentId?: string;
  name: string | null;
  suite?: string | null;
  email: string | null;
  phoneNumber?: string | null;
  fax?: string | null;
  street?: string | null;
  city?: string | null;
  zip?: string | null;
  stateId?: string | null;
  isActive?: boolean;
}

export interface AppointmentClaimExaminerDto extends FullAuditedEntityDto<string> {
  appointmentId?: string;
  name?: string | null;
  suite?: string | null;
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
  appointmentId?: string;
  name: string | null;
  suite?: string | null;
  email: string | null;
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
  appointmentId?: string | null;
}
