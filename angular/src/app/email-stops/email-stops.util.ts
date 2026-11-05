import { ChangeRequestSide, EmailRecipientKind, EmailRoutingNote } from './email-stops.models';
import type { AppointmentEmailRecipientDto, EmailStopConsentImpactDto } from './email-stops.models';

export type StopPill = 'allCases' | 'case' | 'active';

/** allCasesStop wins over caseStop; neither means the person is getting emails. */
export function pillFor(r: AppointmentEmailRecipientDto): StopPill {
  if (r.allCasesStop) {
    return 'allCases';
  }
  return r.caseStop ? 'case' : 'active';
}

const KIND_KEYS: Record<EmailRecipientKind, string> = {
  [EmailRecipientKind.Patient]: '::EmailStops:Kind:Patient',
  [EmailRecipientKind.ApplicantAttorney]: '::EmailStops:Kind:ApplicantAttorney',
  [EmailRecipientKind.ApplicantParalegal]: '::EmailStops:Kind:ApplicantParalegal',
  [EmailRecipientKind.DefenseAttorney]: '::EmailStops:Kind:DefenseAttorney',
  [EmailRecipientKind.DefenseParalegal]: '::EmailStops:Kind:DefenseParalegal',
  [EmailRecipientKind.ClaimExaminer]: '::EmailStops:Kind:ClaimExaminer',
  [EmailRecipientKind.Booker]: '::EmailStops:Kind:Booker',
  [EmailRecipientKind.AuthorizedUser]: '::EmailStops:Kind:AuthorizedUser',
};

export function kindKey(kind: EmailRecipientKind): string {
  return KIND_KEYS[kind] ?? KIND_KEYS[EmailRecipientKind.AuthorizedUser];
}

const NOTE_KEYS: Partial<Record<EmailRoutingNote, string>> = {
  [EmailRoutingNote.SendsToParalegalInstead]: '::EmailStops:Note:SendsToParalegalInstead',
  [EmailRoutingNote.RequestReceiptOnly]: '::EmailStops:Note:RequestReceiptOnly',
  [EmailRoutingNote.StartsAfterApproval]: '::EmailStops:Note:StartsAfterApproval',
};

/** The small grey routing note key, or null when there is nothing to say. */
export function routingNoteKey(note: EmailRoutingNote): string | null {
  return NOTE_KEYS[note] ?? null;
}

export function sideKey(side: ChangeRequestSide): string {
  return side === ChangeRequestSide.SideA
    ? '::EmailStops:Side:Applicant'
    : '::EmailStops:Side:Defense';
}

/** Lower-cased set of every address that is stopped (this case or all cases). */
export function stoppedEmailSet(items: AppointmentEmailRecipientDto[]): Set<string> {
  return new Set(
    items.filter((r) => r.caseStop || r.allCasesStop).map((r) => r.email.trim().toLowerCase()),
  );
}

export function isStoppedIn(set: ReadonlySet<string>, email: string | null | undefined): boolean {
  return !!email && set.has(email.trim().toLowerCase());
}

/** Confirmation numbers of the other cases, joined for the warning text. */
export function otherCaseNumbers(impact: EmailStopConsentImpactDto): string {
  return impact.otherCases.map((c) => c.requestConfirmationNumber).join(', ');
}

/** Distinct sides on this case that depend only on this address. */
export function distinctSides(impact: EmailStopConsentImpactDto): ChangeRequestSide[] {
  return [...new Set(impact.thisCaseSides)];
}
