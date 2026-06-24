import { Pipe, PipeTransform } from '@angular/core';

/**
 * Issue 2.1 (2026-05-12) -- Mask a Social Security Number for read-only
 * display. Shows `***-**-1234` (last 4 visible) so an admin can still
 * identify which record they are looking at, but the full 9 digits do
 * not leak to the rendered DOM. NIST SP 800-122 treats partial SSNs as
 * PII; this mask is appropriate for authenticated UI surfaces where the
 * user already has access to the record. Never use this for an
 * input element -- use `ngx-mask` with `[hiddenInput]="true"` there.
 *
 * Accepts SSN with or without hyphens. Returns empty string when the
 * value is null / undefined / empty.
 */
@Pipe({ name: 'ssnMask', standalone: true, pure: true })
export class SsnMaskPipe implements PipeTransform {
  transform(value?: string | null): string {
    if (!value) return '';
    const digits = String(value).replace(/\D/g, '');
    if (digits.length === 0) return '';
    // I14 (2026-06-08): bullet circles (codepoint U+2022) instead of asterisks.
    // The escape renders as a dot and keeps the source ASCII-only.
    const dot = String.fromCharCode(0x2022);
    if (digits.length < 4) return dot.repeat(digits.length);
    return `${dot}${dot}${dot}-${dot}${dot}-${digits.slice(-4)}`;
  }
}
