import { AppointmentStatusType } from '../../../proxy/enums/appointment-status-type.enum';
import { ChangeRequestType } from '../../../proxy/appointment-change-requests/change-request-type.enum';

export interface AutoApprovePlan {
  kind: 'reschedule' | 'cancel';
  outcome: AppointmentStatusType;
}

/**
 * AP1 (decisions 1 + 2): an internal staff member who holds the
 * AppointmentChangeRequests.Approve permission gets a one-click auto-approve
 * immediately after submitting a change request. The outcome defaults to
 * NoBill -- the submit endpoints already enforce the lead-time / cancel-window
 * policy, so a request that passes submit is within policy (no late penalty);
 * "Late" outcomes are left to the supervisor approval pages. Returns null when
 * the caller cannot approve (external roles / Intake Staff), so the request
 * stays Pending for the supervisor queue. Keeping this a pure function lets the
 * decision be unit-tested without the HTTP layer.
 */
export function planAutoApprove(
  changeRequestType: ChangeRequestType | undefined,
  canApprove: boolean,
): AutoApprovePlan | null {
  if (!canApprove) {
    return null;
  }
  if (changeRequestType === ChangeRequestType.Reschedule) {
    return { kind: 'reschedule', outcome: AppointmentStatusType.RescheduledNoBill };
  }
  if (changeRequestType === ChangeRequestType.Cancel) {
    return { kind: 'cancel', outcome: AppointmentStatusType.CancelledNoBill };
  }
  return null;
}
