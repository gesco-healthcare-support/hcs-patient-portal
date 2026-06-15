import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { ChangeRequestType } from './change-request-type.enum';
import type { ChangeRequestConsentStatus } from './change-request-consent-status.enum';
import type { ChangeRequestSide } from './change-request-side.enum';
import type { RequestStatusType } from '../enums/request-status-type.enum';
import type { AppointmentStatusType } from '../enums/appointment-status-type.enum';

export interface AppointmentChangeRequestDto extends FullAuditedEntityDto<string> {
  tenantId?: string | null;
  appointmentId?: string;
  // Human-facing confirmation number (e.g. "A00077") populated server-side in
  // GetPendingChangeRequestsAsync (not by the mapper) so the inbox shows it.
  appointmentConfirmationNumber?: string | null;
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
  // Group D opposing-side consent state (NotRequired/Pending/Approved/Rejected/Expired)
  // + which side filed the request. Auto-mapped from the entity by Mapperly.
  consentStatus?: ChangeRequestConsentStatus;
  requestingSide?: ChangeRequestSide | null;
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
