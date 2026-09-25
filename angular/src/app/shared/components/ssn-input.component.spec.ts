import { TestBed } from '@angular/core/testing';
import { ConfigStateService } from '@abp/ng.core';

import { PatientService } from '../../proxy/patients/patient.service';
import { SsnInputComponent } from './ssn-input.component';

/**
 * Sweep #640 touched the on-file redaction twice: `String.fromCharCode(0x2022)` became
 * `fromCodePoint` (S7758, identical for a BMP code point) and the `*` substitution became
 * `replaceAll` (S7781).
 *
 * <p>The component had no spec. It is the last thing between a stored SSN and the screen, so
 * "the redaction still redacts" is worth asserting rather than assuming.</p>
 */
describe('SsnInputComponent on-file redaction (sweep #640)', () => {
  const DOT = String.fromCodePoint(0x2022);

  function create() {
    TestBed.configureTestingModule({
      providers: [
        { provide: PatientService, useValue: {} },
        { provide: ConfigStateService, useValue: { getOne: () => null } },
      ],
    });
    return TestBed.createComponent(SsnInputComponent).componentInstance;
  }

  afterEach(() => TestBed.resetTestingModule());

  it('renders the masked value with bullets in place of the asterisks', () => {
    const c = create();
    c.currentMaskedSsn = '***-**-6789';
    expect(c.onFileDisplay()).toBe(`${DOT}${DOT}${DOT}-${DOT}${DOT}-6789`);
  });

  it('replaces every asterisk, not only the first', () => {
    // The point of replaceAll over replace(/\*/g): a single-arg replace would have left
    // four of the five stars on screen.
    const c = create();
    c.currentMaskedSsn = '***-**-6789';
    expect(c.onFileDisplay()).not.toContain('*');
  });

  it('never reveals the redacted digits', () => {
    const c = create();
    c.currentMaskedSsn = '***-**-6789';
    const out = c.onFileDisplay();
    expect(out).toContain('6789');
    expect(out.replace(/[0-9-]/g, '')).toBe(DOT.repeat(5));
  });

  it('is empty when there is no masked value', () => {
    const c = create();
    c.currentMaskedSsn = null;
    expect(c.onFileDisplay()).toBe('');
  });
});
