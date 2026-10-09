import {
  LETTERHEAD_TEXT_KEYS,
  emptyLetterheadForm,
  letterheadFormFromDto,
  letterheadInputFromForm,
  parseFee,
} from './office-letterhead.util';
import type { OfficeLetterheadDto } from './office-letterhead.service';

/**
 * The packet-letterhead editor's form mapping (walkthrough Q5). The rule the server relies on:
 * a blank field goes up as null, which clears it back to the derived default. A fee the user
 * typed wrong must block the save rather than be guessed. All values synthetic.
 */
describe('office-letterhead.util', () => {
  const dto = (over: Partial<OfficeLetterheadDto> = {}): OfficeLetterheadDto => ({
    defaultPhysicianName: 'Dr. TEST-Ada TEST-Example',
    defaultLetterheadName: 'Dr. TEST-Ada TEST-Example',
    defaultPracticeName: 'TEST Office',
    ...over,
  });

  describe('parseFee', () => {
    it('treats blank as no fee', () => {
      expect(parseFee('')).toBeNull();
      expect(parseFee('   ')).toBeNull();
    });

    it('accepts whole and two-decimal amounts, with an optional $ and thousands commas', () => {
      expect(parseFee('250')).toBe(250);
      expect(parseFee('503.75')).toBe(503.75);
      expect(parseFee(' $1,200.5 ')).toBe(1200.5);
    });

    it('refuses anything that is not a non-negative amount', () => {
      for (const bad of ['-5', 'abc', '1.234', '1.2.3', '$-1']) {
        expect(parseFee(bad)).withContext(bad).toBeUndefined();
      }
    });
  });

  it('starts empty, one entry per editable text field', () => {
    const form = emptyLetterheadForm();
    expect(Object.keys(form.text).sort()).toEqual([...LETTERHEAD_TEXT_KEYS].sort());
    expect(Object.values(form.text).every((v) => v === '')).toBeTrue();
    expect(form.fee).toBe('');
  });

  it('loads stored values and shows the fee with cents', () => {
    const form = letterheadFormFromDto(
      dto({
        physicianName: 'TEST-Ada Example, M.D.',
        phone: '555-0100',
        missedAppointmentFee: 250,
      }),
    );
    expect(form.text.physicianName).toBe('TEST-Ada Example, M.D.');
    expect(form.text.phone).toBe('555-0100');
    expect(form.text.fax).toBe('');
    expect(form.fee).toBe('250.00');
  });

  it('loads an empty form when there is no dto', () => {
    expect(letterheadFormFromDto(null)).toEqual(emptyLetterheadForm());
  });

  it('sends blank text as null and trims the rest', () => {
    const form = emptyLetterheadForm();
    form.text.practiceName = '  TEST Institute  ';
    form.text.fax = '   ';
    form.fee = '12.50';

    const input = letterheadInputFromForm(form)!;

    expect(input.practiceName).toBe('TEST Institute');
    expect(input.fax).toBeNull();
    expect(input.physicianName).toBeNull();
    expect(input.missedAppointmentFee).toBe(12.5);
  });

  it('refuses to build a payload when the fee is invalid', () => {
    const form = emptyLetterheadForm();
    form.fee = 'twelve';
    expect(letterheadInputFromForm(form)).toBeUndefined();
  });
});
