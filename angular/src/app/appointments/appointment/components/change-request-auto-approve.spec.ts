import { AppointmentStatusType } from '../../../proxy/enums/appointment-status-type.enum';
import { ChangeRequestType } from '../../../proxy/appointment-change-requests/change-request-type.enum';
import { planAutoApprove } from './change-request-auto-approve';

describe('planAutoApprove (AP1 auto-approve decision)', () => {
  it('plans a reschedule NoBill approval when the caller can approve', () => {
    const plan = planAutoApprove(ChangeRequestType.Reschedule, true);
    expect(plan).toEqual({ kind: 'reschedule', outcome: AppointmentStatusType.RescheduledNoBill });
  });

  it('plans a cancel NoBill approval when the caller can approve', () => {
    const plan = planAutoApprove(ChangeRequestType.Cancel, true);
    expect(plan).toEqual({ kind: 'cancel', outcome: AppointmentStatusType.CancelledNoBill });
  });

  it('returns null when the caller cannot approve (request stays Pending)', () => {
    expect(planAutoApprove(ChangeRequestType.Reschedule, false)).toBeNull();
    expect(planAutoApprove(ChangeRequestType.Cancel, false)).toBeNull();
  });

  it('returns null for an unknown/undefined change-request type', () => {
    expect(planAutoApprove(undefined, true)).toBeNull();
  });
});
