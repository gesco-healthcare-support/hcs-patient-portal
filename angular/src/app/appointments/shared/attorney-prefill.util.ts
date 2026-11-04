/**
 * True when either attorney section already carries something the booker (or a prefill, a
 * resumed draft, a re-request source) put there. Used so the async external-profile load does
 * not wipe attorney fields that were filled before it returned (the prefill race).
 */
const ATTORNEY_TEXT_FIELDS = [
  'applicantAttorneyFirstName',
  'applicantAttorneyLastName',
  'applicantAttorneyEmail',
  'applicantAttorneyFirmName',
  'defenseAttorneyFirstName',
  'defenseAttorneyLastName',
  'defenseAttorneyEmail',
  'defenseAttorneyFirmName',
] as const;

export function hasAttorneyPrefill(raw: Record<string, unknown> | null | undefined): boolean {
  if (!raw) {
    return false;
  }
  return ATTORNEY_TEXT_FIELDS.some((key) => {
    const v = raw[key];
    return typeof v === 'string' && v.trim() !== '';
  });
}
