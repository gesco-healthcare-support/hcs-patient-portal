import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

/**
 * Shape of a row in the parent's `appointmentAuthorizedUsers` array.
 * Exported so the parent (and any future consumer) imports the type
 * from the section file; this section owns the type because the table
 * + modal are the only producers.
 */
export type AppointmentAuthorizedUserDraft = {
  id?: string;
  identityUserId: string;
  firstName: string;
  lastName: string;
  email: string;
  userRole: string;
  accessTypeId: number;
};

/**
 * One entry in the lookup the modal's Email dropdown renders. Loaded by
 * the parent via `loadExternalAuthorizedUsers` because two other
 * sections (Applicant Attorney, Defense Attorney) also consume the
 * same lookup -- centralising the fetch at the parent level avoids
 * three duplicate API calls.
 */
export type ExternalAuthorizedUserOption = {
  identityUserId: string;
  firstName: string;
  lastName: string;
  email: string;
  userRole: string;
};

/**
 * #121 phase T2 (2026-05-13) -- "Additional Authorized User" table +
 * modal, extracted from `AppointmentAddComponent`. OLD parity (line
 * reference in original parent: B11 2026-05-06): hidden for Claim
 * Examiner bookers via `shouldShowAuthorizedUserSection` at parent
 * level; the parent decides visibility, the child renders the section
 * when shown.
 *
 * State ownership:
 *   - parent  -> the `appointmentAuthorizedUsers` array (data, used
 *                at submit). Passed in by reference.
 *   - parent  -> `externalAuthorizedUserOptions` lookup. Passed in.
 *   - child   -> modal open/closed flag + create-or-edit mode +
 *                editing index + selected option + the modal-local
 *                FormGroup.
 *
 * Why the list is mutated in place: parent never reactively watches
 * the array; it serializes it once at submit. Passing by reference
 * and letting the child push / splice keeps the data-flow simple,
 * avoids double Output plumbing.
 */
@Component({
  selector: 'app-appointment-add-authorized-users',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './appointment-add-authorized-users.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppointmentAddAuthorizedUsersComponent {
  @Input({ required: true }) users!: AppointmentAuthorizedUserDraft[];
  @Input({ required: true }) userOptions!: ExternalAuthorizedUserOption[];

  /**
   * OLD parity: access-type 23 = View, 24 = Edit. Lives at child level
   * because only this section's table + modal render labels for these
   * values.
   */
  readonly accessTypeOptions = [
    { value: 23, label: 'View' },
    { value: 24, label: 'Edit' },
  ];

  isAuthorizedUserModalOpen = false;
  authorizedUserModalMode: 'create' | 'edit' = 'create';
  editingAuthorizedUserIndex = -1;
  selectedAuthorizedUser: ExternalAuthorizedUserOption | null = null;

  private readonly fb = new FormBuilder();
  readonly authorizedUserForm = this.fb.group({
    identityUserId: [null as string | null, [Validators.required]],
    accessTypeId: [23, [Validators.required]],
  });

  constructor() {
    this.authorizedUserForm.get('identityUserId')?.valueChanges.subscribe((value) => {
      this.onAuthorizedUserIdentityChanged(value);
    });
  }

  openAddAuthorizedUserModal(): void {
    this.authorizedUserModalMode = 'create';
    this.editingAuthorizedUserIndex = -1;
    this.selectedAuthorizedUser = null;
    this.authorizedUserForm.reset({ identityUserId: null, accessTypeId: 23 });
    this.isAuthorizedUserModalOpen = true;
  }

  openEditAuthorizedUserModal(index: number): void {
    const item = this.users[index];
    if (!item) {
      return;
    }

    this.authorizedUserModalMode = 'edit';
    this.editingAuthorizedUserIndex = index;
    this.authorizedUserForm.reset({
      identityUserId: item.identityUserId,
      accessTypeId: item.accessTypeId,
    });
    this.selectedAuthorizedUser =
      this.userOptions.find((x) => x.identityUserId === item.identityUserId) ?? null;
    this.isAuthorizedUserModalOpen = true;
  }

  closeAuthorizedUserModal(): void {
    this.isAuthorizedUserModalOpen = false;
  }

  saveAuthorizedUserFromModal(): void {
    if (this.authorizedUserForm.invalid || !this.selectedAuthorizedUser) {
      this.authorizedUserForm.markAllAsTouched();
      return;
    }

    const raw = this.authorizedUserForm.getRawValue();
    const identityUserId = raw.identityUserId ?? '';
    const accessTypeId = Number(raw.accessTypeId ?? 23);

    const duplicateIndex = this.users.findIndex(
      (x, i) => x.identityUserId === identityUserId && i !== this.editingAuthorizedUserIndex,
    );
    if (duplicateIndex >= 0) {
      return;
    }

    const mapped: AppointmentAuthorizedUserDraft = {
      id:
        this.authorizedUserModalMode === 'edit'
          ? this.users[this.editingAuthorizedUserIndex]?.id
          : undefined,
      identityUserId: this.selectedAuthorizedUser.identityUserId,
      firstName: this.selectedAuthorizedUser.firstName,
      lastName: this.selectedAuthorizedUser.lastName,
      email: this.selectedAuthorizedUser.email,
      userRole: this.selectedAuthorizedUser.userRole,
      accessTypeId,
    };

    if (this.authorizedUserModalMode === 'edit' && this.editingAuthorizedUserIndex >= 0) {
      this.users[this.editingAuthorizedUserIndex] = mapped;
    } else {
      this.users.push(mapped);
    }

    this.closeAuthorizedUserModal();
  }

  removeAuthorizedUser(index: number): void {
    this.users.splice(index, 1);
  }

  getAccessTypeLabel(value: number): string {
    return this.accessTypeOptions.find((x) => x.value === value)?.label ?? '';
  }

  private onAuthorizedUserIdentityChanged(identityUserId: string | null): void {
    this.selectedAuthorizedUser =
      this.userOptions.find((x) => x.identityUserId === identityUserId) ?? null;
  }
}
