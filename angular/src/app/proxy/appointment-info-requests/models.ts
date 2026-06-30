import type { InfoRequestStatus } from './info-request-status.enum';

export interface AppointmentInfoRequestDto {
  id?: string;
  appointmentId?: string;
  note?: string;
  flaggedFields?: FlaggedFieldDto[];
  status?: InfoRequestStatus;
  requestedByUserId?: string | null;
  creationTime?: string;
  resolvedAt?: string | null;
}

export interface AppointmentInfoRequestRoundDto {
  id?: string;
  roundNumber?: number;
  note?: string;
  requestedByName?: string | null;
  requestedAt?: string;
  isResolved?: boolean;
  resolvedAt?: string | null;
  resubmittedByName?: string | null;
  flaggedCount?: number;
  fixedCount?: number;
  diffs?: InfoRequestFieldDiffDto[];
}

export interface FlaggedFieldDto {
  key?: string;
  hint?: string | null;
}

export interface InfoRequestFieldDiffDto {
  key?: string;
  oldValue?: string | null;
  newValue?: string | null;
  changed?: boolean;
}

export interface SaveInfoRequestCorrectionsInput {
  corrections?: Record<string, string>;
}

export interface SendBackAppointmentInput {
  note: string;
  flaggedFields?: FlaggedFieldDto[];
}
