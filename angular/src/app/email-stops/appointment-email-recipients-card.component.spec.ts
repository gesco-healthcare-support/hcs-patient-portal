import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LocalizationService, PermissionService, QUEUE_MANAGER } from '@abp/ng.core';
import { of } from 'rxjs';
import { AppointmentEmailRecipientsCardComponent } from './appointment-email-recipients-card.component';
import { ChangeRequestSide } from '../proxy/appointment-change-requests/change-request-side.enum';
import { EmailRecipientKind, EmailRoutingNote, EmailStopScope } from '../proxy/email-stops';
import type {
  AppointmentEmailRecipientDto,
  EmailStopConsentImpactDto,
  EmailStopStatusDto,
} from '../proxy/email-stops';
import { EmailStopsService } from '../proxy/email-stops';

const stop = (scope: EmailStopScope, id = 'stop-1'): EmailStopStatusDto => ({
  id,
  scope,
  stoppedByName: 'Pat Staff',
  stoppedAt: '2026-11-03T18:00:00Z',
  note: 'Asked us to',
  stoppedFromOfficeName: null,
});

const person = (over: Partial<AppointmentEmailRecipientDto>): AppointmentEmailRecipientDto => ({
  email: 'test.attorney@example.test',
  name: 'Test Attorney',
  kinds: [EmailRecipientKind.ApplicantAttorney],
  routingNote: EmailRoutingNote.None,
  caseStop: null,
  allCasesStop: null,
  ...over,
});

const NO_IMPACT: EmailStopConsentImpactDto = {
  hasImpact: false,
  thisCaseSides: [],
  otherCases: [],
};

