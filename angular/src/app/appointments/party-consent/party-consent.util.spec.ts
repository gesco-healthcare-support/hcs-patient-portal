import { ChangeRequestType } from '../../proxy/appointment-change-requests/change-request-type.enum';
import { ChangeRequestConsentStatus } from '../../proxy/appointment-change-requests/change-request-consent-status.enum';
import type { PartyConsentRequestDto } from '../../proxy/appointment-change-requests/models';
import {
  partyConsentConfirmText,
  partyConsentHeadline,
  partyConsentSubject,
} from './party-consent.util';

describe('party-consent.util', () => {
  const item = (over: Partial<PartyConsentRequestDto> = {}): PartyConsentRequestDto => ({
    changeRequestId: 'cr-1',
    appointmentId: 'appt-1',
    confirmationNumber: 'A90001',
    patientName: 'TEST-Pat TEST-Doe',
    appointmentDate: '2031-07-01T09:30:00',
    changeRequestType: ChangeRequestType.Reschedule,
    requestedNewDateTime: 'Jul 9, 2031 at 10:00 AM',
    consentStatus: ChangeRequestConsentStatus.Pending,
    ...over,
  });

  it('headlines a reschedule with the proposed date', () => {
    expect(partyConsentHeadline(item())).toBe('Move this appointment to Jul 9, 2031 at 10:00 AM?');
    expect(partyConsentHeadline(item({ requestedNewDateTime: null }))).toBe(
      'Move this appointment to a new date?',
    );
  });

  it('headlines a cancellation', () => {
    expect(partyConsentHeadline(item({ changeRequestType: ChangeRequestType.Cancel }))).toBe(
      'Cancel this appointment?',
    );
  });

  it('names the case by patient and confirmation number, skipping blanks', () => {
    expect(partyConsentSubject(item())).toBe('TEST-Pat TEST-Doe - A90001');
    expect(partyConsentSubject(item({ patientName: '  ' }))).toBe('A90001');
  });

  it('says what each answer does before it is sent', () => {
    expect(partyConsentConfirmText(item(), true)).toContain('moved');
    expect(partyConsentConfirmText(item(), false)).toContain('propose another');
    const c = item({ changeRequestType: ChangeRequestType.Cancel });
    expect(partyConsentConfirmText(c, true)).toContain('cancelled');
    expect(partyConsentConfirmText(c, false)).toContain('stays booked');
  });
});
