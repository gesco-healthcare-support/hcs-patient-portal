import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { FormBuilder } from '@angular/forms';
import { Observable, of, throwError } from 'rxjs';
import { ConfigStateService, RestService } from '@abp/ng.core';

import { InternalUsersFormComponent } from './internal-users-form.component';

/**
 * The internal-user create form: submitting, what a failure says, resetting, and leaving.
 *
 * <p>`internal-users-form-init.spec.ts` covers loading the tenant list; this is the rest of the
 * component, with the same injection-context harness.</p>
 *
 * <p>All names, emails and identifiers below are synthetic.</p>
 */
describe('InternalUsersFormComponent submitting', () => {
  let request: jasmine.Spy;
  let navigateByUrl: jasmine.Spy;
  let postResponse: () => Observable<unknown>;

  interface Probe {
    [key: string]: any;
  }

  const CREATED = {
    userId: 'u-1',
    email: 'ada@example.test',
    firstName: 'Ada',
    lastName: 'Example',
    roleName: 'Intake Staff',
    tenantName: 'Example Practice',
    welcomeEmailQueued: true,
  };

  function create(options: { tenants?: { id: string; displayName: string }[] } = {}): Probe {
    postResponse = () => of(CREATED);
    request = jasmine
      .createSpy('request')
      .and.callFake((req: { method: string }) =>
        req.method === 'POST' ? postResponse() : of({ items: options.tenants ?? [] }),
      );
    navigateByUrl = jasmine.createSpy('navigateByUrl');
    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        { provide: Router, useValue: { navigateByUrl } },
        { provide: ConfigStateService, useValue: { getOne: () => null } },
        { provide: RestService, useValue: { request } },
      ],
    });
    return TestBed.runInInjectionContext(
      () => new InternalUsersFormComponent(),
    ) as unknown as Probe;
  }

  /**
   * A valid user. The names are padded to show the trim; the email is not, because
   * `Validators.email` already rejects surrounding whitespace.
   */
  function fillValid(c: Probe): void {
    c.form.setValue({
      tenantId: 't-1',
      email: 'ada@example.test',
      firstName: ' Ada',
      lastName: 'Example ',
      roleName: 'Intake Staff',
      phoneNumber: '  ',
    });
  }

  const posts = () =>
    request.calls
      .allArgs()
      .map((a) => a[0])
      .filter((r: { method: string }) => r.method === 'POST');

  afterEach(() => TestBed.resetTestingModule());

  it('preselects the only tenant a host admin can pick', async () => {
    const c = create({ tenants: [{ id: 't-9', displayName: 'Example Practice' }] });
    // ngOnInit discards this promise on purpose (see the component); awaiting it directly is
    // what lets the test observe the result without guessing how many ticks it takes.
    await c.loadTenants();
    expect(c.form.getRawValue().tenantId).toBe('t-9');
  });

  it('refuses an incomplete form and marks it', async () => {
    const c = create();

    await c.onSubmit();

    expect(posts()).toEqual([]);
    expect(c.form.get('email').touched).toBeTrue();
  });

  it('refuses a second submit while one is in flight', async () => {
    const c = create();
    fillValid(c);
    c.isSubmitting.set(true);

    await c.onSubmit();

    expect(posts()).toEqual([]);
  });

  it('posts the trimmed user, sending a blank phone as null', async () => {
    const c = create();
    fillValid(c);

    await c.onSubmit();

    expect(posts()[0].url).toBe('/api/app/internal-users');
    expect(posts()[0].body).toEqual({
      tenantId: 't-1',
      email: 'ada@example.test',
      firstName: 'Ada',
      lastName: 'Example',
      roleName: 'Intake Staff',
      phoneNumber: null,
    });
  });

  it('shows the created user and clears only the per-user fields', async () => {
    const c = create();
    fillValid(c);

    await c.onSubmit();

    expect(c.result()).toEqual({
      email: 'ada@example.test',
      firstName: 'Ada',
      lastName: 'Example',
      roleName: 'Intake Staff',
      tenantName: 'Example Practice',
      welcomeEmailQueued: true,
    });
    expect(c.form.getRawValue().email).toBe('');
    expect(c.form.getRawValue().tenantId).withContext('kept for the next user').toBe('t-1');
    expect(c.isSubmitting()).toBeFalse();
  });

  describe('a failed submit', () => {
    for (const { label, error, message } of [
      {
        label: "the server's own message",
        error: { error: { error: { message: 'Email already in use.' } } },
        message: 'Email already in use.',
      },
      {
        label: 'a top-level error message',
        error: { error: { message: 'Bad request.' } },
        message: 'Bad request.',
      },
      {
        label: 'the transport message',
        error: { message: 'Network down.' },
        message: 'Network down.',
      },
      {
        label: 'a generic line when nothing says why',
        error: {},
        message: 'Could not create the user. Please try again.',
      },
    ]) {
      it(`shows ${label}`, async () => {
        const c = create();
        fillValid(c);
        postResponse = () => throwError(() => error);

        await c.onSubmit();

        expect(c.errorMessage()).toBe(message);
        expect(c.result()).toBeNull();
        expect(c.isSubmitting()).toBeFalse();
      });
    }
  });

  describe('resetting', () => {
    it('clears the form, the result and the error', () => {
      const c = create();
      fillValid(c);
      c.result.set({ ...CREATED });
      c.errorMessage.set('Old error');

      c.resetForm();

      expect(c.form.getRawValue().email).toBe('');
      expect(c.form.getRawValue().tenantId).toBe('');
      expect(c.result()).toBeNull();
      expect(c.errorMessage()).toBeNull();
    });

    it('keeps the only tenant selected', () => {
      const c = create();
      c.tenants.set([{ id: 't-9', displayName: 'Example Practice' }]);
      c.resetForm();
      expect(c.form.getRawValue().tenantId).toBe('t-9');
    });

    it("keeps a tenant admin's tenant locked", () => {
      const c = create();
      c.tenantLocked.set(true);
      c.resetForm();
      expect(c.form.get('tenantId').disabled).toBeTrue();
    });
  });

  it('goes back to the home page', () => {
    const c = create();
    c.goBack();
    expect(navigateByUrl).toHaveBeenCalledWith('/');
  });
});
