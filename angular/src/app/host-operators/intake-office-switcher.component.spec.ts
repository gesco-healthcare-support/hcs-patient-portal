import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';
import { ImpersonationService } from '@volo/abp.commercial.ng.ui/config';

import { IntakeOfficeSwitcherComponent } from './intake-office-switcher.component';
import { IntakeAssignmentsService } from '../proxy/host-operators/intake-assignments.service';

/**
 * The intake operator's office picker: one card per assigned office, with an "Enter practice"
 * button that impersonates into it.
 *
 * <p>It had no spec. It is built in an injection context and never rendered.</p>
 *
 * <p>All office names and identifiers below are synthetic.</p>
 */
describe('IntakeOfficeSwitcherComponent', () => {
  let getMyOfficeMetrics: jasmine.Spy;
  let impersonateTenant: jasmine.Spy;
  let info: jasmine.Spy;

  interface Probe {
    [key: string]: any;
  }

  const office = { officeId: 'o-1', officeName: 'Example Practice', pendingRequests: 2 };

  function create(metrics: unknown = of({ items: [office] })): Probe {
    getMyOfficeMetrics = jasmine.createSpy('getMyOfficeMetrics').and.returnValue(metrics);
    impersonateTenant = jasmine.createSpy('impersonateTenant').and.returnValue(of({}));
    info = jasmine.createSpy('info');
    TestBed.configureTestingModule({
      providers: [
        { provide: IntakeAssignmentsService, useValue: { getMyOfficeMetrics } },
        { provide: ImpersonationService, useValue: { impersonateTenant } },
        { provide: ToasterService, useValue: { info } },
      ],
    });
    return TestBed.runInInjectionContext(
      () => new IntakeOfficeSwitcherComponent(),
    ) as unknown as Probe;
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('loading the offices', () => {
    it('lists the assigned offices and stops loading', () => {
      const c = create();
      expect(c.metrics()).toEqual([office]);
      expect(c.loading()).toBeFalse();
    });

    it('lists none when the response has no rows', () => {
      expect(create(of({})).metrics()).toEqual([]);
    });

    it('lists none, and stops loading, when the load fails', () => {
      const c = create(throwError(() => ({ status: 500 })));
      expect(c.metrics()).toEqual([]);
      expect(c.loading()).toBeFalse();
    });
  });

  describe('entering a practice', () => {
    it("impersonates into the office as the operator's own shadow user", () => {
      const c = create();

      c.switchInto(office);

      // An empty user name makes the custom grant use the operator's own shadow user.
      expect(impersonateTenant).toHaveBeenCalledWith('o-1', '');
      expect(info).toHaveBeenCalledWith('Switching into Example Practice...');
      expect(c.busy()).withContext('released once the call settles').toBeFalse();
    });

    it('names a generic practice when the office has no name', () => {
      const c = create();
      c.switchInto({ officeId: 'o-2' });
      expect(info).toHaveBeenCalledWith('Switching into practice...');
    });

    it('does nothing for an office with no id, or while a switch is running', () => {
      const c = create();

      c.switchInto({ officeName: 'No id' });
      c.busy.set(true);
      c.switchInto(office);

      expect(impersonateTenant).not.toHaveBeenCalled();
    });

    it('releases the button when the switch fails', () => {
      const c = create();
      impersonateTenant.and.returnValue(throwError(() => ({ status: 400 })));

      c.switchInto(office);

      expect(c.busy()).toBeFalse();
    });
  });
});
