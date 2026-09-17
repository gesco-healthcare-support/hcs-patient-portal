import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ConfigStateService } from '@abp/ng.core';
import { of, throwError } from 'rxjs';

import { PatientService } from '../../proxy/patients/patient.service';
import { SsnInputComponent } from './ssn-input.component';

/**
 * The SSN entry and reveal control -- the single UI surface for a social-security number on
 * every form in this product, and the last thing between a stored SSN and the screen.
 *
 * <p>It sat at 80 of 117 lines uncovered. An existing spec (sweep #640) covers `onFileDisplay`
 * and nothing else, so none of that is repeated here.</p>
 *
 * <p>What this pins is the part that matters: the three rules the component's own header states,
 * each of which fails silently if broken.</p>
 *
 * <ol>
 *   <li><b>Never pre-filled.</b> An untouched box emits null, and the backend reads null as
 *       "leave the stored SSN unchanged". If the box emitted an empty string, or echoed the
 *       on-file mask back as typed input, a user who opened a form and saved without touching
 *       the field would overwrite the real number.</li>
 *   <li><b>Redaction never leaks.</b> The mask shows at most the last four digits, and fewer
 *       than four digits produce dots only -- never a partial number.</li>
 *   <li><b>Reveal is gated.</b> `canReveal()` mirrors the server's SsnRevealAccess predicate:
 *       an internal role, or the record's own owner. Nobody else gets the button.</li>
 * </ol>
 *
 * <p>TEST DATA. Every digit string here uses area 000, which has never been issued, so none of
 * them can collide with a real social-security number while still exercising the nine-digit
 * paths. Grouped renderings are ASSEMBLED by {@link grouped} rather than written as literals,
 * so no SSN-shaped string appears in this source at all.</p>
 */
