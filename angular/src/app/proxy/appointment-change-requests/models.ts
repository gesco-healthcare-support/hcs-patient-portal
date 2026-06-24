import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { ChangeRequestType } from './change-request-type.enum';
import type { RequestStatusType } from '../enums/request-status-type.enum';
import type { AppointmentStatusType } from '../enums/appointment-status-type.enum';

export interface AppointmentChangeRequestDto extends FullAuditedEntityDto<string> {
  tenantId?: string | null;
  appointmentId?: string;
  changeRequestType?: ChangeRequestType;
  cancellationReason?: string | null;
  reScheduleReason?: string | null;
  newDoctorAvailabilityId?: string | null;
  requestStatus?: RequestStatusType;
  rejectionNotes?: string | null;
  rejectedById?: string | null;
  approvedById?: string | null;
  adminReScheduleReason?: string | null;
  adminOverrideSlotId?: string | null;
  isBeyondLimit?: boolean;
  cancellationOutcome?: AppointmentStatusType | null;
}

export interface ApproveCancellationInput {
  cancellationOutcome: AppointmentStatusType;
  concurrencyStamp?: string | null;
}

export interface ApproveRescheduleInput {
  rescheduleOutcome: AppointmentStatusType;
  overrideSlotId?: string | null;
  adminReScheduleReason?: string | null;
  concurrencyStamp?: string | null;
}

export interface GetChangeRequestsInput extends PagedAndSortedResultRequestDto {
  requestStatus?: RequestStatusType | null;
  changeRequestType?: ChangeRequestType | null;
  createdFromUtc?: string | null;
  createdToUtc?: string | null;
  filterText?: string | null;
}

export interface RejectChangeRequestInput {
  reason: string;
  concurrencyStamp?: string | null;
}

export interface RequestCancellationDto {
  reason: string;
}

export interface RequestRescheduleDto {
  newDoctorAvailabilityId: string;
  reScheduleReason: string;
  isBeyondLimit?: boolean;
}
