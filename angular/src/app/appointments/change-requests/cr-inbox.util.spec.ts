import { ChangeRequestConsentStatus } from '../../proxy/appointment-change-requests/change-request-consent-status.enum';
import { ChangeRequestSide } from '../../proxy/appointment-change-requests/change-request-side.enum';
import {
  changeRequestAgeClass,
  changeRequestAgeDays,
  changeRequestConsentView,
  consentOverrideWarning,
  requestingSideLabel,
} from './cr-inbox.util';

describe('cr-inbox.util', () => {
  const now = new Date('2026-06-15T12:00:00Z').getTime();

  describe('changeRequestAgeDays', () => {
    it('counts whole days since the request was filed', () => {
      expect(changeRequestAgeDays('2026-06-13T12:00:00Z', now)).toBe(2);
      expect(changeRequestAgeDays('2026-06-15T12:00:00Z', now)).toBe(0);
    });
    it('clamps to 0 for a future or unparseable date', () => {
      expect(changeRequestAgeDays('2026-06-20T12:00:00Z', now)).toBe(0);
      expect(changeRequestAgeDays('not-a-date', now)).toBe(0);
      expect(changeRequestAgeDays(null, now)).toBe(0);
    });
  });

  describe('changeRequestAgeClass', () => {
    it('buckets by waiting age', () => {
      expect(changeRequestAgeClass(0)).toBe('ok');
      expect(changeRequestAgeClass(3)).toBe('ok');
      expect(changeRequestAgeClass(4)).toBe('warn');
      expect(changeRequestAgeClass(6)).toBe('warn');
      expect(changeRequestAgeClass(7)).toBe('crit');
    });
  });

  describe('changeRequestConsentView', () => {
    it('hides the chip when consent is not in play', () => {
      expect(changeRequestConsentView(ChangeRequestConsentStatus.NotRequired).show).toBe(false);
      expect(changeRequestConsentView(null).show).toBe(false);
    });
    it('maps each consent state to a label + class', () => {
      expect(changeRequestConsentView(ChangeRequestConsentStatus.Approved)).toEqual({
        show: true,
        label: 'Consent received',
        cls: 'agreed',
      });
      expect(changeRequestConsentView(ChangeRequestConsentStatus.Pending).cls).toBe('pending');
      expect(changeRequestConsentView(ChangeRequestConsentStatus.Rejected).cls).toBe('declined');
      expect(changeRequestConsentView(ChangeRequestConsentStatus.Expired).cls).toBe('declined');
    });
  });

  describe('consentOverrideWarning', () => {
    it('warns only when an unresolved consent would be overridden', () => {
      expect(consentOverrideWarning(ChangeRequestConsentStatus.Pending)).toContain('still pending');
      expect(consentOverrideWarning(ChangeRequestConsentStatus.Rejected)).toContain('declined');
      expect(consentOverrideWarning(ChangeRequestConsentStatus.Expired)).toContain('expired');
    });
    it('is silent when consent is granted or not required', () => {
      expect(consentOverrideWarning(ChangeRequestConsentStatus.Approved)).toBeNull();
      expect(consentOverrideWarning(ChangeRequestConsentStatus.NotRequired)).toBeNull();
      expect(consentOverrideWarning(null)).toBeNull();
    });
  });

  describe('requestingSideLabel', () => {
    it('names the filing side', () => {
      expect(requestingSideLabel(ChangeRequestSide.SideA)).toBe('Applicant side');
      expect(requestingSideLabel(ChangeRequestSide.SideB)).toBe('Defense side');
      expect(requestingSideLabel(null)).toBe('');
    });
  });
});
