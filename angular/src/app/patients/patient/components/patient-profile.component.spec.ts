import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Observable, of } from 'rxjs';
import { ConfigStateService, RestService } from '@abp/ng.core';

import { PatientProfileComponent } from './patient-profile.component';

/**
 * Covers this component's two date-of-birth call sites after #620 moved the helpers into
 * `shared/date-of-birth.util.ts`.
 *
 * The helpers' own behaviour is pinned in `date-of-birth.util.spec.ts`, including the
 * zone-independent proof of the day shift. What is pinned HERE is only the WIRING, which that
 * spec cannot see: that the load path runs what the server sent through the normalizer, and
 * that `save()` puts the helper's output on the wire rather than the datepicker's
 * `{year, month, day}` object straight through.
 *
 * <p>Worth being precise about the limit: these assertions do NOT detect the time-zone bug
 * itself. Both the old and the new helper yield `1990-05-15` on a machine at or west of
 * Greenwich, so on CI and on any Gesco workstation this suite would pass either way. It fails
 * only if the call site stops going through the helper at all -- which is exactly what it is
 * for, since that is the failure the util spec cannot reach.</p>
 *
 * The component is created but never change-detected, so only the methods called here run.
 */
describe('PatientProfileComponent date-of-birth wiring (#620)', () => {
  interface Recorded {
    method: string;
    url: string;
    body?: Record<string, unknown>;
  }

  function create(patient: Record<string, unknown>) {
    const requests: Recorded[] = [];
    const rest = {
      request: (req: Recorded): Observable<unknown> => {
        requests.push(req);
        return req.method === 'GET' ? of({ patient }) : of({ ...patient, ...(req.body ?? {}) });
      },
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: RestService, useValue: rest },
        {
          provide: ConfigStateService,
          useValue: {
            // Only a Patient-role user reaches /patients/me; anyone else takes the
            // external-user branch and never touches either helper.
            getOne: (key: string) =>
              key === 'currentUser' ? { roles: ['Patient'], userName: 'p' } : { name: 'Office' },
            // The shell components pulled in by the standalone imports read the config
            // reactively; they are never rendered here but the injector still resolves them.
            getDeep: () => null,
            getDeep$: () => of(null),
            getOne$: () => of(null),
            getAll$: () => of({}),
          },
        },
        { provide: Router, useValue: { navigateByUrl: () => undefined } },
      ],
    });

    const fixture = TestBed.createComponent(PatientProfileComponent);
    return { fixture, component: fixture.componentInstance, requests };
  }

  /** Everything except dateOfBirth, which each test supplies. */
  function makeFormValid(component: PatientProfileComponent): void {
    component.form.patchValue({
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      genderId: 1,
      phoneNumberTypeId: 1,
      identityUserId: '11111111-1111-1111-1111-111111111111',
    });
  }

  afterEach(() => TestBed.resetTestingModule());

  it('keeps a served date of birth when it is a real one', () => {
    const c = create({ id: 'p1', dateOfBirth: '1990-05-15T00:00:00', concurrencyStamp: 's' });
    c.component.ngOnInit();
    expect(c.component.form.get('dateOfBirth')!.value).toBe('1990-05-15T00:00:00');
  });

  it('blanks a served date of birth the server could not have meant', () => {
    // Pre-1900 is the normalizer's "this is not a birth date" guard.
    const c = create({ id: 'p1', dateOfBirth: '1890-01-01T00:00:00', concurrencyStamp: 's' });
    c.component.ngOnInit();
    expect(c.component.form.get('dateOfBirth')!.value).toBeNull();
  });

  it('sends a plain yyyy-mm-dd date of birth on save, not the datepicker object', () => {
    const c = create({ id: 'p1', dateOfBirth: '1990-05-15T00:00:00', concurrencyStamp: 's' });
    c.component.ngOnInit();
    makeFormValid(c.component);
    // What the datepicker puts in the control once the patient edits the field.
    c.component.form.get('dateOfBirth')!.setValue({ year: 1990, month: 5, day: 15 } as never);

    c.component.save();

    const put = c.requests.find((r) => r.method === 'PUT');
    expect(put).withContext('save() should PUT /api/app/patients/me').toBeDefined();
    // The whole point of #620: the old helper round-tripped through Date and toISOString,
    // so this was '1990-05-14' for any browser east of Greenwich.
    expect(put!.body!['dateOfBirth']).toBe('1990-05-15');
  });

  it('pads a single-digit month and day rather than emitting 1990-5-1', () => {
    const c = create({ id: 'p1', dateOfBirth: '1990-05-15T00:00:00', concurrencyStamp: 's' });
    c.component.ngOnInit();
    makeFormValid(c.component);
    c.component.form.get('dateOfBirth')!.setValue({ year: 1990, month: 5, day: 1 } as never);

    c.component.save();

    expect(c.requests.find((r) => r.method === 'PUT')!.body!['dateOfBirth']).toBe('1990-05-01');
  });

  it('does not PUT at all when the form is incomplete', () => {
    const c = create({ id: 'p1', dateOfBirth: '1990-05-15T00:00:00', concurrencyStamp: 's' });
    c.component.ngOnInit();
    // No makeFormValid: the required controls are still empty.
    c.component.save();
    expect(c.requests.some((r) => r.method === 'PUT')).toBeFalse();
  });
});
