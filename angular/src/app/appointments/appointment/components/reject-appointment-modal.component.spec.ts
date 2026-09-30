import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';

import { RejectAppointmentModalComponent } from './reject-appointment-modal.component';
import { AppointmentApprovalService } from '../../../proxy/appointments/appointment-approval.service';
import type { AppointmentDto } from '../../../proxy/appointments/models';

/**
 * #629 marked the two Outputs and two injected services readonly
 * (typescript:S2933). The class had no spec, so the change would have landed
 * unverified -- and readonly on an EventEmitter is exactly the kind of edit
 * that looks inert until something reassigns it.
 *
 * The behaviour worth pinning while here: setVisible(false) resets the reason,
 * so a rejected-then-reopened modal cannot submit the previous text, and a
 * failed submit clears isBusy so the button is not stuck.
 */
describe('RejectAppointmentModalComponent (#629)', () => {
  let toasts: string[];
  let rejectCalls: Array<{ id: string; reason: string }>;

  function create(result: 'ok' | 'error' = 'ok'): RejectAppointmentModalComponent {
    toasts = [];
    rejectCalls = [];
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AppointmentApprovalService,
          useValue: {
            rejectAppointment: (id: string, input: { reason: string }) => {
              rejectCalls.push({ id, reason: input.reason });
              return result === 'ok'
                ? of({ id } as AppointmentDto)
                : throwError(() => new Error('rejected'));
            },
          },
        },
        {
          provide: ToasterService,
          useValue: { success: (m: string) => toasts.push(m), error: () => undefined },
        },
      ],
    });
    return TestBed.runInInjectionContext(() => new RejectAppointmentModalComponent());
  }

  afterEach(() => TestBed.resetTestingModule());

  it('blocks submit until a non-blank reason is typed', () => {
    const c = create();
    expect(c.canSubmit).toBeFalse();
    c.reason = '   ';
    expect(c.canSubmit).toBeFalse();
    c.reason = 'Not a valid panel number';
    expect(c.canSubmit).toBeTrue();
  });

  it('blocks submit past the reason length cap', () => {
    const c = create();
    c.reason = 'x'.repeat(c.maxReasonLength);
    expect(c.canSubmit).toBeTrue();
    c.reason = 'x'.repeat(c.maxReasonLength + 1);
    expect(c.canSubmit).toBeFalse();
  });

  it('emits the new visibility and clears the reason on close', () => {
    const c = create();
    const emitted: boolean[] = [];
    c.visibleChange.subscribe((v) => emitted.push(v));
    c.reason = 'typed but abandoned';
    c.isBusy = true;

    c.setVisible(false);

    expect(emitted).toEqual([false]);
    expect(c.reason).toBe('');
    expect(c.isBusy).toBeFalse();
  });

  it('submits the trimmed reason and closes on success', () => {
    const c = create('ok');
    const succeeded: AppointmentDto[] = [];
    c.succeeded.subscribe((d) => succeeded.push(d));
    c.appointmentId = 'appt-1';
    c.reason = '  wrong location  ';

    c.submit();

    expect(rejectCalls).toEqual([{ id: 'appt-1', reason: 'wrong location' }]);
    expect(toasts).toEqual(['Appointment booking request has been Rejected']);
    expect(succeeded).toHaveSize(1);
    expect(c.visible).toBeFalse();
  });

  it('does nothing without an appointment id', () => {
    const c = create();
    c.appointmentId = null;
    c.reason = 'valid reason';

    c.submit();

    expect(rejectCalls).toEqual([]);
  });

  /** Without this the button stays disabled after a server error and the
   *  requester has to reload to try again. */
  it('clears the busy flag when the request fails', () => {
    const c = create('error');
    c.appointmentId = 'appt-1';
    c.reason = 'valid reason';

    c.submit();

    expect(c.isBusy).toBeFalse();
    expect(c.visible).toBeFalse();
  });
});
