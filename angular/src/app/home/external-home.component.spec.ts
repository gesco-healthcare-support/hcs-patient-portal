import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { ConfigStateService, ListService, RestService } from '@abp/ng.core';

import { ExternalHomeComponent } from './external-home.component';
import { AppointmentService } from '../proxy/appointments/appointment.service';

/**
 * Covers the two filter-option getters. They were `.sort()` with no comparator
 * until sweep #660 (Sonar typescript:S2871), which compares UTF-16 code units
 * rather than collating -- so an accented location sorted after every ASCII
 * name and casing decided order. These specs pin the collated order, because
 * the bug is invisible to a test that only uses lowercase ASCII.
 *
 * The component is created but never change-detected, so `ngOnInit` (which
 * loads appointments over HTTP) does not run. Only the pure getters are
 * exercised, driven by the writable `rows` signal.
 */
describe('ExternalHomeComponent filter options (sweep #660)', () => {
  /** Minimal row: the getters read only `type` and `location`. */
  const row = (type: string, location: string) => ({ type, location }) as never;

  function optionsFor(rows: ReturnType<typeof row>[]) {
    TestBed.configureTestingModule({
      providers: [
        { provide: ConfigStateService, useValue: { getOne: () => null, getAll: () => ({}) } },
        { provide: RestService, useValue: { request: () => undefined } },
        {
          provide: Router,
          useValue: { navigate: () => undefined, navigateByUrl: () => undefined },
        },
        { provide: AppointmentService, useValue: {} },
        { provide: ListService, useValue: { hookToQuery: () => undefined } },
      ],
    });

    const fixture = TestBed.createComponent(ExternalHomeComponent);
    const component = fixture.componentInstance as unknown as {
      rows: { set(value: unknown[]): void };
      typeOptions: string[];
      locationOptions: string[];
    };
    component.rows.set(rows);
    return { type: component.typeOptions, location: component.locationOptions };
  }

  afterEach(() => TestBed.resetTestingModule());

  it('sorts by collation, not by UTF-16 code unit', () => {
    // 'Ontario' vs 'Ånstrom': a bare .sort() puts every ASCII name before any
    // accented one, because 'Å' (U+00C5) is above 'Z' (U+005A) in code units.
    const { location } = optionsFor([
      row('PQME', 'Ontario'),
      row('PQME', 'Ånstrom'),
      row('PQME', 'Bakersfield'),
    ]);
    expect(location).toEqual(['Ånstrom', 'Bakersfield', 'Ontario']);
  });

  it('does not let casing decide order', () => {
    // A bare .sort() yields ['AME', 'PQME', 'ame'] -- every uppercase first.
    const { type } = optionsFor([row('PQME', 'X'), row('ame', 'X'), row('AME', 'X')]);
    expect(type.map((t) => t.toLowerCase())).toEqual(['ame', 'ame', 'pqme']);
  });

  it('de-duplicates and drops blanks and the em-dash placeholder', () => {
    const { type, location } = optionsFor([
      row('PQME', 'Ontario'),
      row('PQME', 'Ontario'),
      row('—', '—'),
      row('', ''),
    ]);
    expect(type).toEqual(['PQME']);
    expect(location).toEqual(['Ontario']);
  });

  it('returns an empty list when there are no rows', () => {
    const { type, location } = optionsFor([]);
    expect(type).toEqual([]);
    expect(location).toEqual([]);
  });
});
