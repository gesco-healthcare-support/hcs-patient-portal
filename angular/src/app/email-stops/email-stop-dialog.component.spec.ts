import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LocalizationService } from '@abp/ng.core';
import { Subject } from 'rxjs';
import { EmailStopDialogComponent } from './email-stop-dialog.component';
import { EmailStopsService } from '../proxy/email-stops';
import type { EmailStopConsentImpactDto, EmailStopStatusDto } from '../proxy/email-stops';

const NO_IMPACT: EmailStopConsentImpactDto = {
  hasImpact: false,
  thisCaseSides: [],
  otherCases: [],
};

describe('EmailStopDialogComponent', () => {
  let service: jasmine.SpyObj<EmailStopsService>;
  let fixture: ComponentFixture<EmailStopDialogComponent>;
  let pending: Subject<EmailStopConsentImpactDto>[];

  const confirm = (): HTMLButtonElement =>
    (fixture.nativeElement as HTMLElement).querySelector(
      '[data-testid="confirm-stop"]',
    ) as HTMLButtonElement;
  const click = (sel: string): void => {
    ((fixture.nativeElement as HTMLElement).querySelector(sel) as HTMLElement).click();
    fixture.detectChanges();
  };

  beforeEach(() => {
    pending = [];
    service = jasmine.createSpyObj<EmailStopsService>('EmailStopsService', [
      'getConsentImpact',
      'stopForAppointment',
    ]);
    service.getConsentImpact.and.callFake(() => {
      const s = new Subject<EmailStopConsentImpactDto>();
      pending.push(s);
      return s;
    });
    TestBed.configureTestingModule({
      providers: [
        { provide: EmailStopsService, useValue: service },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
      ],
    });
    fixture = TestBed.createComponent(EmailStopDialogComponent);
    fixture.componentRef.setInput('appointmentId', 'appt-1');
    fixture.componentRef.setInput('email', 'test.party@example.test');
    fixture.detectChanges();
  });

  afterEach(() => TestBed.resetTestingModule());

  it('keeps Stop emails disabled until the consent check answers', () => {
    expect(confirm().disabled).toBeTrue();
    pending[0].next(NO_IMPACT);
    fixture.detectChanges();
    expect(confirm().disabled).toBeFalse();
  });

  it('enables Stop emails after a failed check and leaves the warning empty', () => {
    pending[0].error(new Error('boom'));
    fixture.detectChanges();
    expect(confirm().disabled).toBeFalse();
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="consent-warning"]'),
    ).toBeNull();
  });

  it('disables the button again until the new scope has answered', () => {
    pending[0].next(NO_IMPACT);
    fixture.detectChanges();
    click('[data-testid="scope-all"]');
    expect(confirm().disabled).toBeTrue();
    // A late answer for the old scope must not enable it.
    pending[0].next(NO_IMPACT);
    fixture.detectChanges();
    expect(confirm().disabled).toBeTrue();
    pending[1].next(NO_IMPACT);
    fixture.detectChanges();
    expect(confirm().disabled).toBeFalse();
  });

  it('closes on Escape', () => {
    let closed = 0;
    fixture.componentInstance.closed.subscribe(() => closed++);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(closed).toBe(1);
  });

  it('stays open on Escape while the stop is saving, and closes again once it answers', () => {
    const saving = new Subject<EmailStopStatusDto>();
    service.stopForAppointment.and.returnValue(saving);
    pending[0].next(NO_IMPACT);
    fixture.detectChanges();
    let closed = 0;
    fixture.componentInstance.closed.subscribe(() => closed++);
    click('[data-testid="confirm-stop"]');
    expect(service.stopForAppointment).toHaveBeenCalledTimes(1);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(closed).toBe(0);

    saving.error(new Error('boom'));
    fixture.detectChanges();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(closed).toBe(1);
  });

  it('marks the box as a modal dialog', () => {
    const box = (fixture.nativeElement as HTMLElement).querySelector('.ra-modal');
    expect(box?.getAttribute('role')).toBe('dialog');
    expect(box?.getAttribute('aria-modal')).toBe('true');
  });
});
