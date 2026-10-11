import type { AppointmentStatusType } from '../../enums/appointment-status-type.enum';

export interface CaseTrackerDeadLetterDto {
  id?: string;
  officeId?: string;
  officeName?: string;
  appointmentId?: string;
  confirmationNumber?: string;
  messageType?: string;
  targetPath?: string;
  attemptCount?: number;
  lastError?: string | null;
  failedAt?: string;
  alertedAt?: string | null;
}

export interface CaseTrackerDeadLetterRetryAllResultDto {
  requeued?: number;
  alreadyDelivered?: number;
  notRetried?: number;
  remaining?: number;
}

export interface CaseTrackerDeadLetterRetryResultDto {
  queuedOutboxItemId?: string;
  resolvedOutboxItemId?: string;
  alreadyDelivered?: boolean;
}

export interface CaseTrackerMissingIntakeItemDto {
  appointmentId?: string;
  confirmationNumber?: string;
  status?: AppointmentStatusType;
  approvedAt?: string;
}

export interface CaseTrackerMissingIntakeOfficeDto {
  officeId?: string;
  officeName?: string;
  failed?: boolean;
  firstIntakeRowAt?: string | null;
  likelyLost?: CaseTrackerMissingIntakeItemDto[];
  settling?: CaseTrackerMissingIntakeItemDto[];
  beforeIntegration?: CaseTrackerMissingIntakeItemDto[];
  beforeIntegrationCount?: number;
}

export interface CaseTrackerMissingIntakeReportDto {
  generatedAt?: string;
  offices?: CaseTrackerMissingIntakeOfficeDto[];
}

export interface CaseTrackerOfficePushStateDto {
  officeId?: string;
  officeName?: string;
  pushEnabled?: boolean;
  pendingCount?: number;
  feedActive?: boolean;
  feedStartedAt?: string | null;
  lastRequestAt?: string | null;
  lastAdvancedAt?: string | null;
  outstandingCount?: number | null;
}

export interface CaseTrackerPushQueuedDto {
  appointmentId?: string;
  outboxItemId?: string;
  status?: string;
  pushEnabled?: boolean;
}
