import {
  ChangeRequestSide,
  EmailRecipientKind,
  EmailRoutingNote,
  EmailStopScope,
} from './email-stops.models';
import type { AppointmentEmailRecipientDto, EmailStopStatusDto } from './email-stops.models';
import {
  distinctSides,
  isStoppedIn,
  kindKey,
  otherCaseNumbers,
  pillFor,
  routingNoteKey,
  sideKey,
  stoppedEmailSet,
} from './email-stops.util';

const stop: EmailStopStatusDto = {
  id: 's',
  scope: EmailStopScope.Case,
  stoppedByName: null,
  stoppedAt: '2026-11-03T00:00:00Z',
  note: null,
  stoppedFromOfficeName: null,
};
const rec = (email: string, over: Partial<AppointmentEmailRecipientDto> = {}) => ({
  email,
  name: null,
  kinds: [EmailRecipientKind.Patient],
  routingNote: EmailRoutingNote.None,
  caseStop: null,
  allCasesStop: null,
  ...over,
});

describe('email-stops.util', () => {
  it('pillFor lets all-cases win over case, and neither means active', () => {
    expect(pillFor(rec('a'))).toBe('active');
    expect(pillFor(rec('a', { caseStop: stop }))).toBe('case');
    expect(pillFor(rec('a', { caseStop: stop, allCasesStop: stop }))).toBe('allCases');
  });

  it('kindKey and routingNoteKey map to plain-language keys', () => {
    expect(kindKey(EmailRecipientKind.Booker)).toBe('::EmailStops:Kind:Booker');
    expect(routingNoteKey(EmailRoutingNote.None)).toBeNull();
    expect(routingNoteKey(EmailRoutingNote.StartsAfterApproval)).toBe(
      '::EmailStops:Note:StartsAfterApproval',
    );
  });

  it('sideKey names the applicant and defense sides', () => {
    expect(sideKey(ChangeRequestSide.SideA)).toBe('::EmailStops:Side:Applicant');
    expect(sideKey(ChangeRequestSide.SideB)).toBe('::EmailStops:Side:Defense');
  });

  it('stoppedEmailSet matches case-insensitively and ignores people getting emails', () => {
    const set = stoppedEmailSet([
      rec('Test.One@Example.test', { caseStop: stop }),
      rec('b@x.test'),
    ]);
    expect(isStoppedIn(set, 'test.one@example.TEST')).toBeTrue();
    expect(isStoppedIn(set, ' TEST.ONE@example.test ')).toBeTrue();
    expect(isStoppedIn(set, 'b@x.test')).toBeFalse();
    expect(isStoppedIn(set, null)).toBeFalse();
  });

  it('joins the other case numbers and de-duplicates sides', () => {
    const impact = {
      hasImpact: true,
      thisCaseSides: [ChangeRequestSide.SideA, ChangeRequestSide.SideA],
      otherCases: [
        { appointmentId: '1', requestConfirmationNumber: 'A00001', side: ChangeRequestSide.SideA },
        { appointmentId: '2', requestConfirmationNumber: 'A00002', side: ChangeRequestSide.SideB },
      ],
    };
    expect(otherCaseNumbers(impact)).toBe('A00001, A00002');
    expect(distinctSides(impact)).toEqual([ChangeRequestSide.SideA]);
  });
});

describe('Users hub rail', () => {
  it('has a Stopped emails tab gated by the email-stops permission', async () => {
    const { USERS_SECTIONS } = await import('../users/users-hub.util');
    const tab = USERS_SECTIONS.find((s) => s.key === 'emailStops');
    expect(tab?.route).toBe('/users/email-stops');
    expect(tab?.policy).toBe('CaseEvaluation.EmailStops');
  });
});
