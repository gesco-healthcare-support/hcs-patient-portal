import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { PatientDetailComponent } from './patient-detail.component';
import { PeopleSectionGateway } from './people-section.gateway';
import type { PersonRow } from './people.util';
import { AppointmentStatusType } from '../proxy/enums/appointment-status-type.enum';

/**
 * The patient detail view: the identity header, the demographic labels, and the patient's own
 * appointments.
 *
 * <p>It had no spec. It is built in an injection context and never rendered, so each member is
 * called directly.</p>
 *
 * <p>All names and identifiers below are synthetic.</p>
 */
describe('PatientDetailComponent', () => {
  let appointmentsForPatient: jasmine.Spy;

  interface Probe {
    [key: string]: any;
  }

  const person = (over: Partial<PersonRow> = {}) =>
    ({ id: 'p-1', firstName: 'Ada', lastName: 'Example', ...over }) as PersonRow;

  function create(): Probe {
    appointmentsForPatient = jasmine.createSpy('appointmentsForPatient').and.returnValue(of([]));
    TestBed.configureTestingModule({
      providers: [{ provide: PeopleSectionGateway, useValue: { appointmentsForPatient } }],
    });
    return TestBed.runInInjectionContext(() => new PatientDetailComponent()) as unknown as Probe;
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('receiving a patient', () => {
    it("loads that patient's appointments and clears the spinner", () => {
      const c = create();
      const rows = [{ appointment: { id: 'a-1' } }];
      appointmentsForPatient.and.returnValue(of(rows));

      c.patient = person();

      expect(appointmentsForPatient).toHaveBeenCalledWith('p-1');
      expect(c.row().id).toBe('p-1');
      expect(c.appointments()).toBe(rows);
      expect(c.loadingAppts()).toBeFalse();
    });

    it('shows no appointments, and no spinner, when the load fails', () => {
      const c = create();
      appointmentsForPatient.and.returnValue(throwError(() => ({ status: 500 })));

      c.patient = person();

      expect(c.appointments()).toEqual([]);
      expect(c.loadingAppts()).toBeFalse();
    });
  });

  it('joins the name parts, skipping a missing middle name', () => {
    const c = create();
    c.patient = person({ middleName: 'Byron' } as Partial<PersonRow>);
    expect(c.fullName).toBe('Ada Byron Example');

    c.patient = person();
    expect(c.fullName).toBe('Ada Example');
  });

  it('pills an appointment by its status, and as pending when it has none', () => {
    const c = create();
    expect(c.pill({ appointment: { appointmentStatus: AppointmentStatusType.Approved } })).toBe(
      'Approved',
    );
    expect(c.pill({ appointment: {} })).toBe('Pending');
  });

  it('names the gender and phone type, and nothing for an unknown value', () => {
    const c = create();
    expect(c.genderLabel(2)).toBe('Female');
    expect(c.genderLabel(null)).toBe('');
    expect(c.phoneTypeLabel(29)).toBe('Home');
    expect(c.phoneTypeLabel(99)).toBe('');
  });

  it('starts loading, with no appointments, before a patient arrives', () => {
    const c = create();
    expect(c.loadingAppts()).toBeTrue();
    expect(c.appointments()).toEqual([]);
    expect(c.canInvite).toBeFalse();
    expect(c.canEdit).toBeFalse();
  });
});