describe('SsnInputComponent surfaces', () => {
  let getFullSsn: jasmine.Spy;
  let user: { id?: string; roles?: string[] } | null;

  /** Structurally impossible SSNs: area 000 is never issued. */
  const NINE = '000000000';
  const NINE_ALT = '000006789';

  const DOT = String.fromCodePoint(0x2022);

  /** The grouped rendering of a digit string, assembled so no SSN-shaped literal is written. */
  function grouped(digits: string): string {
    if (digits.length <= 3) {
      return digits;
    }
    if (digits.length <= 5) {
      return [digits.slice(0, 3), digits.slice(3)].join('-');
    }
    return [digits.slice(0, 3), digits.slice(3, 5), digits.slice(5, 9)].join('-');
  }

  /** The redacted rendering: three dots, two dots, then the last four digits. */
  function redacted(last4: string): string {
    return [DOT.repeat(3), DOT.repeat(2), last4].join('-');
  }

  /** The masked form the DTO carries, built from asterisks the component swaps for dots. */
  function maskedDto(last4: string): string {
    return ['*'.repeat(3), '*'.repeat(2), last4].join('-');
  }

  function create(): SsnInputComponent {
    user = null;
    getFullSsn = jasmine
      .createSpy('getFullSsn')
      .and.returnValue(of({ socialSecurityNumber: NINE_ALT }));

    TestBed.configureTestingModule({
      providers: [
        { provide: PatientService, useValue: { getFullSsn } },
        {
          provide: ConfigStateService,
          useValue: { getOne: (k: string) => (k === 'currentUser' ? user : null) },
        },
      ],
    });
    return TestBed.createComponent(SsnInputComponent).componentInstance;
  }

  /** A stand-in for the rendered input, which the handlers read and write directly. */
  function el(value = ''): HTMLInputElement {
    const input = document.createElement('input');
    input.value = value;
    return input;
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('who may reveal a stored SSN', () => {
    it('refuses when there is no signed-in user', () => {
      const c = create();
      user = null;
      expect(c.canReveal()).toBeFalse();
    });

    ['admin', 'intake staff', 'staff supervisor', 'it admin', 'doctor'].forEach((role) => {
      it(`allows the internal role "${role}"`, () => {
        const c = create();
        user = { id: 'u-1', roles: [role] };
        expect(c.canReveal()).toBeTrue();
      });
    });

    it('matches an internal role regardless of casing or padding', () => {
      // Roles arrive from ABP's config state as stored; a casing difference must not
      // silently withdraw access from someone who has it.
      const c = create();
      user = { id: 'u-1', roles: ['  IT Admin  '] };
      expect(c.canReveal()).toBeTrue();
    });

    it('allows an external user who owns the record', () => {
      const c = create();
      c.patientIdentityUserId = 'u-1';
      user = { id: 'u-1', roles: ['Patient'] };
      expect(c.canReveal()).toBeTrue();
    });

    it('REFUSES an external user who does not own the record', () => {
      /**
       * The case this gate exists for: one patient must never reveal another patient's
       * stored number. The server re-checks and returns 403, so this is the first of two
       * gates rather than the only one -- but it is the one that decides what the UI offers.
       */
      const c = create();
      c.patientIdentityUserId = 'someone-else';
      user = { id: 'u-1', roles: ['Patient'] };
      expect(c.canReveal()).toBeFalse();
    });

    it('refuses when the record has no owner to compare against', () => {
      const c = create();
      c.patientIdentityUserId = null;
      user = { id: 'u-1', roles: ['Patient'] };
      expect(c.canReveal()).toBeFalse();
    });

    it('refuses when the user carries no id', () => {
      const c = create();
      c.patientIdentityUserId = 'u-1';
      user = { roles: ['Patient'] };
      expect(c.canReveal()).toBeFalse();
    });

    it('refuses a user with no roles at all', () => {
      const c = create();
      user = { id: 'u-1' };
      expect(c.canReveal()).toBeFalse();
    });
  });

  describe('the reveal button', () => {
    function onFile(c: SsnInputComponent): void {
      c.patientId = 'p-1';
      c.currentMaskedSsn = maskedDto('6789');
    }

    it('is offered only when there is something on file to reveal', () => {
      const c = create();
      user = { id: 'u-1', roles: ['admin'] };
      expect(c.showOnFile()).withContext('no patient, no masked value').toBeFalse();
      expect(c.eyeDisabled()).toBeTrue();

      onFile(c);
      expect(c.showOnFile()).toBeTrue();
      expect(c.eyeDisabled()).toBeFalse();
    });

    it('needs BOTH a patient id and a masked value', () => {
      const c = create();
      c.patientId = 'p-1';
      expect(c.showOnFile()).toBeFalse();

      c.patientId = null;
      c.currentMaskedSsn = maskedDto('6789');
      expect(c.showOnFile()).toBeFalse();
    });

    it('stays disabled for a user who may not reveal', () => {
      const c = create();
      onFile(c);
      user = { id: 'u-1', roles: ['Patient'] };
      c.patientIdentityUserId = 'someone-else';
      expect(c.eyeDisabled()).toBeTrue();
    });

    it('stays disabled while a reveal is already in flight', () => {
      const c = create();
      onFile(c);
      user = { id: 'u-1', roles: ['admin'] };
      c.onFileLoading.set(true);
      expect(c.eyeDisabled()).toBeTrue();
    });

    it('follows the field disabled state once the user has typed', () => {
      // Revealing what you just typed is a different act from revealing what is stored:
      // it needs no permission, but it does follow a read-only form.
      const c = create();
      c.entryDigits.set('0000');
      c.setDisabledState(true);
      expect(c.eyeDisabled()).toBeTrue();

      c.setDisabledState(false);
      expect(c.eyeDisabled()).toBeFalse();
    });

    it('reports whether what is shown is currently redacted', () => {
      const c = create();
      c.entryDigits.set('0000');
      c.entryHidden.set(true);
      expect(c.eyeClosed()).toBeTrue();
      c.entryHidden.set(false);
      expect(c.eyeClosed()).toBeFalse();

      c.entryDigits.set('');
      c.onFileRevealed.set(false);
      expect(c.eyeClosed()).toBeTrue();
      c.onFileRevealed.set(true);
      expect(c.eyeClosed()).toBeFalse();
    });

    it('labels itself for what it will actually do', () => {
      const c = create();
      c.entryDigits.set('0000');
      c.entryHidden.set(true);
      expect(c.eyeAriaLabel()).toBe('Show entered SSN');
      c.entryHidden.set(false);
      expect(c.eyeAriaLabel()).toBe('Hide entered SSN');

      c.entryDigits.set('');
      c.onFileRevealed.set(false);
      expect(c.eyeAriaLabel()).toBe('Reveal SSN on file');
      c.onFileRevealed.set(true);
      expect(c.eyeAriaLabel()).toBe('Hide SSN on file');
    });

    it('reveals the typed value when there is one, and the stored one otherwise', () => {
      const c = create();
      onFile(c);
      user = { id: 'u-1', roles: ['admin'] };

      c.entryDigits.set('0000');
      c.onEyeClick(el());
      expect(getFullSsn)
        .withContext('a typed value never triggers the audited fetch')
        .not.toHaveBeenCalled();

      c.entryDigits.set('');
      c.onEyeClick(el());
      expect(getFullSsn).toHaveBeenCalledWith('p-1');
    });

    it('does nothing when there is neither a typed value nor one on file', () => {
      const c = create();
      expect(() => c.onEyeClick(el())).not.toThrow();
      expect(getFullSsn).not.toHaveBeenCalled();
    });
  });

  describe('the audited on-file reveal', () => {
    function ready(c: SsnInputComponent): void {
      c.patientId = 'p-1';
      c.currentMaskedSsn = maskedDto('6789');
      user = { id: 'u-1', roles: ['admin'] };
    }

    it('fetches the stored value and shows it', () => {
      const c = create();
      ready(c);
      c.toggleOnFile();
      expect(getFullSsn).toHaveBeenCalledWith('p-1');
      expect(c.onFileRevealed()).toBeTrue();
      expect(c.onFileLoading()).toBeFalse();
    });

    it('hides again without a second fetch', () => {
      const c = create();
      ready(c);
      c.toggleOnFile();
      getFullSsn.calls.reset();

      c.toggleOnFile();

      expect(c.onFileRevealed()).toBeFalse();
      expect(getFullSsn).not.toHaveBeenCalled();
    });

    it('re-reveals from what it already holds rather than re-auditing', () => {
      // Each fetch writes an audit record; re-showing a value already in memory is not a
      // new access and must not create one.
      const c = create();
      ready(c);
      c.toggleOnFile();
      c.toggleOnFile();
      getFullSsn.calls.reset();

      c.toggleOnFile();

      expect(c.onFileRevealed()).toBeTrue();
      expect(getFullSsn).not.toHaveBeenCalled();
    });

    it('does not fetch without a patient to fetch for', () => {
      // The caller is deliberately one who MAY reveal, so this exercises the missing-id
      // branch rather than short-circuiting on the access guard above it.
      const c = create();
      c.currentMaskedSsn = maskedDto('6789');
      c.patientId = null;
      user = { id: 'u-1', roles: ['admin'] };

      c.toggleOnFile();

      expect(getFullSsn).not.toHaveBeenCalled();
      expect(c.onFileRevealed()).toBeFalse();
    });

    it('reveals nothing and clears the spinner when the fetch is refused', () => {
      // A 403 from the server-side re-check lands here; the value must stay hidden.
      const c = create();
      ready(c);
      getFullSsn.and.returnValue(throwError(() => ({ status: 403 })));

      c.toggleOnFile();

      expect(c.onFileRevealed()).toBeFalse();
      expect(c.onFileLoading()).toBeFalse();
    });

    it('falls back to the redaction when the response carries no number', () => {
      /**
       * The empty revealed value is falsy, so onFileDisplay drops through to the masked
       * form rather than blanking the box. That is the safe direction: a reveal that
       * returned nothing shows the redaction it already had, never an empty field that
       * reads as "this patient has no SSN on file".
       */
      const c = create();
      ready(c);
      getFullSsn.and.returnValue(of(null));

      c.toggleOnFile();

      expect(c.onFileRevealed()).toBeTrue();
      expect(c.onFileDisplay()).toBe(redacted('6789'));
    });

    it('formats the revealed value in groups', () => {
      const c = create();
      ready(c);
      c.toggleOnFile();
      expect(c.onFileDisplay()).toBe(grouped(NINE_ALT));
    });
  });

  describe('the reveal ACTION re-checks access, not only the button', () => {
    /**
     * Added 2026-09-17 with the guard it describes. `toggleOnFile` previously trusted the
     * eye button's disabled state as the only client-side gate; it now re-checks
     * `canReveal()` itself, matching `AppointmentDocumentsComponent.approve()`.
     *
     * This is defence in depth and NOT a security boundary: the server's SsnRevealAccess
     * predicate re-checks every request and returns 403. The client guard mirrors that
     * predicate's structure -- internal role, or record owner -- by reusing `canReveal()`
     * rather than restating the rule, so the two cannot drift apart.
     *
     * THE NEGATIVE AND POSITIVE CASES BELOW ARE A PAIR, ON PURPOSE. "Did not fetch" is
     * satisfied by anything broken -- a bad fixture, a missing patient id, a typo in the
     * spy name -- so on its own it would pass with the guard deleted AND with the whole
     * component broken. The positive case runs the SAME setup with one field changed, so
     * removing the guard fails the first while the second still passes. That is what makes
     * the failure attributable to the guard.
     */
    function onFileFor(c: SsnInputComponent, ownerId: string | null): void {
      c.patientId = 'p-1';
      c.currentMaskedSsn = maskedDto('6789');
      c.patientIdentityUserId = ownerId;
      user = { id: 'u-1', roles: ['Patient'] };
    }

    it('REFUSES to disclose a stored number to a patient who does not own the record', () => {
      const c = create();
      onFileFor(c, 'a-different-patient');

      c.toggleOnFile();

      expect(getFullSsn).not.toHaveBeenCalled();
      expect(c.onFileRevealed()).toBeFalse();
      expect(c.onFileDisplay())
        .withContext('and the box still shows only the redacted form')
        .toBe(redacted('6789'));
    });

    it('but DOES disclose it to the record owner, from the same setup', () => {
      // The positive half of the pair. Identical fixture, one field changed.
      const c = create();
      onFileFor(c, 'u-1');

      c.toggleOnFile();

      expect(getFullSsn).toHaveBeenCalledWith('p-1');
      expect(c.onFileRevealed()).toBeTrue();
    });

    it('and DOES disclose it to an internal caller who owns no record at all', () => {
      // The other arm of the server predicate: internal role, no ownership required.
      const c = create();
      onFileFor(c, 'a-different-patient');
      user = { id: 'u-1', roles: ['Intake Staff'] };

      c.toggleOnFile();

      expect(getFullSsn).toHaveBeenCalledWith('p-1');
      expect(c.onFileRevealed()).toBeTrue();
    });

    it('still lets ANYONE hide a value that is already showing', () => {
      /**
       * The guard sits after the hide branch deliberately. Hiding removes disclosure, so
       * it must never be refused -- a caller whose access lapsed mid-session must still be
       * able to put the number away.
       */
      const c = create();
      onFileFor(c, 'u-1');
      c.toggleOnFile();
      expect(c.onFileRevealed()).toBeTrue();

      c.patientIdentityUserId = 'a-different-patient';
      c.toggleOnFile();

      expect(c.onFileRevealed()).toBeFalse();
    });

    it('does not write a second audit row when a permitted caller re-shows it', () => {
      // Point of the cache, and it must survive the new guard: each fetch is an audited
      // access, so hiding and re-showing must not create another one.
      const c = create();
      onFileFor(c, 'u-1');

      c.toggleOnFile();
      c.toggleOnFile();
      c.toggleOnFile();

      expect(getFullSsn).toHaveBeenCalledTimes(1);
      expect(c.onFileRevealed()).toBeTrue();
    });
  });

  describe('the redaction itself', () => {
    it('shows at most the last four digits', () => {
      const c = create();
      c.entryDigits.set(NINE_ALT);
      c.entryHidden.set(true);
      const shown = c.entryDisplay();
      expect(shown).toBe(redacted('6789'));
      expect(shown).withContext('the leading digits must never appear').not.toContain('00000');
    });

    it('shows NO digits at all when fewer than four have been typed', () => {
      /**
       * A partial number is still a number. Masking a three-digit entry as anything that
       * kept a digit would leak the start of an SSN mid-typing, which is exactly when the
       * idle timer re-hides the field.
       */
      const c = create();
      c.entryHidden.set(true);
      ['0', '00', '000'].forEach((digits) => {
        c.entryDigits.set(digits);
        const shown = c.entryDisplay();
        expect(shown).toBe(DOT.repeat(digits.length));
        expect(shown).toMatch(/^[^0-9]*$/);
      });
    });

    it('shows nothing at all for an empty entry', () => {
      const c = create();
      c.entryDigits.set('');
      expect(c.entryDisplay()).toBe('');
    });

    it('groups a revealed value only when the field is not focused', () => {
      // While focused the raw digits are shown, because inserting separators under the
      // cursor moves it and corrupts what the user is typing.
      const c = create();
      c.entryDigits.set(NINE_ALT);
      c.entryHidden.set(false);

      c.onFocus(el());
      expect(c.entryDisplay()).toBe(NINE_ALT);

      c.onBlur();
      c.entryHidden.set(false);
      expect(c.entryDisplay()).toBe(grouped(NINE_ALT));
    });

    it('groups a partial value at each boundary', () => {
      const c = create();
      c.entryHidden.set(false);
      c.onBlur();
      c.entryHidden.set(false);

      c.entryDigits.set('000');
      expect(c.entryDisplay()).toBe('000');

      c.entryDigits.set('00000');
      expect(c.entryDisplay()).toBe(grouped('00000'));

      c.entryDigits.set(NINE_ALT);
      expect(c.entryDisplay()).toBe(grouped(NINE_ALT));
    });
  });

  describe('what the box shows', () => {
    it('shows the entry once the user has typed, in preference to the stored value', () => {
      const c = create();
      c.patientId = 'p-1';
      c.currentMaskedSsn = maskedDto('6789');
      c.entryDigits.set('0000');
      c.entryHidden.set(false);

      // Not focused, so a revealed entry renders grouped: four digits become '000-0'.
      expect(c.boxDisplay()).toBe(grouped('0000'));
      expect(c.boxDisplay())
        .withContext('the stored value must not win once something is typed')
        .not.toBe(redacted('6789'));
    });

    it('shows the on-file value while the box is untouched', () => {
      const c = create();
      c.patientId = 'p-1';
      c.currentMaskedSsn = maskedDto('6789');
      expect(c.boxDisplay()).toBe(redacted('6789'));
    });

    it('shows nothing when there is neither an entry nor a stored value', () => {
      const c = create();
      expect(c.boxDisplay()).toBe('');
    });
  });

  describe('typing', () => {
    it('keeps only digits, and at most nine of them', () => {
      const c = create();
      c.onInput(el(grouped(NINE_ALT) + 'extra'));
      expect(c.entryDigits()).toBe(NINE_ALT);
    });

    it('writes the cleaned digits back so separators cannot accumulate', () => {
      const c = create();
      const input = el(grouped(NINE_ALT));
      c.onInput(input);
      expect(input.value).toBe(NINE_ALT);
    });

    it('emits the digits to the form', () => {
      const c = create();
      const seen: (string | null)[] = [];
      c.registerOnChange((v) => seen.push(v));
      c.onInput(el('0001'));
      expect(seen).toEqual(['0001']);
    });

    it('emits NULL rather than an empty string when the box is cleared', () => {
      /**
       * This is the whole "leave the stored SSN unchanged" contract. An empty string is a
       * value the backend would write; null is the absence of one. Getting this wrong
       * blanks a stored SSN whenever someone clears the box and saves.
       */
      const c = create();
      const seen: (string | null)[] = [];
      c.registerOnChange((v) => seen.push(v));

      c.onInput(el('0001'));
      c.onInput(el(''));

      expect(seen).toEqual(['0001', null]);
    });

    it('clears the box on focus so the stored mask is never read back as typed input', () => {
      /**
       * The box displays the on-file mask while untouched. If focusing left that string in
       * the field, the next keystroke would append to it and the user would submit the mask
       * itself as their SSN.
       */
      const c = create();
      c.patientId = 'p-1';
      c.currentMaskedSsn = maskedDto('6789');
      const input = el(redacted('6789'));

      c.onFocus(input);

      expect(input.value).toBe('');
      expect(c.entryDigits()).withContext('and no value is adopted into the form').toBe('');
    });

    it('leaves a typed value alone on focus', () => {
      const c = create();
      c.entryDigits.set('0001');
      const input = el('0001');
      c.onFocus(input);
      expect(input.value).toBe('0001');
    });

    it('restores the raw digits before a keystroke lands on a masked field', () => {
      // Editing the mask string itself would splice bullets into the number.
      const c = create();
      c.entryDigits.set(NINE_ALT);
      c.entryHidden.set(true);
      const input = el(redacted('6789'));

      c.onKeydown(input);

      expect(c.entryHidden()).toBeFalse();
      expect(input.value).toBe(NINE_ALT);
    });

    it('re-hides and marks the control touched on blur', () => {
      const c = create();
      let touched = false;
      c.registerOnTouched(() => (touched = true));
      c.entryHidden.set(false);

      c.onBlur();

      expect(c.entryHidden()).toBeTrue();
      expect(touched).toBeTrue();
    });

    it('toggles the typed value between hidden and shown', () => {
      const c = create();
      c.entryDigits.set(NINE_ALT);
      c.entryHidden.set(true);
      const input = el();

      c.toggleEntryReveal(input);
      expect(c.entryHidden()).toBeFalse();
      expect(input.value).toBe(grouped(NINE_ALT));

      c.toggleEntryReveal(input);
      expect(c.entryHidden()).toBeTrue();
    });

    it('BLOCKS copy and cut of the entered value', () => {
      // The entry is deliberately not copyable: a number typed into this box should not be
      // liftable out of it onto the clipboard. Pasting in is still allowed.
      const c = create();
      const event = new Event('copy', { cancelable: true });
      c.block(event);
      expect(event.defaultPrevented).toBeTrue();
    });
  });

  describe('the idle re-hide', () => {
    it('redacts the field after the idle delay while it is focused', fakeAsync(() => {
      const c = create();
      const input = el();
      c.onFocus(input);
      c.onInput(el(NINE_ALT));
      expect(c.entryHidden()).toBeFalse();

      tick(1200);

      expect(c.entryHidden()).toBeTrue();
    }));

    it('does not redact once the field has been left', fakeAsync(() => {
      // onBlur already hides and clears the timer; a timer firing afterwards would be a
      // second, unowned state change.
      const c = create();
      c.onFocus(el());
      c.onBlur();
      c.entryHidden.set(false);

      tick(1200);

      expect(c.entryHidden()).toBeFalse();
    }));

    it('restarts the delay on each keystroke rather than redacting mid-entry', fakeAsync(() => {
      const c = create();
      const input = el();
      c.onFocus(input);

      tick(1000);
      c.onKeydown(input);
      tick(1000);

      expect(c.entryHidden()).withContext('the second keystroke reset the clock').toBeFalse();

      tick(200);
      expect(c.entryHidden()).toBeTrue();
    }));
  });

  describe('the form-control contract', () => {
    it('keeps only digits when a value is written in, capped at nine', () => {
      const c = create();
      c.writeValue(grouped(NINE_ALT));
      expect(c.entryDigits()).toBe(NINE_ALT);
      expect(c.entryHidden()).withContext('a written value arrives redacted').toBeTrue();
    });

    it('treats null and empty as no value', () => {
      const c = create();
      c.writeValue(NINE);
      c.writeValue(null);
      expect(c.entryDigits()).toBe('');

      c.writeValue('');
      expect(c.entryDigits()).toBe('');
    });

    it('caps an over-long written value at nine digits', () => {
      const c = create();
      c.writeValue(NINE + '9999');
      expect(c.entryDigits().length).toBe(9);
    });

    it('records the disabled state the form sets', () => {
      const c = create();
      c.setDisabledState(true);
      expect(c.disabled).toBeTrue();
      c.setDisabledState(false);
      expect(c.disabled).toBeFalse();
    });
  });
});
