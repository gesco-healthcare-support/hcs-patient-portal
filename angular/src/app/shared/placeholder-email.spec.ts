import { FormControl } from '@angular/forms';

import {
  isPlaceholderEmail,
  PLACEHOLDER_EMAIL_DOMAINS,
  PLACEHOLDER_EMAIL_WORDS,
  placeholderEmailValidator,
} from './placeholder-email';

/** Placeholder emails (2026-11-13). The lists are drift-checked against the .NET copy by a Domain test. */
describe('placeholder email', () => {
  const placeholders = [
    'unknown@gmail.com',
    'Unknown@Gmail.com',
    'un.known@gmail.com',
    'un-known_1@gmail.com',
    'unknown123@gmail.com',
    'unknown+claim7@gmail.com',
    'na@na.com',
    'N/A@firm.com',
    'idk@firm.com',
    'none2@firm.com',
    'noemail@firm.com',
    'do.not.reply@firm.com',
    'test@firm.com',
    'x@firm.com',
    'xx@firm.com',
    'aaaa@firm.com',
    'anyone@example.com',
    'anyone@EXAMPLE.NET',
    'anyone@example.org',
    'jane@noemail.com',
    'jane@nomail.com',
    'jane@none.com',
    'jane@unknown.com',
    'jane@fake.com',
  ];
  const real = [
    'jane.doe@firm.com',
    'jdoe@firm.com',
    'levonk+test@firm.com',
    'unknownsender@firm.com',
    'nathan@firm.com',
    'xavier@firm.com',
    'ab@firm.com',
    '123@firm.com',
    '1111xx@firm.com',
    'jane@example.co',
    'jane@myexample.com',
    '',
    '   ',
  ];

  it('recognises every placeholder shape', () => {
    for (const email of placeholders) {
      expect(isPlaceholderEmail(email)).withContext(email).toBeTrue();
    }
  });

  it('lets real addresses and blank values through', () => {
    for (const email of real) {
      expect(isPlaceholderEmail(email)).withContext(email).toBeFalse();
    }
    expect(isPlaceholderEmail(null)).toBeFalse();
    expect(isPlaceholderEmail(undefined)).toBeFalse();
  });

  it('keeps lists with no duplicates, all lowercase', () => {
    expect(new Set(PLACEHOLDER_EMAIL_WORDS).size).toBe(PLACEHOLDER_EMAIL_WORDS.length);
    expect(new Set(PLACEHOLDER_EMAIL_DOMAINS).size).toBe(PLACEHOLDER_EMAIL_DOMAINS.length);
    expect(PLACEHOLDER_EMAIL_WORDS.every((w) => w === w.toLowerCase())).toBeTrue();
    expect(PLACEHOLDER_EMAIL_DOMAINS.every((d) => d === d.toLowerCase())).toBeTrue();
  });

  it('validator flags a placeholder and passes a real or blank value', () => {
    expect(placeholderEmailValidator(new FormControl('unknown@gmail.com'))).toEqual({
      placeholderEmail: true,
    });
    expect(placeholderEmailValidator(new FormControl('jane.doe@firm.com'))).toBeNull();
    expect(placeholderEmailValidator(new FormControl(''))).toBeNull();
    expect(placeholderEmailValidator(new FormControl(null))).toBeNull();
  });
});
