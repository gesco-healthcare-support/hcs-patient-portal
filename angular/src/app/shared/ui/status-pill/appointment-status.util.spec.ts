import { AppointmentStatusType } from '../../../proxy/enums/appointment-status-type.enum';
import { appointmentStatusToPill, appointmentStatusToSegment } from './appointment-status.util';

/**
 * Phase 4c (2026-08-05) -- pins the split of the two REQUESTED statuses out of their terminal
 * pills. Before this, an appointment merely AWAITING a decision rendered as though the decision
 * had already been made; Adrian: "that is misleading".
 *
 * The paired requirement is that nothing else moves: the new pills filter under the SAME chips
 * as before, so no seventh chip appears and every existing chip count is unchanged.
 */
describe('appointmentStatusToPill', () => {
  it('gives an in-flight reschedule its own pill instead of the terminal Rescheduled one', () => {
    expect(appointmentStatusToPill(AppointmentStatusType.RescheduleRequested)).toBe(
      'RescheduleRequested',
    );
  });

  it('gives an in-flight cancellation its own pill instead of the terminal Cancelled one', () => {
    expect(appointmentStatusToPill(AppointmentStatusType.CancellationRequested)).toBe(
      'CancellationRequested',
    );
  });

  it('still maps the settled reschedule outcomes to Rescheduled', () => {
    expect(appointmentStatusToPill(AppointmentStatusType.RescheduledNoBill)).toBe('Rescheduled');
    expect(appointmentStatusToPill(AppointmentStatusType.RescheduledLate)).toBe('Rescheduled');
  });

  it('still maps the settled cancellation outcomes to Cancelled', () => {
    expect(appointmentStatusToPill(AppointmentStatusType.CancelledNoBill)).toBe('Cancelled');
    expect(appointmentStatusToPill(AppointmentStatusType.CancelledLate)).toBe('Cancelled');
  });

  // Phase 5 (2026-08-07). NoShow used to return 'Cancelled' here -- asserted by this
  // very spec until now -- while the backend StatusPillPolicy returned null for it.
  // The appointment was neither cancelled nor absent from the UI; it was mislabelled.
  it('gives each attendance outcome its own pill instead of Cancelled', () => {
    expect(appointmentStatusToPill(AppointmentStatusType.NoShow)).toBe('NoShow');
    expect(appointmentStatusToPill(AppointmentStatusType.NotSeen)).toBe('NotSeen');
  });

  it('does not report either attendance outcome as Cancelled', () => {
    expect(appointmentStatusToPill(AppointmentStatusType.NoShow)).not.toBe('Cancelled');
    expect(appointmentStatusToPill(AppointmentStatusType.NotSeen)).not.toBe('Cancelled');
  });

  it('leaves the untouched buckets alone', () => {
    expect(appointmentStatusToPill(AppointmentStatusType.Approved)).toBe('Approved');
    expect(appointmentStatusToPill(AppointmentStatusType.CheckedIn)).toBe('Approved');
    expect(appointmentStatusToPill(AppointmentStatusType.Rejected)).toBe('Rejected');
    expect(appointmentStatusToPill(AppointmentStatusType.Pending)).toBe('Pending');
  });
});

describe('appointmentStatusToSegment', () => {
  it('keeps an in-flight reschedule under the existing Rescheduled chip', () => {
    // If this drifts, the chip counts move and a seventh chip becomes necessary -- explicitly
    // out of scope for phase 4c, which changes the pill TEXT only.
    expect(appointmentStatusToSegment(AppointmentStatusType.RescheduleRequested)).toBe(
      'rescheduled',
    );
    expect(appointmentStatusToSegment(AppointmentStatusType.RescheduledNoBill)).toBe('rescheduled');
  });

  it('keeps an in-flight cancellation under the existing Cancelled chip', () => {
    expect(appointmentStatusToSegment(AppointmentStatusType.CancellationRequested)).toBe(
      'cancelled',
    );
    expect(appointmentStatusToSegment(AppointmentStatusType.CancelledLate)).toBe('cancelled');
  });

  it('produces the same segment for every status it did before the pill split', () => {
    // A regression net over the whole enum: the pill split must not change ANY row's chip.
    const expected: ReadonlyArray<[AppointmentStatusType, string]> = [
      [AppointmentStatusType.Pending, 'pending'],
      [AppointmentStatusType.Approved, 'approved'],
      [AppointmentStatusType.CheckedIn, 'approved'],
      [AppointmentStatusType.CheckedOut, 'approved'],
      [AppointmentStatusType.Billed, 'approved'],
      [AppointmentStatusType.Rejected, 'rejected'],
      [AppointmentStatusType.CancelledNoBill, 'cancelled'],
      [AppointmentStatusType.CancelledLate, 'cancelled'],
      [AppointmentStatusType.CancellationRequested, 'cancelled'],
      // Phase 5: the PILL changed for these two, the CHIP deliberately did not.
      // This row is the proof that no external chip count moved.
      [AppointmentStatusType.NoShow, 'cancelled'],
      [AppointmentStatusType.NotSeen, 'cancelled'],
      [AppointmentStatusType.RescheduledNoBill, 'rescheduled'],
      [AppointmentStatusType.RescheduledLate, 'rescheduled'],
      [AppointmentStatusType.RescheduleRequested, 'rescheduled'],
    ];

    for (const [status, segment] of expected) {
      expect(appointmentStatusToSegment(status)).toBe(segment);
    }
  });
});
