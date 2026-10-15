import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';

import { InternalChangeRequestInboxComponent } from './internal-change-request-inbox.component';
import { AppointmentChangeRequestApprovalService } from '../../proxy/appointment-change-requests/appointment-change-request-approval.service';
import { ChangeRequestType } from '../../proxy/appointment-change-requests/change-request-type.enum';
import { AppointmentStatusType } from '../../proxy/enums/appointment-status-type.enum';

/**
 * The approve modal, RENDERED, at each reschedule stage plus the cancellation it shares with.
 *
 * <p>2026-10-14 click-through: the modal showed a green Approve button, disabled, while consent
 * was pending. Since 2026-10-09 a reschedule completes ON ITS OWN once both sides agree, so in
 * the normal path staff never approve it and the dead button read as "something is stuck". The
 * modal now shows a read-only status with Resend as the main action while consent is pending,
 * and offers Approve only in the fallback where both agreed but the automatic completion did
 * not happen. The outcome was chosen at Set date, so it is shown, not asked again.</p>
 *
 * <p>A party-requested CANCELLATION keeps the staff Approve after consent (Q11), so its modal
 * is pinned unchanged here as well.</p>
 *
 * <p>All identifiers and confirmation numbers are synthetic.</p>
 */
describe('InternalChangeRequestInboxComponent approve modal', () => {
  const CONSENT_PENDING = 1;
  const CONSENT_APPROVED = 2;

  let service: Record<string, jasmine.Spy>;

  function reschedule(over: Record<string, unknown> = {}) {
    return {
      id: 'cr-1',
      appointmentId: 'appt-1',
      appointmentConfirmationNumber: 'C0001',
      changeRequestType: ChangeRequestType.Reschedule,
      creationTime: '2026-10-14T00:00:00Z',
      currentConsentRoundNumber: 1,
      // The value as the server sent it before the 2026-10-14 fix: a calendar date at UTC midnight.
      currentRoundProposedDate: '2026-10-20T00:00:00Z',
      currentRoundProposedFromTime: '10:30',
      currentRoundSideAStatus: CONSENT_APPROVED,
      currentRoundSideBStatus: CONSENT_PENDING,
      currentRoundProposedOutcome: AppointmentStatusType.RescheduledLate,
      ...over,
    };
  }

  const bothAgreed = {
    currentRoundSideAStatus: CONSENT_APPROVED,
    currentRoundSideBStatus: CONSENT_APPROVED,
  };

  function render(row: Record<string, unknown>) {
    service = {
      getPending: jasmine.createSpy('getPending').and.returnValue(of({ items: [], totalCount: 0 })),
      resendConsentRequest: jasmine
        .createSpy('resendConsentRequest')
        .and.returnValue(of(undefined)),
      approveReschedule: jasmine.createSpy('approveReschedule').and.returnValue(of(undefined)),
      approveCancellation: jasmine.createSpy('approveCancellation').and.returnValue(of(undefined)),
    };
    TestBed.configureTestingModule({
      providers: [
        { provide: AppointmentChangeRequestApprovalService, useValue: service },
        {
          provide: Router,
          useValue: { navigate: () => undefined, navigateByUrl: () => undefined },
        },
        {
          provide: ToasterService,
          useValue: {
            success: () => undefined,
            warn: () => undefined,
            error: () => undefined,
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(InternalChangeRequestInboxComponent);
    fixture.detectChanges(); // ngOnInit -> load(), against the empty stub queue
    const c = fixture.componentInstance as unknown as {
      openApprove(r: unknown): void;
    };
    c.openApprove(row);
    fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    const modal = host.querySelector('.ra-modal') as HTMLElement;
    const buttons = () =>
      Array.from(modal.querySelectorAll('.ra-modal__foot button')) as HTMLButtonElement[];
    const button = (text: string) => buttons().find((b) => b.textContent?.trim().includes(text));
    return { fixture, modal, buttons, button };
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('a reschedule waiting for consent', () => {
    it('shows the status read-only and offers no Approve', () => {
      const v = render(reschedule());

      expect(v.modal.querySelector('h3')?.textContent?.trim()).toBe('Reschedule awaiting consent');
      expect(v.modal.textContent).toContain('Oct 20, 2026 at 10:30');
      expect(v.modal.textContent).toContain('Billing outcome:');
      expect(v.modal.textContent).toContain('Late');
      expect(v.modal.querySelector('#cr-billing-outcome')).toBeNull();
      expect(v.button('Approve')).toBeUndefined();
    });

    it('makes Resend the main action, next to Close', () => {
      const v = render(reschedule());

      const resend = v.button('Resend consent request');
      expect(resend).toBeDefined();
      expect(resend!.classList).toContain('af-btn--blue');
      expect(v.button('Close')).toBeDefined();

      resend!.click();
      expect(service['resendConsentRequest']).toHaveBeenCalled();
    });
  });

  describe('a reschedule both sides agreed to that did not complete on its own', () => {
    it('offers Approve, with the outcome chosen at Set date shown rather than asked', () => {
      const v = render(reschedule(bothAgreed));

      expect(v.modal.querySelector('h3')?.textContent?.trim()).toBe('Complete reschedule');
      expect(v.modal.querySelector('#cr-billing-outcome')).toBeNull();
      expect(v.modal.textContent).toContain('Late');
      const approve = v.button('Approve');
      expect(approve).toBeDefined();
      expect(approve!.disabled).toBeFalse();
      expect(v.button('Resend consent request')).toBeUndefined();
    });

    it('finalizes with the outcome chosen at Set date, not the modal default', () => {
      const v = render(reschedule(bothAgreed));

      v.button('Approve')!.click();

      expect(service['approveReschedule']).toHaveBeenCalled();
      const [, body] = service['approveReschedule'].calls.mostRecent().args;
      expect(body).toEqual({ rescheduleOutcome: AppointmentStatusType.RescheduledLate });
    });

    it('asks for the outcome on a round opened before outcomes were chosen at Set date', () => {
      const v = render(reschedule({ ...bothAgreed, currentRoundProposedOutcome: null }));

      expect(v.modal.querySelector('#cr-billing-outcome')).not.toBeNull();
      expect(v.button('Approve')!.disabled).toBeFalse();
    });
  });

  describe('a cancellation (unchanged: staff approve after consent, Q11)', () => {
    function cancellation(over: Record<string, unknown> = {}) {
      return {
        id: 'cr-2',
        appointmentId: 'appt-2',
        appointmentConfirmationNumber: 'C0002',
        changeRequestType: ChangeRequestType.Cancel,
        creationTime: '2026-10-14T00:00:00Z',
        sideAConsentStatus: CONSENT_APPROVED,
        sideBConsentStatus: CONSENT_APPROVED,
        ...over,
      };
    }

    it('keeps the outcome choice and an Approve that is live once consent is in', () => {
      const v = render(cancellation());

      expect(v.modal.querySelector('h3')?.textContent?.trim()).toBe('Approve cancellation');
      expect(v.modal.querySelector('#cr-billing-outcome')).not.toBeNull();
      expect(v.button('Approve')!.disabled).toBeFalse();
    });

    it('keeps Approve disabled while a side has not agreed', () => {
      const v = render(cancellation({ sideBConsentStatus: CONSENT_PENDING }));

      expect(v.button('Approve')!.disabled).toBeTrue();
    });
  });
});
