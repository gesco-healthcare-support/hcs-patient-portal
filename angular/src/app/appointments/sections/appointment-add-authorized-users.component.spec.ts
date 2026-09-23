import {
  AppointmentAddAuthorizedUsersComponent,
  AppointmentAuthorizedUserDraft,
} from './appointment-add-authorized-users.component';

/**
 * The booking form's "Additional Authorized User" section: the add / edit modal over the parent's
 * `users` array, which the section mutates in place.
 *
 * <p>It had no spec. The component injects nothing -- its FormBuilder is constructed, not
 * injected -- so it is built with `new`, with no TestBed and no template.</p>
 *
 * <p>The rule that matters most here is the email dedup: the email is the accessor's identity key
 * at submit, so two rows with one email would be two grants for one person.</p>
 *
 * <p>All names and emails below are synthetic.</p>
 */
describe('AppointmentAddAuthorizedUsersComponent', () => {
  function row(over: Partial<AppointmentAuthorizedUserDraft> = {}): AppointmentAuthorizedUserDraft {
    return {
      firstName: 'Ada',
      lastName: 'Example',
      email: 'ada@example.test',
      userRole: 'Patient',
      accessTypeId: 23,
      ...over,
    };
  }

  function create(
    users: AppointmentAuthorizedUserDraft[] = [],
  ): AppointmentAddAuthorizedUsersComponent {
    const c = new AppointmentAddAuthorizedUsersComponent();
    c.users = users;
    return c;
  }

  /**
   * Fills the modal with a valid accessor. The names are padded to show the trim; the email is
   * not, because `Validators.email` already rejects surrounding whitespace.
   */
  function fill(
    c: AppointmentAddAuthorizedUsersComponent,
    over: Record<string, unknown> = {},
  ): void {
    c.authorizedUserForm.setValue({
      firstName: '  Grace ',
      lastName: ' Example  ',
      email: 'grace@example.test',
      userRole: 'Claim Examiner',
      accessTypeId: 24,
      ...over,
    } as never);
  }

  describe('opening the modal', () => {
    it('opens a blank create form that defaults to View access', () => {
      const c = create();
      c.authorizedUserForm.patchValue({ email: 'left@example.test' });

      c.openAddAuthorizedUserModal();

      expect(c.isAuthorizedUserModalOpen).toBeTrue();
      expect(c.authorizedUserModalMode).toBe('create');
      expect(c.editingAuthorizedUserIndex).toBe(-1);
      expect(c.authorizedUserForm.getRawValue()).toEqual({
        firstName: '',
        lastName: '',
        email: '',
        userRole: '',
        accessTypeId: 23,
      });
    });

    it('loads the chosen row into the form for an edit', () => {
      const c = create([row(), row({ email: 'grace@example.test', accessTypeId: 24 })]);

      c.openEditAuthorizedUserModal(1);

      expect(c.isAuthorizedUserModalOpen).toBeTrue();
      expect(c.authorizedUserModalMode).toBe('edit');
      expect(c.editingAuthorizedUserIndex).toBe(1);
      expect(c.authorizedUserForm.getRawValue().email).toBe('grace@example.test');
      expect(c.authorizedUserForm.getRawValue().accessTypeId).toBe(24);
    });

    it('does nothing for an index that holds no row', () => {
      const c = create([row()]);
      c.openEditAuthorizedUserModal(5);
      expect(c.isAuthorizedUserModalOpen).toBeFalse();
      expect(c.authorizedUserModalMode).toBe('create');
    });

    it('closes', () => {
      const c = create();
      c.openAddAuthorizedUserModal();
      c.closeAuthorizedUserModal();
      expect(c.isAuthorizedUserModalOpen).toBeFalse();
    });
  });

  describe('saving', () => {
    it('refuses an incomplete form and shows its errors', () => {
      const users: AppointmentAuthorizedUserDraft[] = [];
      const c = create(users);
      c.openAddAuthorizedUserModal();

      c.saveAuthorizedUserFromModal();

      expect(users).toEqual([]);
      expect(c.authorizedUserForm.get('email')!.touched).toBeTrue();
      expect(c.isAuthorizedUserModalOpen).withContext('stays open to be fixed').toBeTrue();
    });

    it('adds a trimmed row and closes the modal', () => {
      const users: AppointmentAuthorizedUserDraft[] = [];
      const c = create(users);
      c.openAddAuthorizedUserModal();
      fill(c);

      c.saveAuthorizedUserFromModal();

      expect(users).toEqual([
        {
          id: undefined,
          identityUserId: undefined,
          firstName: 'Grace',
          lastName: 'Example',
          email: 'grace@example.test',
          userRole: 'Claim Examiner',
          accessTypeId: 24,
        },
      ]);
      expect(c.isAuthorizedUserModalOpen).toBeFalse();
    });

    it('stores the access type as a number whatever shape the select gives it', () => {
      const users: AppointmentAuthorizedUserDraft[] = [];
      const c = create(users);
      c.openAddAuthorizedUserModal();
      fill(c, { accessTypeId: '24' });

      c.saveAuthorizedUserFromModal();

      expect(users[0].accessTypeId).toBe(24);
    });

    it('refuses a second row for the same email, whatever its case', () => {
      const users = [row()];
      const c = create(users);
      c.openAddAuthorizedUserModal();
      fill(c, { email: 'ADA@Example.Test' });

      c.saveAuthorizedUserFromModal();

      expect(users).toHaveSize(1);
      expect(c.isAuthorizedUserModalOpen).toBeTrue();
    });

    it('replaces an edited row in place, keeping its stored ids', () => {
      const users = [
        row({ id: 'acc-1', identityUserId: 'user-1' }),
        row({ email: 'b@example.test' }),
      ];
      const c = create(users);
      c.openEditAuthorizedUserModal(0);
      c.authorizedUserForm.patchValue({ userRole: 'Defense Attorney' });

      c.saveAuthorizedUserFromModal();

      expect(users).toHaveSize(2);
      expect(users[0].id).toBe('acc-1');
      expect(users[0].identityUserId).toBe('user-1');
      expect(users[0].userRole).toBe('Defense Attorney');
      expect(users[0].email)
        .withContext('a row keeping its own email is not a duplicate of itself')
        .toBe('ada@example.test');
    });
  });

  it('removes the row at an index', () => {
    const users = [row(), row({ email: 'b@example.test' })];
    create(users).removeAuthorizedUser(0);
    expect(users.map((u) => u.email)).toEqual(['b@example.test']);
  });

  it('names each access type, and nothing for an unknown one', () => {
    const c = create();
    expect(c.getAccessTypeLabel(23)).toBe('View');
    expect(c.getAccessTypeLabel(24)).toBe('Edit');
    expect(c.getAccessTypeLabel(99)).toBe('');
  });

  it('offers the four external roles the server grants', () => {
    expect(create().roleOptions).toEqual([
      'Patient',
      'Applicant Attorney',
      'Defense Attorney',
      'Claim Examiner',
    ]);
  });
});
