import { CustomFieldType } from '../../proxy/enums/custom-field-type.enum';

/**
 * PR2 (2026-07-10): format a saved custom-field value for the read-only
 * appointment detail views. Returns '' for an unanswered field so each caller
 * applies its own empty-state placeholder ('-' internal, 'Not provided'
 * external). Tickbox booleans render as Yes/No; every other type shows the
 * stored string as-is (dates are already ISO yyyy-MM-dd from the booking form).
 */
export function formatCustomFieldValue(
  fieldType: CustomFieldType | undefined,
  value: string | null | undefined,
): string {
  const raw = (value ?? '').trim();
  if (!raw) {
    return '';
  }
  if (fieldType === CustomFieldType.Tickbox) {
    const lower = raw.toLowerCase();
    if (lower === 'true') {
      return 'Yes';
    }
    if (lower === 'false') {
      return 'No';
    }
  }
  return raw;
}
