import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { CancellationRequestModalComponent } from './cancellation-request-modal.component';
import { AppointmentChangeRequestService } from '../../../proxy/appointment-change-requests/appointment-change-request.service';

/**
 * The cancellation-request modal: a required reason of at most 500 characters, submitted as a
 * change request.
 *
 * <p>It had no spec. It is built in an injection context and never rendered.</p>
 *
 * <p>Identifiers below are synthetic.</p>
 */
describe('CancellationRequestModalComponent', () => {
  let requestCancellation: jasmine.Spy;

  function create(): CancellationRequestModalComponent {
    requestCancellation = jasmine
      .createSpy('requestCancellation')
      .and.returnValue(of({ id: 'cr-1' }));
    TestBed.configureTestingModule({
      providers: [{ provide: AppointmentChangeRequestService, useValue: { requestCancellation } }],
    });
    const c = TestBed.runInInjectionContext(() => new CancellationRequestModalComponent());
    c.appointmentId = 'appt-1';
    return c;
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('whether Submit is available', () => {
    it('needs a reason that is not only whitespace', () => {
      const c = create();
      c.reason = '   ';
      expect(c.canSubmit).toBeFalse();
      c.reason = 'Patient moved away';
      expect(c.canSubmit).toBeTrue();
    });

    it('allows exactly 500 characters and refuses 501', () => {
      const c = create();
      c.reason = 'x'.repeat(500);
      expect(c.canSubmit).toBeTrue();
      c.reason = 'x'.repeat(501);
      expect(c.canSubmit).toBeFalse();
    });

    it('is withheld while a request is in flight', () => {
      const c = create();
      c.reason = 'Patient moved away';
      c.isBusy = true;
      expect(c.canSubmit).toBeFalse();
    });
  });

  describe('showing and hiding', () => {
    it('announces the new visibility', () => {
      const c = create();
      const seen: boolean[] = [];
      c.visibleChange.subscribe((v) => seen.push(v));

      c.setVisible(true);

      expect(c.visible).toBeTrue();
      expect(seen).toEqual([true]);
    });

    it('clears the reason, the busy flag and the error when closed', () => {
      const c = create();
      c.reason = 'Draft';
      c.isBusy = true;
      c.errorMessage = 'Old error';

      c.setVisible(false);

      expect(c.reason).toBe('');
      expect(c.isBusy).toBeFalse();
      expect(c.errorMessage).toBeNull();
    });
  });

  describe('submitting', () => {
    it('sends the trimmed reason, reports success and closes', () => {
      const c = create();
      const succeeded: unknown[] = [];
      c.succeeded.subscribe((dto) => succeeded.push(dto));
      c.visible = true;
      c.reason = '  Patient moved away  ';

      c.submit();

      expect(requestCancellation).toHaveBeenCalledWith('appt-1', { reason: 'Patient moved away' });
      expect(succeeded).toEqual([{ id: 'cr-1' }]);
      expect(c.visible).toBeFalse();
    });

    it('does not send without an appointment, or without a valid reason', () => {
      const c = create();
      c.reason = '';
      c.submit();
      c.appointmentId = null;
      c.reason = 'Patient moved away';
      c.submit();
      expect(requestCancellation).not.toHaveBeenCalled();
    });

    it("keeps the modal open with the server's reason when the request is refused", () => {
      const c = create();
      c.visible = true;
      c.reason = 'Patient moved away';
      requestCancellation.and.returnValue(
        throwError(() => ({ error: { error: { message: 'Already cancelled.' } } })),
      );

      c.submit();

      expect(c.visible).toBeTrue();
      expect(c.isBusy).withContext('Submit and Close work again').toBeFalse();
      expect(c.errorMessage).toBe('Already cancelled.');
    });

    it('explains a refusal that carries no message', () => {
      const c = create();
      c.reason = 'Patient moved away';
      requestCancellation.and.returnValue(throwError(() => ({ status: 400 })));

      c.submit();

      expect(c.errorMessage).toBe('This appointment cannot be cancelled in its current status.');
    });
  });
});
