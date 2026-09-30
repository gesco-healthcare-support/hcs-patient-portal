import { Directive, ElementRef, HostListener, inject } from '@angular/core';

/**
 * QA #15 obs 1 (2026-07-07): reformat a raw string into a progressive US date
 * (MM/DD/YYYY). Non-digits are stripped, digits are capped at 8 (MMDDYYYY), and a
 * `/` is inserted only before a group that already has content -- so "06" stays
 * "06" (no dangling slash) and "061" becomes "06/1". Idempotent on an
 * already-formatted value, which lets the directive re-run safely.
 */
export function formatPartialUsDate(raw: string): string {
  const digits = (raw ?? '').replace(/\D/g, '').slice(0, 8);
  const groups = [digits.slice(0, 2), digits.slice(2, 4), digits.slice(4, 8)].filter(
    (g) => g.length > 0,
  );
  return groups.join('/');
}

/**
 * QA #15 obs 1 (2026-07-07): auto-inserts the "/" separators as the user types into
 * a US date field, so they never wonder whether to type "/" or "-".
 *
 * Applied ALONGSIDE `ngbDatepicker` on the same input. It is deliberately NOT a
 * ControlValueAccessor (ngbDatepicker already owns that role, and Angular forbids
 * two): on each `input` it rewrites the element text via {@link formatPartialUsDate}
 * and re-dispatches a bubbling `input` event so the datepicker's CVA re-parses the
 * formatted value through UsDateParserFormatter. The re-dispatch terminates after
 * one pass because the second pass sees an already-formatted value (no change ->
 * no re-dispatch). The caret is moved to the end, which is correct for the
 * left-to-right date entry these fields expect.
 */
@Directive({
  selector: 'input[appUsDateAutoSlash]',
  standalone: true,
})
export class UsDateAutoSlashDirective {
  private readonly host = inject<ElementRef<HTMLInputElement>>(ElementRef);

  @HostListener('input')
  onInput(): void {
    const input = this.host.nativeElement;
    const formatted = formatPartialUsDate(input.value);
    if (formatted === input.value) {
      return;
    }
    input.value = formatted;
    input.setSelectionRange(formatted.length, formatted.length);
    // Let ngbDatepicker's value accessor re-read + parse the formatted text.
    input.dispatchEvent(new Event('input', { bubbles: true }));
  }
}
