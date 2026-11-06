import type { EmailRecipientKind } from './email-recipient-kind.enum';
import type { EmailRoutingNote } from './email-routing-note.enum';
import type { ChangeRequestSide } from '../appointment-change-requests/change-request-side.enum';
import type { EmailStopScope } from './email-stop-scope.enum';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface AppointmentEmailRecipientDto {
  email?: string;
  name?: string | null;
  kinds?: EmailRecipientKind[];
  routingNote?: EmailRoutingNote;
  caseStop?: EmailStopStatusDto | null;
  allCasesStop?: EmailStopStatusDto | null;
}

export interface AppointmentEmailRecipientsDto {
  items?: AppointmentEmailRecipientDto[];
}

export interface CreateGlobalEmailStopInput {
  email: string;
  note?: string | null;
}

export interface EmailStopConsentImpactCaseDto {
  appointmentId?: string;
  requestConfirmationNumber?: string;
  side?: ChangeRequestSide;
}

export interface EmailStopConsentImpactDto {
  hasImpact?: boolean;
  thisCaseSides?: ChangeRequestSide[];
  otherCases?: EmailStopConsentImpactCaseDto[];
}

export interface EmailStopStatusDto {
  id?: string;
  scope?: EmailStopScope;
  stoppedByName?: string | null;
  stoppedAt?: string;
  note?: string | null;
  stoppedFromOfficeName?: string | null;
}

export interface GetGlobalEmailStopsInput extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface GlobalEmailStopDto {
  id?: string;
  email?: string;
  stoppedByName?: string | null;
  stoppedAt?: string;
  note?: string | null;
  stoppedFromOfficeName?: string | null;
}

export interface StopAppointmentEmailsInput {
  email: string;
  scope?: EmailStopScope;
  note?: string | null;
}
