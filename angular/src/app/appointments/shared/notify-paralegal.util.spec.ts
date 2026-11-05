import {
  EMPTY_NOTIFY_PARALEGAL,
  NOTIFY_PARALEGAL_ACCESS_TYPE_ID,
  notifyParalegalDraft,
  notifyParalegalErrors,
  notifyParalegalSummary,
  withNotifyParalegal,
  type NotifyParalegalState,
} from './notify-paralegal.util';
import { hasAttorneyPrefill } from './attorney-prefill.util';

const OK: NotifyParalegalState = {
  enabled: true,
  firstName: ' Jane ',
  lastName: 'Paralegal',
  email: ' jane.paralegal@example.test ',
};

describe('notify-paralegal.util', () => {
  it('reports nothing while unticked', () => {
    expect(notifyParalegalErrors(EMPTY_NOTIFY_PARALEGAL)).toEqual([]);
    expect(notifyParalegalErrors({ ...EMPTY_NOTIFY_PARALEGAL, email: 'bad' })).toEqual([]);
  });

  it('requires first name, last name and a valid email once ticked', () => {
    expect(notifyParalegalErrors({ ...EMPTY_NOTIFY_PARALEGAL, enabled: true })).toEqual([
      'firstName',
      'lastName',
      'email',
    ]);
    expect(notifyParalegalErrors({ ...OK, email: 'not-an-email' })).toEqual(['email']);
    expect(notifyParalegalErrors(OK)).toEqual([]);
  });

  it('builds a trimmed draft with the side paralegal role and the default access type', () => {
    expect(notifyParalegalDraft(OK, 'applicant')).toEqual({
      firstName: 'Jane',
      lastName: 'Paralegal',
      email: 'jane.paralegal@example.test',
      userRole: 'Applicant Paralegal',
      accessTypeId: NOTIFY_PARALEGAL_ACCESS_TYPE_ID,
    });
    expect(notifyParalegalDraft(OK, 'defense')?.userRole).toBe('Defense Paralegal');
    expect(notifyParalegalDraft({ ...OK, enabled: false }, 'defense')).toBeNull();
    expect(notifyParalegalDraft({ ...OK, email: '' }, 'defense')).toBeNull();
  });

  it('appends the draft once and ignores a duplicate email', () => {
    expect(withNotifyParalegal([], OK, 'applicant').length).toBe(1);
    const existing = [
      {
        firstName: '',
        lastName: '',
        email: 'JANE.PARALEGAL@example.test',
        userRole: 'X',
        accessTypeId: 24,
      },
    ];
    expect(withNotifyParalegal(existing, OK, 'applicant')).toEqual(existing);
  });

  it('adds nothing without a side (staff / paralegal bookers) or when unticked', () => {
    expect(withNotifyParalegal([], OK, null)).toEqual([]);
    expect(withNotifyParalegal([], EMPTY_NOTIFY_PARALEGAL, 'applicant')).toEqual([]);
  });

  it('summarises the errors for the banner', () => {
    const rows = notifyParalegalSummary({ ...EMPTY_NOTIFY_PARALEGAL, enabled: true });
    expect(rows.map((r) => r.control)).toEqual([
      'notifyParalegal.firstName',
      'notifyParalegal.lastName',
      'notifyParalegal.email',
    ]);
    expect(rows[2].message).toBe('Enter a valid email address');
  });
});

describe('hasAttorneyPrefill', () => {
  it('is false for null, blank and whitespace-only values', () => {
    expect(hasAttorneyPrefill(null)).toBeFalse();
    expect(
      hasAttorneyPrefill({ applicantAttorneyFirstName: null, defenseAttorneyEmail: '  ' }),
    ).toBeFalse();
  });

  it('is true when either side carries any text field', () => {
    expect(hasAttorneyPrefill({ applicantAttorneyFirstName: 'Ada' })).toBeTrue();
    expect(hasAttorneyPrefill({ defenseAttorneyFirmName: 'X LLP' })).toBeTrue();
  });
});
