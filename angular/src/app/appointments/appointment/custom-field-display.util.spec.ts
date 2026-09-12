import { CustomFieldType } from '../../proxy/enums/custom-field-type.enum';
import { formatCustomFieldValue } from './custom-field-display.util';

describe('formatCustomFieldValue', () => {
  it('returns an empty string for null / undefined / whitespace (unanswered)', () => {
    expect(formatCustomFieldValue(CustomFieldType.Alphanumeric, null)).toBe('');
    expect(formatCustomFieldValue(CustomFieldType.Alphanumeric, undefined)).toBe('');
    expect(formatCustomFieldValue(CustomFieldType.Alphanumeric, '   ')).toBe('');
  });

  it('maps a tickbox boolean to Yes / No (case-insensitive)', () => {
    expect(formatCustomFieldValue(CustomFieldType.Tickbox, 'true')).toBe('Yes');
    expect(formatCustomFieldValue(CustomFieldType.Tickbox, 'True')).toBe('Yes');
    expect(formatCustomFieldValue(CustomFieldType.Tickbox, 'false')).toBe('No');
  });

  it('leaves a multi-option tickbox string untouched', () => {
    expect(formatCustomFieldValue(CustomFieldType.Tickbox, 'Left, Right')).toBe('Left, Right');
  });

  it('trims and passes through non-tickbox values', () => {
    expect(formatCustomFieldValue(CustomFieldType.Alphanumeric, '  hello ')).toBe('hello');
    expect(formatCustomFieldValue(CustomFieldType.Numeric, '42')).toBe('42');
    expect(formatCustomFieldValue(CustomFieldType.Date, '2026-07-10')).toBe('2026-07-10');
    expect(formatCustomFieldValue(CustomFieldType.Picklist, 'Option A')).toBe('Option A');
  });
});
