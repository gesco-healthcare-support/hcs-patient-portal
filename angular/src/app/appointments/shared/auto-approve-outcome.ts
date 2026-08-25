/**
 * Classifies why an internal booker's automatic approval did not complete.
 *
 * <p>Item B PR3 (2026-08-22). The approval stays a SEPARATE call after the atomic submit -- it
 * cannot join the submit transaction, because a PQME cannot be approved until a panel strike list
 * document is on file (`AppointmentManager.cs:478-487`) and blob uploads cannot join a database
 * transaction. So the ordering submit -> upload documents -> approve is required, not a workaround.
 * What PR1/PR2 fixed is the race behind it: the child rows are now guaranteed committed before the
 * approve call runs, which is what the F1/F2 comment was dodging.</p>
 *
 * <p>What this file fixes is the other half: the wizard used to wrap the approve call in a bare
 * `catch {}` and show one generic warning for every failure. That swallowed the reason, told a
 * booker to "approve it from the appointment view" even when the real problem was a missing strike
 * list they could fix in a second, and treated an already-approved appointment as a failure.</p>
 */

/** Already approved -- the appointment reached the desired state, so this is success. */
const ALREADY_APPROVED = 'CaseEvaluation:Appointment.NotPendingForApproval';

/**
 * Refusals from an approval gate. Every one names something the booker can act on, and retrying
 * without acting is guaranteed to fail again -- so these must NOT be retried, and the server's own
 * message is more useful than anything generic we could write here.
 */
const GATE_CODES: readonly string[] = [
  'CaseEvaluation:Appointment.ApprovalRequiresInjuryDetail',
  'CaseEvaluation:Appointment.ApprovalRequiresClaimExaminer',
  'CaseEvaluation:Appointment.ApprovalRequiresPanelStrikeList',
  'CaseEvaluation:Appointment.ApprovalRequiresResponsibleUser',
];

export type AutoApproveOutcome =
  /** Nothing to do: the appointment is already Approved. Report as success. */
  | { kind: 'alreadyApproved' }
  /** A gate refused for a reason the booker can fix. Show `message`; do NOT retry. */
  | { kind: 'blocked'; message: string }
  /** Unclassified -- possibly transient (network, 500). Worth exactly one retry. */
  | { kind: 'retryable'; message: string };

/** The generic fallback, used only when the server gave us no message of its own. */
export const AUTO_APPROVE_FALLBACK_MESSAGE =
  'Appointment booked, but it was not approved automatically. Approve it from the appointment view.';

/**
 * Pulls the ABP error envelope apart. ABP nests the payload as `error.error.{code,message}`, and
 * `withHttpErrorConfig` only screens [401,403,404,500], so a 400 arrives here intact -- the same
 * shape the booking-error branch in the wizard already reads.
 */
function readAbpError(err: unknown): { code?: string; message?: string } {
  const envelope = err as { error?: { error?: { code?: string; message?: string } } } | null;
  return {
    code: envelope?.error?.error?.code,
    message: envelope?.error?.error?.message,
  };
}

/**
 * Decides what to do about a failed approve call.
 *
 * <p>Deliberately a pure function over the error rather than logic inside the component: the
 * component is a template-less base class with heavy DI and no spec file, so anything living in it
 * is effectively untestable. This is the one part of the decision worth pinning, so it is extracted
 * -- the same move phase 4b recorded for untestable app-service branches.</p>
 */
export function classifyAutoApproveFailure(err: unknown): AutoApproveOutcome {
  const { code, message } = readAbpError(err);

  if (code === ALREADY_APPROVED) {
    return { kind: 'alreadyApproved' };
  }

  if (code && GATE_CODES.includes(code)) {
    // The server's localized message names the missing thing; ours could not.
    return { kind: 'blocked', message: message || AUTO_APPROVE_FALLBACK_MESSAGE };
  }

  return { kind: 'retryable', message: message || AUTO_APPROVE_FALLBACK_MESSAGE };
}
