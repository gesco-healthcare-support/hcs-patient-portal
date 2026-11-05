/**
 * Types for the "stop emails" feature. They mirror the backend contract
 * (HealthcareSupport.CaseEvaluation.EmailStops) so that, once the generated proxy
 * `EmailStopService` lands in `proxy/`, switching over is a one-import change.
 */

export enum EmailStopScope {
  Case = 1,
  AllCases = 2,
}

export enum EmailRecipientKind {
  Patient = 1,
  ApplicantAttorney = 2,
  ApplicantParalegal = 3,
  DefenseAttorney = 4,
  DefenseParalegal = 5,
  ClaimExaminer = 6,
  Booker = 7,
  AuthorizedUser = 8,
}

export enum EmailRoutingNote {
  None = 0,
  SendsToParalegalInstead = 1,
  RequestReceiptOnly = 2,
  StartsAfterApproval = 3,
}

/** Existing server enum (AppointmentChangeRequests): 1 = applicant side, 2 = defense side. */
export enum ChangeRequestSide {
  SideA = 1,
  SideB = 2,
}

export interface EmailStopStatusDto {
  id: string;
  scope: EmailStopScope;
  stoppedByName: string | null;
  stoppedAt: string;
  note: string | null;
  stoppedFromOfficeName: string | null;
}

export interface AppointmentEmailRecipientDto {
  email: string;
  name: string | null;
  kinds: EmailRecipientKind[];
  routingNote: EmailRoutingNote;
  caseStop: EmailStopStatusDto | null;
  allCasesStop: EmailStopStatusDto | null;
}

export interface AppointmentEmailRecipientsDto {
  items: AppointmentEmailRecipientDto[];
}

export interface StopAppointmentEmailsInput {
  email: string;
  scope: EmailStopScope;
  note: string | null;
}

export interface EmailStopConsentImpactCaseDto {
  appointmentId: string;
  requestConfirmationNumber: string;
  side: ChangeRequestSide;
}

export interface EmailStopConsentImpactDto {
  hasImpact: boolean;
  thisCaseSides: ChangeRequestSide[];
  otherCases: EmailStopConsentImpactCaseDto[];
}

export interface GetGlobalEmailStopsInput {
  filter?: string;
  sorting?: string;
  skipCount?: number;
  maxResultCount?: number;
}

export interface GlobalEmailStopDto {
  id: string;
  email: string;
  stoppedByName: string | null;
  stoppedAt: string;
  note: string | null;
  stoppedFromOfficeName: string | null;
}

export interface CreateGlobalEmailStopInput {
  email: string;
  note: string | null;
}

export interface PagedResult<T> {
  totalCount: number;
  items: T[];
}

/** The one permission behind every email-stop screen. */
export const EMAIL_STOPS_POLICY = 'CaseEvaluation.EmailStops';
