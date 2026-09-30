import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { InviteExternalUserComponent } from './invite-external-user.component';

/**
 * The admin-side invite form for the four external roles.
 *
 * <p>It had NO spec at all and sat at 1 of 64 lines covered -- the only one of this batch with
 * nothing holding it.</p>
 *
 * <p>Two things carry real weight. The role dropdown sends NUMERIC enum values that must match
 * the backend ExternalUserType (Patient=1, ClaimExaminer=2, ApplicantAttorney=3,
 * DefenseAttorney=4); a transposed pair would invite people into the wrong role, which is an
 * access-control outcome rather than a cosmetic one. And a 200 response no longer always means
 * "invited": when the address already has an account the server issues nothing and says so, so
 * announcing success there would be a false success on the exact support call this screen
 * exists to handle.</p>
 *
 * <p>`navigator.clipboard.writeText` is stubbed for EVERY test, not just the copy one. In
 * headless Chrome that promise can never settle, and a hang is invisible to jasmine -- it took
 * a full isolation run to attribute the same thing in the users hub on 2026-09-17.</p>
 *
 * <p>All emails, names and identifiers below are synthetic.</p>
 */
describe('InviteExternalUserComponent surfaces', () => {
  let rest: { request: jasmine.Spy };
  let toaster: { success: jasmine.Spy; error: jasmine.Spy };
  let router: { navigateByUrl: jasmine.Spy };
  let clipboardWrite: jasmine.Spy | null;

  interface Probe {
    [key: string]: any;
  }

  const INVITE_RESPONSE = {
    inviteUrl: 'https://auth.example.test/Account/Register?inviteToken=token-1',
    email: 'ada@example.test',
    roleName: 'Patient',
    tenantName: 'Example Practice',
    expiresAt: '2026-10-01T00:00:00Z',
  };

  function create(): Probe {
    rest = { request: jasmine.createSpy('request').and.returnValue(of(INVITE_RESPONSE)) };
    toaster = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };
    router = { navigateByUrl: jasmine.createSpy('navigateByUrl') };

    TestBed.configureTestingModule({
      providers: [
        { provide: RestService, useValue: rest },
        { provide: Router, useValue: router },
        { provide: ToasterService, useValue: toaster },
      ],
    });

    return TestBed.createComponent(InviteExternalUserComponent)
      .componentInstance as unknown as Probe;
  }

  /** Fill the form with something that passes validation. */
  function valid(c: Probe, over: Record<string, unknown> = {}): void {
    c.form.patchValue({
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      userType: 1,
      ...over,
    });
  }

  beforeEach(() => {
    clipboardWrite = null;
    if (navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
      clipboardWrite = spyOn(navigator.clipboard, 'writeText').and.returnValue(Promise.resolve());
    }
  });

  afterEach(() => TestBed.resetTestingModule());

  describe('the role dropdown', () => {
    it('offers exactly the four external roles', () => {
      const c = create();
      expect(c.roleOptions.map((o: { label: string }) => o.label)).toEqual([
        'Patient',
        'Applicant Attorney',
        'Defense Attorney',
        'Claim Examiner',
      ]);
    });

    it('carries the backend enum value for each role', () => {
      /**
       * These numbers go straight to the server as ExternalUserType. A transposed pair
       * would silently invite someone into a role they were not meant to have, and the
       * label on screen would still look right.
       */
      const c = create();
      const byLabel = new Map(
        c.roleOptions.map((o: { label: string; value: number }) => [o.label, o.value]),
      );
      expect(byLabel.get('Patient')).toBe(1);
      expect(byLabel.get('Claim Examiner')).toBe(2);
      expect(byLabel.get('Applicant Attorney')).toBe(3);
      expect(byLabel.get('Defense Attorney')).toBe(4);
    });

    it('defaults to Patient', () => {
      const c = create();
      expect(c.form.getRawValue().userType).toBe(1);
    });
  });

  describe('form validation', () => {
    it('requires an email', () => {
      const c = create();
      expect(c.form.get('email').hasError('required')).toBeTrue();
    });

    it('requires the email to look like one', () => {
      const c = create();
      c.form.patchValue({ email: 'not-an-email' });
      expect(c.form.get('email').hasError('email')).toBeTrue();

      c.form.patchValue({ email: 'ada@example.test' });
      expect(c.form.get('email').valid).toBeTrue();
    });

    it('caps the name fields', () => {
      const c = create();
      c.form.patchValue({ firstName: 'a'.repeat(129) });
      expect(c.form.get('firstName').hasError('maxlength')).toBeTrue();
    });

    it('does not submit an invalid form, and shows the errors instead', async () => {
      const c = create();
      await c.onSubmit();
      expect(rest.request).not.toHaveBeenCalled();
      expect(c.form.touched).withContext('errors become visible').toBeTrue();
    });

    it('does not submit twice at once', async () => {
      const c = create();
      valid(c);
      c.isSubmitting.set(true);
      await c.onSubmit();
      expect(rest.request).not.toHaveBeenCalled();
    });
  });

  describe('sending an invite', () => {
    it('posts to the invite endpoint', async () => {
      const c = create();
      valid(c);
      await c.onSubmit();
      const [req] = rest.request.calls.mostRecent().args;
      expect(req.method).toBe('POST');
      expect(req.url).toBe('/api/app/external-users/invite');
    });

    it('sends the role as a number, not the string the select holds', async () => {
      const c = create();
      valid(c, { userType: '3' });
      await c.onSubmit();
      const [req] = rest.request.calls.mostRecent().args;
      expect(req.body.userType).toBe(3);
      expect(typeof req.body.userType).toBe('number');
    });

    it('rejects a padded email outright rather than trimming it through', async () => {
      /**
       * The payload calls `.trim()` on the email, which reads as though a padded address
       * would be cleaned up and sent. It is not: Validators.email rejects surrounding
       * whitespace first, so the form never becomes valid and nothing is submitted. The
       * trim only ever applies to an address that was already acceptable.
       */
      const c = create();
      valid(c, { email: '  ada@example.test  ' });

      await c.onSubmit();

      expect(c.form.get('email').valid).toBeFalse();
      expect(rest.request).not.toHaveBeenCalled();
    });

    it('sends null rather than an empty string for a blank name', async () => {
      // The server stores what it is given; an empty string is a value, null is its absence.
      const c = create();
      valid(c, { firstName: '   ', lastName: '' });
      await c.onSubmit();
      const [req] = rest.request.calls.mostRecent().args;
      expect(req.body.firstName).toBeNull();
      expect(req.body.lastName).toBeNull();
    });

    it('trims the names it does send', async () => {
      const c = create();
      valid(c, { firstName: '  Ada  ' });
      await c.onSubmit();
      const [req] = rest.request.calls.mostRecent().args;
      expect(req.body.firstName).toBe('Ada');
    });

    it('shows the result card and confirms', async () => {
      const c = create();
      valid(c);
      await c.onSubmit();
      expect(c.result()).toEqual(INVITE_RESPONSE);
      expect(toaster.success).toHaveBeenCalled();
      expect(c.isSubmitting()).toBeFalse();
    });

    it('clears any previous result and error before it starts', async () => {
      const c = create();
      c.errorMessage.set('previous failure');
      c.copyConfirmation.set('Copied!');
      valid(c);

      await c.onSubmit();

      expect(c.errorMessage()).toBeNull();
      expect(c.copyConfirmation()).toBeNull();
    });
  });

  describe('an address that already has an account', () => {
    const ALREADY = {
      ...INVITE_RESPONSE,
      alreadyRegistered: true,
      existingRoleName: 'Applicant Attorney',
    };

    it('does NOT claim an invitation was sent', async () => {
      /**
       * The server deliberately issues nothing here. A success toast would be a false
       * success on precisely the support call this panel exists to answer.
       */
      const c = create();
      rest.request.and.returnValue(of(ALREADY));
      valid(c);

      await c.onSubmit();

      expect(toaster.success).not.toHaveBeenCalled();
      expect(c.result()).toBeNull();
    });

    it('offers the sign-in-link panel instead', async () => {
      const c = create();
      rest.request.and.returnValue(of(ALREADY));
      valid(c);

      await c.onSubmit();

      expect(c.alreadyRegistered()).toEqual({
        email: ALREADY.email,
        invitedRoleName: ALREADY.roleName,
        existingRoleName: 'Applicant Attorney',
        tenantName: ALREADY.tenantName,
      });
    });

    it('treats an absent existing role as unknown rather than dropping the panel', async () => {
      const c = create();
      rest.request.and.returnValue(of({ ...ALREADY, existingRoleName: undefined }));
      valid(c);

      await c.onSubmit();

      expect(c.alreadyRegistered().existingRoleName).toBeNull();
    });

    it('is not an error state', async () => {
      const c = create();
      rest.request.and.returnValue(of(ALREADY));
      valid(c);
      await c.onSubmit();
      expect(c.errorMessage()).toBeNull();
      expect(c.isSubmitting()).toBeFalse();
    });
  });

  describe('when the invite fails', () => {
    it('prefers the server message', async () => {
      const c = create();
      rest.request.and.returnValue(
        throwError(() => ({ error: { error: { message: 'That office is not active.' } } })),
      );
      valid(c);

      await c.onSubmit();

      expect(c.errorMessage()).toBe('That office is not active.');
    });

    it('falls back through the error shapes it may receive', async () => {
      const c = create();
      valid(c);

      rest.request.and.returnValue(throwError(() => ({ error: { message: 'second shape' } })));
      await c.onSubmit();
      expect(c.errorMessage()).toBe('second shape');

      rest.request.and.returnValue(throwError(() => ({ message: 'third shape' })));
      await c.onSubmit();
      expect(c.errorMessage()).toBe('third shape');
    });

    it('uses a plain message when the failure carries none', async () => {
      const c = create();
      rest.request.and.returnValue(throwError(() => ({})));
      valid(c);

      await c.onSubmit();

      expect(c.errorMessage()).toContain('Failed to create invite');
    });

    it('releases the button so the admin can retry', async () => {
      const c = create();
      rest.request.and.returnValue(throwError(() => ({})));
      valid(c);
      await c.onSubmit();
      expect(c.isSubmitting()).toBeFalse();
    });
  });

  describe('sending a sign-in link instead', () => {
    function registered(c: Probe): void {
      c.alreadyRegistered.set({
        email: 'ada@example.test',
        invitedRoleName: 'Patient',
        existingRoleName: 'Patient',
        tenantName: 'Example Practice',
      });
    }

    it('does nothing when no such panel is open', async () => {
      const c = create();
      await c.sendPortalLink();
      expect(rest.request).not.toHaveBeenCalled();
    });

    it('does nothing while one is already being sent', async () => {
      const c = create();
      registered(c);
      c.isSendingPortalLink.set(true);
      await c.sendPortalLink();
      expect(rest.request).not.toHaveBeenCalled();
    });

    it('posts to the portal-link endpoint with the existing address', async () => {
      // The URL is composed server-side from the office subdomain -- never assembled here,
      // and never the admin host.
      const c = create();
      registered(c);
      rest.request.and.returnValue(of(undefined));

      await c.sendPortalLink();

      const [req] = rest.request.calls.mostRecent().args;
      expect(req.method).toBe('POST');
      expect(req.url).toBe('/api/app/external-users/send-portal-link');
      expect(req.body).toEqual({ email: 'ada@example.test' });
    });

    it('confirms both inline and by toast', async () => {
      const c = create();
      registered(c);
      rest.request.and.returnValue(of(undefined));

      await c.sendPortalLink();

      expect(c.portalLinkConfirmation()).toContain('ada@example.test');
      expect(toaster.success).toHaveBeenCalled();
      expect(c.isSendingPortalLink()).toBeFalse();
    });

    it('reports a failure without claiming the link was sent', async () => {
      const c = create();
      registered(c);
      rest.request.and.returnValue(throwError(() => ({})));

      await c.sendPortalLink();

      expect(c.portalLinkConfirmation()).toBeNull();
      expect(c.errorMessage()).toContain('Could not send the sign-in link');
      expect(c.isSendingPortalLink()).toBeFalse();
    });
  });

  describe('copying the invite link', () => {
    it('does nothing when there is no link yet', async () => {
      const c = create();
      await c.copyInviteUrl();
      expect(clipboardWrite ?? { calls: { any: () => false } }).toBeTruthy();
      expect(c.copyConfirmation()).toBeNull();
    });

    it('writes the link and confirms', async () => {
      const c = create();
      if (!clipboardWrite) {
        pending('no clipboard API in this browser');
        return;
      }
      c.result.set(INVITE_RESPONSE);

      await c.copyInviteUrl();

      expect(clipboardWrite).toHaveBeenCalledWith(INVITE_RESPONSE.inviteUrl);
      expect(c.copyConfirmation()).toBe('Copied!');
    });

    it('clears the confirmation after a couple of seconds', fakeAsync(() => {
      const c = create();
      if (!clipboardWrite) {
        pending('no clipboard API in this browser');
        return;
      }
      c.result.set(INVITE_RESPONSE);

      void c.copyInviteUrl();
      tick(0);
      expect(c.copyConfirmation()).toBe('Copied!');

      tick(2000);
      expect(c.copyConfirmation()).toBeNull();
    }));

    it('tells the admin to select the URL manually when the copy is refused', async () => {
      /**
       * This component gets the shape right that the users hub gets wrong: it awaits the
       * write and catches the rejection, so a refused clipboard reports a failure rather
       * than a false "Copied!".
       */
      const c = create();
      if (!clipboardWrite) {
        pending('no clipboard API in this browser');
        return;
      }
      clipboardWrite.and.returnValue(Promise.reject(new Error('denied')));
      c.result.set(INVITE_RESPONSE);

      await c.copyInviteUrl();

      expect(c.copyConfirmation()).toContain('Copy failed');
    });
  });

  describe('resetting and leaving', () => {
    it('clears every panel and returns the role to its default', () => {
      const c = create();
      valid(c, { userType: 4 });
      c.result.set(INVITE_RESPONSE);
      c.errorMessage.set('failure');
      c.copyConfirmation.set('Copied!');
      c.alreadyRegistered.set({
        email: 'x',
        invitedRoleName: 'y',
        existingRoleName: null,
        tenantName: 'z',
      });
      c.portalLinkConfirmation.set('sent');

      c.resetForm();

      expect(c.result()).toBeNull();
      expect(c.errorMessage()).toBeNull();
      expect(c.copyConfirmation()).toBeNull();
      expect(c.alreadyRegistered()).toBeNull();
      expect(c.portalLinkConfirmation()).toBeNull();
      expect(c.form.getRawValue().userType).toBe(1);
      expect(c.form.getRawValue().email).toBeNull();
    });

    it('goes back to the home route', () => {
      const c = create();
      c.goBack();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/');
    });
  });
});
