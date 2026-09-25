import { TestBed } from '@angular/core/testing';
import { RestService } from '@abp/ng.core';
import { of, throwError } from 'rxjs';
import {
  CaseTrackerMissingIntakesComponent,
  MissingIntakeReport,
} from './case-tracker-missing-intakes.component';

/**
 * The missing-intake panel (#944). What matters: it asks nothing until the operator presses Check, it asks the
 * one read-only endpoint, a failed check shows NO list (a stale one would read as current), and there is no
 * control on it that could queue or send. Data is synthetic.
 */
describe('CaseTrackerMissingIntakesComponent', () => {
  interface Signal<T> {
    (): T;
  }
  interface Probe {
    report: Signal<MissingIntakeReport | null>;
    loading: Signal<boolean>;
    error: Signal<string | null>;
    likelyLostTotal: Signal<number>;
    check(): Promise<void>;
    toggleBefore(officeId: string): void;
    isExpanded(officeId: string): boolean;
    statusLabel(status: number): string;
  }

  let request: jasmine.Spy;

  const report: MissingIntakeReport = {
    generatedAt: '2026-09-28T15:00:00Z',
    offices: [
      {
        officeId: 'office-1',
        officeName: 'Example Downtown Office',
        failed: false,
        firstIntakeRowAt: '2026-07-30T18:00:00Z',
        likelyLost: [
          {
            appointmentId: 'ada5e3c5-0034-ebde-253c-3a2293631dee',
            confirmationNumber: 'A00104',
            status: 2,
            approvedAt: '2026-08-04T18:00:00Z',
          },
        ],
        settling: [],
        beforeIntegration: [
          {
            appointmentId: '3c9d1b77-2e40-4a51-8bb2-77f0a1c9d233',
            confirmationNumber: 'A00001',
            status: 2,
            approvedAt: '2026-06-01T18:00:00Z',
          },
        ],
        beforeIntegrationCount: 250,
      },
      {
        officeId: 'office-2',
        officeName: 'Example Uptown Office',
        failed: true,
        firstIntakeRowAt: null,
        likelyLost: [],
        settling: [],
        beforeIntegration: [],
        beforeIntegrationCount: 0,
      },
    ],
  };

  function settle(): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, 0));
  }

  beforeEach(() => {
    request = jasmine.createSpy('request').and.returnValue(of(report));
    TestBed.configureTestingModule({
      imports: [CaseTrackerMissingIntakesComponent],
      providers: [{ provide: RestService, useValue: { request } }],
    });
  });

  function create() {
    const fixture = TestBed.createComponent(CaseTrackerMissingIntakesComponent);
    return { fixture, probe: fixture.componentInstance as unknown as Probe };
  }

  it('asks nothing until Check is pressed, because the check reads every office', async () => {
    const { fixture } = create();
    fixture.detectChanges();
    await settle();
    expect(request).not.toHaveBeenCalled();
  });

  it('asks the read-only missing-intakes endpoint once per check', async () => {
    const { probe } = create();
    await probe.check();
    expect(request).toHaveBeenCalledTimes(1);
    expect(request.calls.mostRecent().args[0]).toEqual({
      method: 'GET',
      url: '/api/app/case-tracker/missing-intakes',
    });
    expect(probe.report()).toEqual(report);
    expect(probe.likelyLostTotal()).toBe(1);
  });

  it('shows no list at all when the check fails', async () => {
    const { probe } = create();
    await probe.check();
    request.and.returnValue(throwError(() => new Error('synthetic failure')));

    await probe.check();

    expect(probe.report()).toBeNull();
    expect(probe.error()).toBe('The check could not be run. Please try again.');
    expect(probe.loading()).toBeFalse();
  });

  it('keeps old history collapsed until asked, per office', async () => {
    const { probe } = create();
    await probe.check();
    expect(probe.isExpanded('office-1')).toBeFalse();
    probe.toggleBefore('office-1');
    expect(probe.isExpanded('office-1')).toBeTrue();
    expect(probe.isExpanded('office-2')).toBeFalse();
    probe.toggleBefore('office-1');
    expect(probe.isExpanded('office-1')).toBeFalse();
  });

  it('names a status, and falls back to the number for one it does not know', () => {
    const { probe } = create();
    expect(probe.statusLabel(2)).toBe('Approved');
    expect(probe.statusLabel(99)).toBe('99');
  });

  it('renders the likely-lost row, the unreadable office, and no control but Check and the history toggle', async () => {
    const { fixture, probe } = create();
    await probe.check();
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    expect(el.textContent).toContain('A00104');
    expect(el.textContent).toContain('This office could not be read. It was not checked.');
    expect(el.textContent).not.toContain('A00001');
    const buttons = Array.from(el.querySelectorAll('button')).map(
      (b) => b.textContent?.trim() ?? '',
    );
    expect(buttons.length).toBe(2);
    expect(buttons[0]).toContain('Check for missing intakes');
    expect(buttons[1]).toContain("Before this office's integration: 250");
  });
});
