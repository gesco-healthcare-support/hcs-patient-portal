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

export interface FlaggedFieldDto {
  key?: string;
  hint?: string | null;
}

export interface SendBackAppointmentInput {
  note: string;
  flaggedFields?: FlaggedFieldDto[];
}
