import { AbstractControl, ValidationErrors } from '@angular/forms';

/**
 * Placeholder emails (2026-11-13): the "I do not know it" addresses people type into a required field
 * (unknown@gmail.com, na@na.com, xx@x.com). They look valid, then receive real case mail.
 *
 * The lists below are the SAME lists as `PlaceholderEmail.cs` in Domain.Shared; a Domain test reads this
 * file and fails when they differ. Change both together. Keep each list as plain quoted strings.
 */
export const PLACEHOLDER_EMAIL_WORDS: readonly string[] = [
  'unknown',
  'unkown',
  'unknow',
  'unknwn',
  'unk',
  'unkn',
  'notknown',
  'dontknow',
  'donotknow',
  'idk',
  'na',
  'no',
  'none',
  'nil',
  'null',
  'nobody',
  'noone',
  'nothing',
  'x',
  'noemail',
  'nomail',
  'noaddress',
  'noemailaddress',
  'notprovided',
  'notavailable',
  'notapplicable',
  'unavailable',
  'tbd',
  'tba',
  'tbc',
  'pending',
  'empty',
  'blank',
  'missing',
  'test',
  'testing',
  'tester',
  'fake',
  'dummy',
  'sample',
  'example',
  'placeholder',
  'temp',
  'invalid',
  'noreply',
  'donotreply',
  'asdf',
  'qwerty',
  'abc',
  'abcd',
];

export const PLACEHOLDER_EMAIL_DOMAINS: readonly string[] = [
  'noemail.com',
  'nomail.com',
  'none.com',
  'unknown.com',
  'fake.com',
  'example.com',
  'example.net',
  'example.org',
];

export const PLACEHOLDER_EMAIL_MESSAGE =
  "This looks like a placeholder email (for example unknown@...). Enter the person's real email address, or leave the field blank if it is optional.";

/**
 * True when the address is a placeholder. Rule: the part before the last `@`, lowercased, cut at the
 * first `+`, with `.` `-` `_` `/` removed and trailing digits removed, is in the word list or is one
 * character repeated two or more times; or the domain is in the domain list. Blank is NOT a placeholder.
 */
export function isPlaceholderEmail(email: string | null | undefined): boolean {
  const trimmed = (email ?? '').trim();
  if (!trimmed) {
    return false;
  }
  const at = trimmed.lastIndexOf('@');
  const local = at >= 0 ? trimmed.slice(0, at) : trimmed;
  const domain = at >= 0 ? trimmed.slice(at + 1).toLowerCase() : '';
  if (PLACEHOLDER_EMAIL_DOMAINS.includes(domain)) {
    return true;
  }
  let key = local.toLowerCase();
  const plus = key.indexOf('+');
  if (plus >= 0) {
    key = key.slice(0, plus);
  }
  key = key.replace(/[.\-_/]/g, '').replace(/[0-9]+$/, '');
  if (!key) {
    return false;
  }
  return (
    PLACEHOLDER_EMAIL_WORDS.includes(key) ||
    (key.length >= 2 && [...key].every((c) => c === key[0]))
  );
}

/** Reactive-forms validator: `{ placeholderEmail: true }` for a placeholder address, null otherwise. */
export function placeholderEmailValidator(control: AbstractControl): ValidationErrors | null {
  return isPlaceholderEmail(control.value as string | null) ? { placeholderEmail: true } : null;
}
