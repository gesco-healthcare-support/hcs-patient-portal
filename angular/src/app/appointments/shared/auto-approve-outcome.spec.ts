import { AUTO_APPROVE_FALLBACK_MESSAGE, classifyAutoApproveFailure } from './auto-approve-outcome';

/**
 * Item B PR3 (2026-08-22). The three branches exist because they need three different reactions,
 * and the old bare `catch {}` gave all of them the same one.
 */
describe('classifyAutoApproveFailure', () => {
  const abpError = (code?: string, message?: string) => ({
    error: { error: { code, message } },
  });

  it('treats an already-approved appointment as success, not failure', () => {
    // The whole point of idempotency here: if a retry (or a double submit) already approved it, the
    // desired state has been reached. Reporting that as a failure sends the booker to approve
    // something that is already approved.
    const outcome = classifyAutoApproveFailure(
      abpError('CaseEvaluation:Appointment.NotPendingForApproval', 'Not pending.'),
    );

    expect(outcome.kind).toBe('alreadyApproved');
  });

  it('marks a missing panel strike list as blocked, and surfaces the server message', () => {
    // A PQME cannot be approved until the strike list is uploaded. This is the case that motivated
    // the whole change: it is trivially fixable, and the old generic warning never said so.
    const outcome = classifyAutoApproveFailure(
      abpError(
        'CaseEvaluation:Appointment.ApprovalRequiresPanelStrikeList',
        'A panel strike list document is required before approval.',
      ),
    );

    expect(outcome.kind).toBe('blocked');
    expect(outcome.kind === 'blocked' && outcome.message).toBe(
      'A panel strike list document is required before approval.',
    );
  });

  it('marks every other approval gate as blocked too', () => {
    // Retrying a refused gate cannot succeed, so none of these may be retried.
    const gates = [
      'CaseEvaluation:Appointment.ApprovalRequiresInjuryDetail',
      'CaseEvaluation:Appointment.ApprovalRequiresClaimExaminer',
      'CaseEvaluation:Appointment.ApprovalRequiresResponsibleUser',
    ];

    gates.forEach((code) => {
      expect(classifyAutoApproveFailure(abpError(code, 'nope')).kind).toBe(
        'blocked',
        `${code} should be blocked`,
      );
    });
  });

  it('treats an unrecognised coded failure as retryable', () => {
    const outcome = classifyAutoApproveFailure(
      abpError('CaseEvaluation:Something.Else', 'Transient thing.'),
    );

    expect(outcome.kind).toBe('retryable');
    expect(outcome.kind === 'retryable' && outcome.message).toBe('Transient thing.');
  });

  it('treats a shapeless error as retryable rather than throwing', () => {
    // A network drop or a 500 arrives with no ABP envelope at all. The classifier must survive it --
    // an exception here would replace a recoverable booking state with a crash.
    [null, undefined, new Error('boom'), {}, 'string error'].forEach((err) => {
      const outcome = classifyAutoApproveFailure(err);
      expect(outcome.kind).toBe('retryable', `${String(err)} should be retryable`);
      expect(outcome.kind === 'retryable' && outcome.message).toBe(AUTO_APPROVE_FALLBACK_MESSAGE);
    });
  });

  it('falls back to our own wording only when the server supplied no message', () => {
    const outcome = classifyAutoApproveFailure(
      abpError('CaseEvaluation:Appointment.ApprovalRequiresInjuryDetail', undefined),
    );

    expect(outcome.kind === 'blocked' && outcome.message).toBe(AUTO_APPROVE_FALLBACK_MESSAGE);
  });
});
