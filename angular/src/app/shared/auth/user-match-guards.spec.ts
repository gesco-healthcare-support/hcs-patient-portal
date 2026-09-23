import { TestBed } from '@angular/core/testing';
import { ConfigStateService } from '@abp/ng.core';
import { CanMatchFn, Route } from '@angular/router';

import { hasOnlyExternalRoles } from './external-user-roles';
import { externalUserOnlyMatchGuard } from './external-user-match.guard';
import { internalUserOnlyMatchGuard } from './internal-user-match.guard';

/**
 * The role-split `canMatch` guards, and the classifier both of them delegate to.
 *
 * <p>WHAT THESE DECIDE. Several paths -- `appointments/view/:id` most importantly -- are declared
 * twice, once for pure-external users and once for internal staff, and which component a caller
 * gets is decided ONLY by these two guards. They are exact complements by design, so the risk
 * worth pinning is not either one in isolation but the two drifting apart: if both were to return
 * true for some role set, a shared path would resolve to whichever route is declared first, and
 * a patient could be served the internal component.</p>
 *
 * <p>So the last spec below asserts the complement property directly, over the role sets that
 * actually occur, rather than trusting two separately-correct implementations to stay opposite.</p>
 *
 * <p>THE ANONYMOUS CASE IS NOT A DEGENERATE ONE. Zero roles means "not authenticated yet", and
 * both guards deliberately treat it as internal -- external matches false, internal matches true
 * -- so the child route's `authGuard` can issue the OAuth challenge instead of the request
 * falling through to the wildcard 404. That is asserted, not assumed.</p>
 *
 * <p>All role names below are the real seeded ABP role names. No patient data is involved.</p>
 */
describe('role-split canMatch guards', () => {
  /**
   * Runs a guard with a stubbed current user. The stub keys on the argument, so nothing passes
   * merely because the guard asked for some other configuration key.
   */
  function matchWith(guard: CanMatchFn, currentUser: unknown): boolean {
    // Reset first: several specs below run this helper more than once, and TestBed refuses to be
    // reconfigured once instantiated. Without this, the complement specs -- which must evaluate
    // BOTH guards against one role set -- fail on the second call rather than on the property.
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: ConfigStateService,
          useValue: { getOne: (key: string) => (key === 'currentUser' ? currentUser : null) },
        },
      ],
    });
    return TestBed.runInInjectionContext(() => guard({} as Route, [])) as boolean;
  }

  describe('hasOnlyExternalRoles', () => {
    it('is false when roles are absent', () => {
      expect(hasOnlyExternalRoles(null)).toBe(false);
      expect(hasOnlyExternalRoles(undefined)).toBe(false);
    });

    it('is false for an empty role list', () => {
      expect(hasOnlyExternalRoles([])).toBe(false);
    });

    it('is false when every entry is blank or null', () => {
      // Distinct from the empty case: the list is non-empty, so the length guard passes and the
      // decision falls to the filtered list being empty.
      expect(hasOnlyExternalRoles(['  ', null, undefined, ''])).toBe(false);
    });

    it('is true when every role is external', () => {
      expect(hasOnlyExternalRoles(['Patient'])).toBe(true);
      expect(
        hasOnlyExternalRoles([
          'Patient',
          'Applicant Attorney',
          'Defense Attorney',
          'Claim Examiner',
        ]),
      ).toBe(true);
    });

    it('compares case-insensitively and ignores surrounding whitespace', () => {
      expect(hasOnlyExternalRoles(['  PATIENT ', 'claim examiner'])).toBe(true);
    });

    it('is false when any role is internal', () => {
      // The positive control for the test above: one internal role among external ones is what
      // makes the user internal, so this must not be satisfiable by an empty-ish list.
      expect(hasOnlyExternalRoles(['Patient', 'IT Admin'])).toBe(false);
      expect(hasOnlyExternalRoles(['Staff Supervisor'])).toBe(false);
    });
  });

  describe('externalUserOnlyMatchGuard', () => {
    it('matches a pure-external user', () => {
      expect(matchWith(externalUserOnlyMatchGuard, { roles: ['Patient'] })).toBe(true);
    });

    it('does not match a user carrying any internal role', () => {
      expect(matchWith(externalUserOnlyMatchGuard, { roles: ['Patient', 'Intake Staff'] })).toBe(
        false,
      );
    });

    it('does not match an anonymous caller', () => {
      expect(matchWith(externalUserOnlyMatchGuard, null)).toBe(false);
      expect(matchWith(externalUserOnlyMatchGuard, { roles: [] })).toBe(false);
      expect(matchWith(externalUserOnlyMatchGuard, {})).toBe(false);
    });
  });

  describe('internalUserOnlyMatchGuard', () => {
    it('matches internal staff', () => {
      expect(matchWith(internalUserOnlyMatchGuard, { roles: ['Staff Supervisor'] })).toBe(true);
    });

    it('matches an anonymous caller, so authGuard can challenge', () => {
      expect(matchWith(internalUserOnlyMatchGuard, null)).toBe(true);
      expect(matchWith(internalUserOnlyMatchGuard, { roles: [] })).toBe(true);
    });

    it('does not match a pure-external user', () => {
      expect(matchWith(internalUserOnlyMatchGuard, { roles: ['Defense Attorney'] })).toBe(false);
    });
  });

  describe('the two guards are exact complements', () => {
    const ROLE_SETS: ReadonlyArray<{ label: string; currentUser: unknown }> = [
      { label: 'pure external', currentUser: { roles: ['Patient'] } },
      { label: 'mixed external and internal', currentUser: { roles: ['Patient', 'IT Admin'] } },
      { label: 'internal only', currentUser: { roles: ['Intake Staff'] } },
      { label: 'host superuser', currentUser: { roles: ['admin'] } },
      { label: 'anonymous, no user', currentUser: null },
      { label: 'authenticated with no roles', currentUser: { roles: [] } },
    ];

    ROLE_SETS.forEach(({ label, currentUser }) => {
      it(`resolves a shared path to exactly one route for: ${label}`, () => {
        const external = matchWith(externalUserOnlyMatchGuard, currentUser);
        const internal = matchWith(internalUserOnlyMatchGuard, currentUser);

        expect(external).not.toBe(internal);
      });
    });
  });
});
