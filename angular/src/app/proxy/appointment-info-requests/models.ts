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

export interface SaveInfoRequestCorrectionsInput {
  dateOfBirth?: string | null;
  socialSecurityNumber?: string | null;
  address?: string | null;
  cellPhoneNumber?: string | null;
  appointmentLanguageId?: string | null;
  applicantAttorneyEmail?: string | null;
  claimExaminerEmail?: string | null;
  insuranceName?: string | null;
  insurancePhoneNumber?: string | null;
  defenseAttorneyFirmName?: string | null;
}

export interface SendBackAppointmentInput {
  note: string;
  flaggedFields?: FlaggedFieldDto[];
}
