import { storedPatientDetailsAreFinal } from './patient-edit-notice.util';

describe('storedPatientDetailsAreFinal (#1107)', () => {
  ['Applicant Attorney', 'Defense Attorney', 'Claim Examiner'].forEach((role) => {
    it(`says so for ${role} once an existing patient is loaded`, () => {
      expect(storedPatientDetailsAreFinal([role], true)).toBe(true);
    });
  });

  ['Patient', 'admin', 'Intake Staff', 'Staff Supervisor', 'IT Admin'].forEach((role) => {
    it(`stays quiet for ${role}, whose edits the server applies`, () => {
      expect(storedPatientDetailsAreFinal([role], true)).toBe(false);
    });
  });

  it('stays quiet while no existing patient is loaded', () => {
    expect(storedPatientDetailsAreFinal(['Applicant Attorney'], false)).toBe(false);
  });

  it('stays quiet when the caller roles are not known yet', () => {
    expect(storedPatientDetailsAreFinal([], true)).toBe(false);
    expect(storedPatientDetailsAreFinal(undefined, true)).toBe(false);
  });

  it('stays quiet when any role is an editing one', () => {
    expect(storedPatientDetailsAreFinal(['Claim Examiner', 'Intake Staff'], true)).toBe(false);
  });
});
