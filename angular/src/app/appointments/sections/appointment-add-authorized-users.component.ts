import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { LocalizationPipe } from '@abp/ng.core';
import { authorizedUserRoleLabel } from '../../shared/auth/external-user-roles';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

/**
 * Shape of a row in the parent's `appointmentAuthorizedUsers` array.
 * Group J (2026-06-05): accessors are added by free-typed name + email
 * + role, so a draft no longer carries a pre-resolved identityUserId --
 * the server resolves the email to a user (or provisions + invites one)
 * at submit. `identityUserId` stays optional only so already-persisted
 * rows loaded from the API can still round-trip their id for edit/delete.
 */
export type AppointmentAuthorizedUserDraft = {
  id?: string;
  identityUserId?: string;
  firstName: string;
  lastName: string;
  email: string;
  userRole: string;
  accessTypeId: number;
};

/**
 * One entry in the external-user lookup. Still produced by the parent's
 * `loadExternalAuthorizedUsers` because the Applicant/Defense Attorney
 * sections consume it; the accessor section no longer uses it (accessors
 * are now free-typed by email, not picked from this list).
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
 * Group J (2026-06-05): the email picker became free-text -- you type
 * any name + email + role + rights and the server resolves or invites
 * the person. State ownership is unchanged:
 *   - parent  -> the `appointmentAuthorizedUsers` array (data, used at
 *                submit). Passed in by reference; the child push/splices.
 *   - child   -> modal open/closed flag + create-or-edit mode + editing
 *                index + the modal-local FormGroup.
 */
@Component({
  selector: 'app-appointment-add-authorized-users',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, LocalizationPipe],
  templateUrl: './appointment-add-authorized-users.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppointmentAddAuthorizedUsersComponent {
  @Input({ required: true }) users!: AppointmentAuthorizedUserDraft[];

  /**
   * OLD parity: access-type 23 = View, 24 = Edit. Lives at child level
   * because only this section's table + modal render labels for these
   * values.
   */
  readonly accessTypeOptions = [
    { value: 23, label: 'View' },
    { value: 24, label: 'Edit' },
  ];

  /**
   * Valid accessor roles. The server grants/validates this role on the
   * accessor's identity (AppointmentAccessorManager.CreateOrLinkAsync),
   * so the values must match the seeded external role names. Order
   * mirrors the external-user invite dropdown for consistency.
   */
  @Input() roleOptions: readonly string[] = [
    'Patient',
    'Applicant Attorney',
    'Defense Attorney',
    'Claim Examiner',
  ];

  /** External non-patient bookers: offer "Add me as an Authorized User". */
  @Input() showBookerFollows = false;
  @Input() bookerFollows = true;
  /** Forced off while the lawyer's "send my notifications to my paralegal instead" is ticked. */
  @Input() bookerFollowsDisabled = false;
  @Output() bookerFollowsChange = new EventEmitter<boolean>();

  /** True for an external booker: a claim examiner's helper is then labelled an assistant. */
  @Input() externalBooker = false;

  roleLabel(role: string): string {
    return authorizedUserRoleLabel(role, this.externalBooker);
  }

  isAuthorizedUserModalOpen = false;
  authorizedUserModalMode: 'create' | 'edit' = 'create';
  editingAuthorizedUserIndex = -1;

  private readonly fb = new FormBuilder();
  readonly authorizedUserForm = this.fb.group({
    firstName: ['', [Validators.maxLength(64)]],
    lastName: ['', [Validators.maxLength(64)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    userRole: ['', [Validators.required]],
    accessTypeId: [23, [Validators.required]],
  });

  openAddAuthorizedUserModal(): void {
    this.authorizedUserModalMode = 'create';
    this.editingAuthorizedUserIndex = -1;
    this.authorizedUserForm.reset({
      firstName: '',
      lastName: '',
      email: '',
      userRole: '',
      accessTypeId: 23,
    });
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
      firstName: item.firstName,
      lastName: item.lastName,
      email: item.email,
      userRole: item.userRole,
      accessTypeId: item.accessTypeId,
    });
    this.isAuthorizedUserModalOpen = true;
  }

  closeAuthorizedUserModal(): void {
    this.isAuthorizedUserModalOpen = false;
  }

  saveAuthorizedUserFromModal(): void {
    if (this.authorizedUserForm.invalid) {
      this.authorizedUserForm.markAllAsTouched();
      return;
    }

    const raw = this.authorizedUserForm.getRawValue();
    const email = (raw.email ?? '').trim();
    const accessTypeId = Number(raw.accessTypeId ?? 23);

    // Dedup by email -- the email is now the accessor's identity key.
    const duplicateIndex = this.users.findIndex(
      (x, i) =>
        x.email.toLowerCase() === email.toLowerCase() && i !== this.editingAuthorizedUserIndex,
    );
    if (duplicateIndex >= 0) {
      return;
    }

    const existing =
      this.authorizedUserModalMode === 'edit'
        ? this.users[this.editingAuthorizedUserIndex]
        : undefined;

    const mapped: AppointmentAuthorizedUserDraft = {
      id: existing?.id,
      identityUserId: existing?.identityUserId,
      firstName: (raw.firstName ?? '').trim(),
      lastName: (raw.lastName ?? '').trim(),
      email,
      userRole: raw.userRole ?? '',
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
}
