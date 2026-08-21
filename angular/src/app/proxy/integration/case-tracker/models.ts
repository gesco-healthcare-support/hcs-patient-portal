
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

export interface CaseTrackerDeadLetterRetryResultDto {
  queuedOutboxItemId?: string;
  resolvedOutboxItemId?: string;
}

export interface CaseTrackerOfficePushStateDto {
  officeId?: string;
  officeName?: string;
  pushEnabled?: boolean;
  pendingCount?: number;
}

export interface CaseTrackerPushQueuedDto {
  appointmentId?: string;
  outboxItemId?: string;
  status?: string;
  pushEnabled?: boolean;
}
