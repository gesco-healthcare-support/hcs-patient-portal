import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';

import { PartyConsentCardComponent } from './party-consent-card.component';
import { PartyConsentRequest, PartyConsentService } from './party-consent.service';
import { ChangeRequestType } from '../../proxy/appointment-change-requests/change-request-type.enum';
import { ChangeRequestConsentStatus } from '../../proxy/appointment-change-requests/change-request-consent-status.enum';

/**
 * The in-portal "Your agreement is needed" card (2026-10-10). Rendered, because the point is
 * what a party sees: nothing when nothing is waiting, the request with Agree / Decline when
 * something is, and a confirmation before an answer that cannot be taken back. Synthetic data.
 */
describe('PartyConsentCardComponent', () => {
  let service: { getMine: jasmine.Spy; decide: jasmine.Spy };
  let confirmation: { warn: jasmine.Spy };
  let toaster: { success: jasmine.Spy };

  const cancel = (over: Partial<PartyConsentRequest> = {}): PartyConsentRequest => ({
    changeRequestId: 'cr-1',
    appointmentId: 'appt-1',
    confirmationNumber: 'A90001',
    patientName: 'TEST-Pat TEST-Doe',
    appointmentDate: '2031-07-01T09:30:00',
    changeRequestType: ChangeRequestType.Cancel,
    reason: 'TEST-reason',
    requestedNewDateTime: null,
    consentStatus: ChangeRequestConsentStatus.Pending,
    ...over,
  });

  function render(items: PartyConsentRequest[], confirmStatus = Confirmation.Status.confirm) {
    service = {
      getMine: jasmine.createSpy('getMine').and.returnValue(of({ items })),
      decide: jasmine
        .createSpy('decide')
        .and.callFake((id: string, approved: boolean) =>
          of({ ...items[0], consentStatus: approved ? 2 : 3 }),
        ),
    };
    confirmation = { warn: jasmine.createSpy('warn').and.returnValue(of(confirmStatus)) };
    toaster = { success: jasmine.createSpy('success') };
    TestBed.configureTestingModule({
      imports: [PartyConsentCardComponent],
      providers: [
        { provide: PartyConsentService, useValue: service },
        { provide: ConfirmationService, useValue: confirmation },
        { provide: ToasterService, useValue: toaster },
      ],
    });
    const fixture = TestBed.createComponent(PartyConsentCardComponent);
    fixture.componentRef.setInput('appointmentId', 'appt-1');
    fixture.detectChanges();
    return fixture;
  }

  const buttons = (root: HTMLElement) =>
    Array.from(root.querySelectorAll<HTMLButtonElement>('button'));

  it('renders nothing when nothing is waiting', () => {
    const root = render([]).nativeElement as HTMLElement;
    expect(root.querySelector('[data-testid="party-consent-card"]')).toBeNull();
    expect(service.getMine).toHaveBeenCalledWith('appt-1');
  });

  it('shows a waiting cancellation with Agree and Decline', () => {
    const root = render([cancel()]).nativeElement as HTMLElement;
    expect(root.textContent).toContain('Your agreement is needed');
    expect(root.textContent).toContain('Cancel this appointment?');
    expect(root.textContent).toContain('TEST-reason');
    expect(buttons(root).map((b) => b.textContent!.trim())).toEqual(['Agree', 'Decline']);
  });

  it('agreeing confirms first, sends the answer, removes the item and tells the host', async () => {
    const fixture = render([cancel()]);
    const decided = jasmine.createSpy('decided');
    fixture.componentInstance.decided.subscribe(decided);

    buttons(fixture.nativeElement)[0].click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(confirmation.warn).toHaveBeenCalled();
    expect(service.decide).toHaveBeenCalledWith('cr-1', true);
    expect(decided).toHaveBeenCalled();
    expect(toaster.success).toHaveBeenCalled();
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="party-consent-card"]'),
    ).toBeNull();
  });

  it('sends nothing when the confirmation is dismissed', async () => {
    const fixture = render([cancel()], Confirmation.Status.dismiss);

    buttons(fixture.nativeElement)[1].click();
    await fixture.whenStable();

    expect(service.decide).not.toHaveBeenCalled();
  });

  it('reloads what is still waiting when the server refuses the answer', async () => {
    const fixture = render([cancel()]);
    service.decide.and.returnValue(throwError(() => ({ status: 403 })));

    buttons(fixture.nativeElement)[1].click();
    await fixture.whenStable();

    expect(service.decide).toHaveBeenCalledWith('cr-1', false);
    expect(service.getMine).toHaveBeenCalledTimes(2);
  });

  it('names the case on the home page, where several can be listed', () => {
    const fixture = render([cancel()]);
    fixture.componentRef.setInput('showSubject', true);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'TEST-Pat TEST-Doe - A90001',
    );
  });

  it('treats a failed load as nothing waiting', () => {
    service = {
      getMine: jasmine.createSpy('getMine').and.returnValue(throwError(() => ({ status: 500 }))),
      decide: jasmine.createSpy('decide'),
    };
    TestBed.configureTestingModule({
      imports: [PartyConsentCardComponent],
      providers: [
        { provide: PartyConsentService, useValue: service },
        { provide: ConfirmationService, useValue: { warn: jasmine.createSpy('warn') } },
        { provide: ToasterService, useValue: { success: jasmine.createSpy('success') } },
      ],
    });
    const fixture = TestBed.createComponent(PartyConsentCardComponent);
    fixture.detectChanges();
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="party-consent-card"]'),
    ).toBeNull();
  });
});