describe('AppointmentEmailRecipientsCardComponent', () => {
  let service: jasmine.SpyObj<EmailStopsService>;
  let fixture: ComponentFixture<AppointmentEmailRecipientsCardComponent>;
  let granted: boolean;

  function setup(items: AppointmentEmailRecipientDto[]): void {
    service.getAppointmentRecipients.and.returnValue(of({ items }));
    fixture = TestBed.createComponent(AppointmentEmailRecipientsCardComponent);
    fixture.componentRef.setInput('appointmentId', 'appt-1');
    fixture.detectChanges();
    fixture.detectChanges();
  }

  const el = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const q = (sel: string): HTMLElement | null => el().querySelector(sel);
  const click = (sel: string): void => {
    (q(sel) as HTMLElement).click();
    fixture.detectChanges();
  };

  beforeEach(() => {
    granted = true;
    service = jasmine.createSpyObj<EmailStopsService>('EmailStopsService', [
      'getAppointmentRecipients',
      'stopForAppointment',
      'resumeForAppointment',
      'getConsentImpact',
      'delete',
    ]);
    service.getConsentImpact.and.returnValue(of(NO_IMPACT));
    service.resumeForAppointment.and.returnValue(of(undefined));
    service.delete.and.returnValue(of(undefined));
    service.stopForAppointment.and.returnValue(of(stop(EmailStopScope.Case)));
    TestBed.configureTestingModule({
      providers: [
        { provide: EmailStopsService, useValue: service },
        // abpPermission queues a change-detection pass; run it inline in tests.
        { provide: QUEUE_MANAGER, useValue: { add: (fn: () => void) => fn() } },
        {
          provide: PermissionService,
          useValue: {
            getGrantedPolicy: () => granted,
            getGrantedPolicy$: () => of(granted),
          },
        },
        {
          provide: LocalizationService,
          useValue: { instant: (k: string, ...p: string[]) => [k, ...p].join('|') },
        },
      ],
    });
  });

  afterEach(() => TestBed.resetTestingModule());

  it('shows the hint and one row per person with plain kind labels', () => {
    setup([
      person({
        kinds: [EmailRecipientKind.Booker, EmailRecipientKind.ApplicantParalegal],
      }),
    ]);
    expect(el().textContent).toContain('::EmailStops:Card:Hint');
    expect(el().textContent).toContain('::EmailStops:Card:HintPassword');
    expect(el().querySelectorAll('[data-testid="recipient-row"]').length).toBe(1);
    expect(el().textContent).toContain('::EmailStops:Kind:Booker');
    expect(el().textContent).toContain('::EmailStops:Kind:ApplicantParalegal');
  });

  it('shows the green pill and a Stop button for a person getting emails', () => {
    setup([person({})]);
    expect(q('[data-testid="pill"]')?.textContent).toContain('::EmailStops:Pill:Active');
    expect(q('[data-testid="stop-btn"]')).not.toBeNull();
    expect(q('[data-testid="resume-btn"]')).toBeNull();
  });

  it('shows the case pill with who stopped it and the note', () => {
    setup([person({ caseStop: stop(EmailStopScope.Case) })]);
    expect(q('[data-testid="pill"]')?.textContent).toContain('::EmailStops:Pill:Case');
    expect(q('[data-testid="stopped-by"]')?.textContent).toContain('Pat Staff');
    expect(q('[data-testid="stopped-by"]')?.textContent).toContain('Asked us to');
    expect(q('[data-testid="resume-btn"]')).not.toBeNull();
  });

  it('shows the all-cases pill even when a case stop also exists', () => {
    setup([
      person({
        caseStop: stop(EmailStopScope.Case),
        allCasesStop: stop(EmailStopScope.AllCases, 'global-1'),
      }),
    ]);
    expect(q('[data-testid="pill"]')?.textContent).toContain('::EmailStops:Pill:AllCases');
  });

  it('shows a routing note only when there is one', () => {
    setup([
      person({ routingNote: EmailRoutingNote.SendsToParalegalInstead }),
      person({ email: 'second@example.test', routingNote: EmailRoutingNote.None }),
    ]);
    const notes = el().querySelectorAll('[data-testid="routing-note"]');
    expect(notes.length).toBe(1);
    expect(notes[0].textContent).toContain('::EmailStops:Note:SendsToParalegalInstead');
  });

  it('emits the loaded rows for the parent page', () => {
    service.getAppointmentRecipients.and.returnValue(of({ items: [person({})] }));
    fixture = TestBed.createComponent(AppointmentEmailRecipientsCardComponent);
    const emitted: AppointmentEmailRecipientDto[][] = [];
    fixture.componentInstance.recipientsChange.subscribe((r) => emitted.push(r));
    fixture.componentRef.setInput('appointmentId', 'appt-1');
    fixture.detectChanges();
    expect(emitted.length).toBe(1);
    expect(emitted[0][0].email).toBe('test.attorney@example.test');
  });

  it('renders nothing and loads nothing without the permission', () => {
    granted = false;
    setup([person({})]);
    expect(q('[data-testid="email-recipients-card"]')).toBeNull();
    expect(service.getAppointmentRecipients).not.toHaveBeenCalled();
  });

  it('stop dialog: checks consent impact again when the scope changes and shows the warning', () => {
    setup([person({})]);
    click('[data-testid="stop-btn"]');
    expect(service.getConsentImpact).toHaveBeenCalledWith(
      'appt-1',
      'test.attorney@example.test',
      EmailStopScope.Case,
    );
    service.getConsentImpact.and.returnValue(
      of({
        hasImpact: true,
        thisCaseSides: [ChangeRequestSide.SideA],
        otherCases: [
          {
            appointmentId: 'a2',
            requestConfirmationNumber: 'A00012',
            side: ChangeRequestSide.SideB,
          },
        ],
      }),
    );
    click('[data-testid="scope-all"]');
    expect(service.getConsentImpact).toHaveBeenCalledWith(
      'appt-1',
      'test.attorney@example.test',
      EmailStopScope.AllCases,
    );
    const warning = document.querySelector('[data-testid="consent-warning"]');
    expect(warning?.textContent).toContain('::EmailStops:Warn:ThisCase');
    expect(warning?.textContent).toContain('::EmailStops:Side:Applicant');
    expect(warning?.textContent).toContain('A00012');
  });

  it('stop dialog: does not show a warning when there is no impact', () => {
    setup([person({})]);
    click('[data-testid="stop-btn"]');
    expect(document.querySelector('[data-testid="consent-warning"]')).toBeNull();
  });

  it('stop dialog: saves with the chosen scope and reloads the list', () => {
    setup([person({})]);
    click('[data-testid="stop-btn"]');
    click('[data-testid="scope-all"]');
    service.getAppointmentRecipients.calls.reset();
    click('[data-testid="confirm-stop"]');
    expect(service.stopForAppointment).toHaveBeenCalledWith('appt-1', {
      email: 'test.attorney@example.test',
      scope: EmailStopScope.AllCases,
      note: null,
    });
    expect(service.getAppointmentRecipients).toHaveBeenCalledTimes(1);
    expect(document.querySelector('app-email-stop-dialog')).toBeNull();
  });

  it('turns a case stop back on without asking', () => {
    setup([person({ caseStop: stop(EmailStopScope.Case) })]);
    click('[data-testid="resume-btn"]');
    expect(service.resumeForAppointment).toHaveBeenCalledWith(
      'appt-1',
      'test.attorney@example.test',
    );
    expect(service.delete).not.toHaveBeenCalled();
  });

  it('asks first before turning an all-cases stop back on, then deletes it', () => {
    setup([person({ allCasesStop: stop(EmailStopScope.AllCases, 'global-1') })]);
    click('[data-testid="resume-btn"]');
    expect(q('[data-testid="confirm-all-text"]')?.textContent).toContain(
      '::EmailStops:Confirm:AllCases',
    );
    expect(service.delete).not.toHaveBeenCalled();
    click('[data-testid="confirm-resume"]');
    expect(service.delete).toHaveBeenCalledWith('global-1');
    expect(service.resumeForAppointment).not.toHaveBeenCalled();
  });

  it('removes both stops when a person has a case stop and an all-cases stop', () => {
    setup([
      person({
        caseStop: stop(EmailStopScope.Case),
        allCasesStop: stop(EmailStopScope.AllCases, 'global-1'),
      }),
    ]);
    click('[data-testid="resume-btn"]');
    click('[data-testid="confirm-resume"]');
    expect(service.resumeForAppointment).toHaveBeenCalled();
    expect(service.delete).toHaveBeenCalledWith('global-1');
  });
});
