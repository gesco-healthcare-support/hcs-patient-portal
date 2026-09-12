import { Pipe, PipeTransform } from '@angular/core';

/**
 * QA item A (2026-06-29) -- display an office/tenant name respectfully as
 * "Dr. {Name}". Tenant.Name is stored as a lowercased subdomain slug (e.g.
 * "falkinstein"), so the first letter is capitalized for display.
 *
 * Idempotent: a value already prefixed with "Dr" (e.g. "Dr. Falkinstein") is
 * returned unchanged, so the pipe is safe to apply even where a name might
 * already carry the prefix. Returns '' for null/empty.
 *
 * Apply ONLY to the raw tenant/office name -- NEVER to the staff-editable
 * OfficeBranding DisplayName, which is shown verbatim.
 */
@Pipe({ name: 'officeName', standalone: true, pure: true })
export class OfficeNamePipe implements PipeTransform {
  transform(value?: string | null): string {
    const name = (value ?? '').trim();
    if (!name) return '';
    if (/^dr\.?\s/i.test(name)) return name;
    return `Dr. ${name.charAt(0).toUpperCase()}${name.slice(1)}`;
  }
}
