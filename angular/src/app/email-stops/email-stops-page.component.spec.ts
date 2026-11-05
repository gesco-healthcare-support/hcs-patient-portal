import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ConfigStateService, LocalizationService } from '@abp/ng.core';
import { of } from 'rxjs';
import { EmailStopsPageComponent } from './email-stops-page.component';
import type { EmailStopConsentImpactDto, GlobalEmailStopDto } from './email-stops.models';
import { ChangeRequestSide } from './email-stops.models';
import { EmailStopsService } from './email-stops.service';

const row = (over: Partial<GlobalEmailStopDto> = {}): GlobalEmailStopDto => ({
  id: 'g1',
  email: 'test.party@example.test',
  stoppedByName: 'Pat Staff',
  stoppedAt: '2026-11-03T18:00:00Z',
  note: 'Left the firm',
  stoppedFromOfficeName: 'testoffice',
  ...over,
});

const NO_IMPACT: EmailStopConsentImpactDto = {
  hasImpact: false,
  thisCaseSides: [],
  otherCases: [],
};
const IMPACT: EmailStopConsentImpactDto = {
  hasImpact: true,
  thisCaseSides: [],
  otherCases: [
    { appointmentId: 'a2', requestConfirmationNumber: 'A00012', side: ChangeRequestSide.SideA },
  ],
};

describe('EmailStopsPageComponent', () => {
  let service: jasmine.SpyObj<EmailStopsService>;
  let fixture: ComponentFixture<EmailStopsPageComponent>;

  function setup(tenantId: string | null): void {
    TestBed.configureTestingModule({
      providers: [
        { provide: EmailStopsService, useValue: service },
        { provide: ConfigStateService, useValue: { getDeep: () => tenantId } },
        {
          provide: LocalizationService,
          useValue: { instant: (k: string, ...p: string[]) => [k, ...p].join('|') },
        },
      ],
    });
    fixture = TestBed.createComponent(EmailStopsPageComponent);
    fixture.detectChanges();
  }

  const el = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const q = (sel: string): HTMLElement | null => el().querySelector(sel);
  const click = (sel: string): void => {
    (q(sel) as HTMLElement).click();
    fixture.detectChanges();
  };
  const type = (sel: string, value: string): void => {
    const input = q(sel) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };

  beforeEach(() => {
    service = jasmine.createSpyObj<EmailStopsService>('EmailStopsService', [
      'getList',
      'create',
      'delete',
      'getGlobalConsentImpact',
    ]);
    service.getList.and.returnValue(of({ totalCount: 1, items: [row()] }));
    service.create.and.returnValue(of(row()));
    service.delete.and.returnValue(of(undefined));
    service.getGlobalConsentImpact.and.returnValue(of(NO_IMPACT));
  });

  afterEach(() => TestBed.resetTestingModule());

  it('lists the stopped addresses newest first with the practice column', () => {
    setup('office-1');
    const first = service.getList.calls.mostRecent().args[0];
    expect(first.sorting).toBe('stoppedAt desc');
    expect(first.skipCount).toBe(0);
    expect(first.maxResultCount).toBe(20);
    const rows = el().querySelectorAll('[data-testid="stop-row"]');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('test.party@example.test');
    expect(rows[0].textContent).toContain('testoffice');
    expect(rows[0].textContent).toContain('Left the firm');
  });

  it('shows "Staff site" when the stop came from the staff site', () => {
    service.getList.and.returnValue(
      of({ totalCount: 1, items: [row({ stoppedFromOfficeName: null })] }),
    );
    setup('office-1');
    expect(q('[data-testid="stop-row"]')?.textContent).toContain('::EmailStops:Page:StaffSite');
  });

  it('filters on the server and goes back to the first page', () => {
    setup('office-1');
    service.getList.calls.reset();
    type('[data-testid="filter"]', 'party');
    const last = service.getList.calls.mostRecent().args[0];
    expect(last.filter).toBe('party');
    expect(last.skipCount).toBe(0);
  });

  it('creates a stop straight away when no open request is affected', () => {
    setup('office-1');
    type('[data-testid="new-email"]', ' test.new@example.test ');
    click('[data-testid="add-stop"]');
    expect(service.getGlobalConsentImpact).toHaveBeenCalledWith('test.new@example.test');
    expect(service.create).toHaveBeenCalledWith({ email: 'test.new@example.test', note: null });
  });

  it('shows the warning first and only creates on a second click', () => {
    service.getGlobalConsentImpact.and.returnValue(of(IMPACT));
    setup('office-1');
    type('[data-testid="new-email"]', 'test.new@example.test');
    click('[data-testid="add-stop"]');
    expect(service.create).not.toHaveBeenCalled();
    expect(q('[data-testid="consent-warning"]')?.textContent).toContain('A00012');
    expect(q('[data-testid="add-stop"]')?.textContent).toContain('::EmailStops:Page:StopAnyway');
    click('[data-testid="add-stop"]');
    expect(service.create).toHaveBeenCalledTimes(1);
  });

  it('asks for confirmation, then turns an address back on', () => {
    setup('office-1');
    click('[data-testid="turn-on"]');
    expect(service.delete).not.toHaveBeenCalled();
    click('[data-testid="confirm-turn-on"]');
    expect(service.delete).toHaveBeenCalledWith('g1');
  });

  it('says it cannot check open requests on the staff site only', () => {
    setup(null);
    expect(q('[data-testid="staff-site-note"]')?.textContent).toContain(
      '::EmailStops:Page:CantCheck',
    );
  });

  it('does not show that note inside a practice', () => {
    setup('office-1');
    expect(q('[data-testid="staff-site-note"]')).toBeNull();
  });
});
