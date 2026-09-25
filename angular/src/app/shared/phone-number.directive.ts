import { Directive, ElementRef, HostListener, OnInit, forwardRef, inject } from '@angular/core';
import {
  AbstractControl,
  ControlValueAccessor,
  NG_VALIDATORS,
  NG_VALUE_ACCESSOR,
  ValidationErrors,
  Validator,
} from '@angular/forms';

/** A US phone number is exactly this many digits. */
export const PHONE_DIGIT_COUNT = 10;

/** Length of the fully formatted value, `(123)-456-7890`. */
const FORMATTED_LENGTH = 14;

/** Everything that is not a digit is punctuation someone typed or pasted; only digits are data. */
export function phoneDigits(raw: string | null | undefined): string {
  return (raw ?? '').replace(/\D/g, '').slice(0, PHONE_DIGIT_COUNT);
}

/**
 * Formats however many digits are present so far, so the field reads correctly while it is being
 * typed rather than only once it is complete. Idempotent on an already-formatted value, which is
 * what lets the directive re-run on every keystroke.
 */
export function formatPartialUsPhone(raw: string | null | undefined): string {
  const digits = phoneDigits(raw);
  if (!digits) return '';
  if (digits.length <= 3) return `(${digits}`;
  if (digits.length <= 6) return `(${digits.slice(0, 3)})-${digits.slice(3)}`;
  return `(${digits.slice(0, 3)})-${digits.slice(3, 6)}-${digits.slice(6)}`;
}

/** Index just past the nth digit of a formatted value -- used to put the caret back where it was. */
function caretAfterDigits(formatted: string, digitCount: number): number {
  if (digitCount <= 0) return 0;
  let seen = 0;
  for (let i = 0; i < formatted.length; i++) {
    if (/\d/.test(formatted[i])) {
      seen++;
      if (seen === digitCount) return i + 1;
    }
  }
  return formatted.length;
}

/**
 * A phone-number field: digits only, a hard stop at the tenth, displayed as `(123)-456-7890`.
 *
 * <p>WHY A ControlValueAccessor rather than a formatter like
 * {@link import('./us-date-auto-slash.directive').UsDateAutoSlashDirective}. The stored value and
 * the displayed value are deliberately DIFFERENT here: the model carries ten bare digits and the
 * element shows the punctuation. Keeping the punctuation out of the model is what makes the value
 * comparable, searchable and safe to hand to another system -- the Case Tracker included -- and a
 * CVA is the only place in Angular where that split belongs. A formatter directive would push
 * `(123)-456-7890` into the model and every consumer would have to strip it again, which is the
 * kind of thing that gets forgotten in exactly one place.</p>
 *
 * <p>It also validates. A partially typed number is not a phone number, so anything present but
 * not ten digits reports `phoneNumber` with the length it actually has. NOTE the consequence for
 * existing records: a legacy value that is not ten digits (seven-digit numbers and numbers with an
 * extension both exist) will show as invalid the first time someone opens that record, and will
 * have to be corrected before the form can be saved. That is deliberate -- the alternative is
 * accepting new bad data to avoid surfacing old bad data -- but it is a behaviour change on
 * existing rows, not only on new input.</p>
 *
 * <p>The caret is preserved by DIGIT position, not by character position, so re-formatting after
 * each keystroke does not throw the cursor to the end while someone is correcting an area code.
 * Backspace over a separator deletes the digit behind it rather than the punctuation, which the
 * mask would otherwise put straight back.</p>
 */
@Directive({
  selector: 'input[appPhoneNumber]',
  standalone: true,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => PhoneNumberDirective),
      multi: true,
    },
    {
      provide: NG_VALIDATORS,
      useExisting: forwardRef(() => PhoneNumberDirective),
      multi: true,
    },
  ],
})
export class PhoneNumberDirective implements ControlValueAccessor, Validator, OnInit {
  private readonly host = inject<ElementRef<HTMLInputElement>>(ElementRef);

  private onChange: (value: string | null) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  ngOnInit(): void {
    // Set imperatively so it wins over any maxlength left on the template. Several of these
    // fields carried maxlength="12" or "19" from before the format was fixed, and a maxlength
    // shorter than the formatted value silently truncates a complete number.
    this.host.nativeElement.maxLength = FORMATTED_LENGTH;
  }

  writeValue(value: unknown): void {
    // Normalizes on the way IN as well, so a stored value with punctuation, spaces or a country
    // code renders the same as one typed today.
    this.host.nativeElement.value = formatPartialUsPhone(
      typeof value === 'string' || typeof value === 'number' ? String(value) : '',
    );
  }

  registerOnChange(fn: (value: string | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.host.nativeElement.disabled = isDisabled;
  }

  validate(control: AbstractControl): ValidationErrors | null {
    const digits = phoneDigits(control.value as string);
    if (!digits) return null;
    return digits.length === PHONE_DIGIT_COUNT
      ? null
      : { phoneNumber: { requiredLength: PHONE_DIGIT_COUNT, actualLength: digits.length } };
  }

  @HostListener('input')
  onInput(): void {
    const input = this.host.nativeElement;
    const caret = input.selectionStart ?? input.value.length;
    const digitsBeforeCaret = phoneDigits(input.value.slice(0, caret)).length;

    const digits = phoneDigits(input.value);
    const formatted = formatPartialUsPhone(digits);

    if (input.value !== formatted) {
      input.value = formatted;
      const next = caretAfterDigits(formatted, digitsBeforeCaret);
      input.setSelectionRange(next, next);
    }

    // Empty stays null rather than '' so an optional field reads as absent, matching what the
    // rest of these forms send for a blank text input.
    this.onChange(digits || null);
  }

  @HostListener('blur')
  onBlur(): void {
    this.onTouched();
  }

  @HostListener('keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (event.key !== 'Backspace') return;

    const input = this.host.nativeElement;
    const start = input.selectionStart ?? 0;
    if (start === 0 || start !== input.selectionEnd) return;
    if (/\d/.test(input.value[start - 1])) return;

    // The caret sits just after punctuation. Extend the selection back through the separators to
    // include the digit before them, so one Backspace removes one digit.
    let index = start - 1;
    while (index > 0 && !/\d/.test(input.value[index - 1])) index--;
    if (index === 0) return;

    input.setSelectionRange(index - 1, start);
  }
}
