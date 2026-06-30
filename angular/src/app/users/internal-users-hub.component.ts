import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ImpersonationService } from '@volo/abp.commercial.ng.ui/config';
import { finalize } from 'rxjs/operators';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { OfficeNamePipe } from '../shared/pipes/office-name.pipe';
import { ExternalUserType } from '../proxy/external-signups/external-user-type.enum';
import { InvitationStatus } from '../proxy/invitations/invitation-status.enum';
import type { InvitationDto, InviteExternalUserResultDto } from '../proxy/external-signups/models';
import type { InternalUserCreatedDto } from '../proxy/internal-users/models';
import {
  avatarColor,
  CREATABLE_INTERNAL_ROLES,
  expiryChip,
  initials,
  INVITE_ROLE_OPTIONS,
  invitationStatusChip,
  isAttorneyType,
  roleChipClass,
  USERS_SECTIONS,
  UsersSection,
  UsersSectionKey,
  userTypeFromName,
} from './users-hub.util';
import {
  InternalUserRow,
  TenantFormState,
  TenantRow,
  UsersSectionGateway,
} from './users-section.gateway';

interface InviteDraft {
  firstName: string;
  lastName: string;
  email: string;
  userType: ExternalUserType;
  firmName: string;
}

interface CreateUserDraft {
  roleName: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
}

const EMAIL_RE = /.+@.+\..+/;

/**
 * Users & Access hub (Prompt 16). One standalone component mounted at the four
 * `/users/*` section routes; it reads `data.section` to know which surface to
 * render, so the left rail is real routerLinks (deep-linkable + per-route
 * guarded). Sections: Invite External, Pending Invites, Internal Users, Tenants.
 * Mirrors the Configuration hub pattern (signals + route-data section + rail
 * gated by granted policy). Replaces the legacy invite + internal-users forms.
 */
@Component({
  selector: 'app-internal-users-hub',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, RouterLink, IconComponent, OfficeNamePipe],
  templateUrl: './internal-users-hub.component.html',
})
export class InternalUsersHubComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly gateway = inject(UsersSectionGateway);
  private readonly permission = inject(PermissionService);
  private readonly toaster = inject(ToasterService);
  private readonly impersonation = inject(ImpersonationService);

  protected readonly sections = USERS_SECTIONS;
  protected readonly roleOptions = INVITE_ROLE_OPTIONS;
  protected readonly creatableRoles = CREATABLE_INTERNAL_ROLES;
  protected readonly roleChipClass = roleChipClass;
  protected readonly initials = initials;
  protected readonly avatarColor = avatarColor;

  protected readonly section = signal<UsersSectionKey>('invite');
  protected readonly meta = computed<UsersSection>(
    () => this.sections.find((s) => s.key === this.section()) ?? this.sections[0],
  );
  protected readonly loading = signal(true);
  protected readonly isBusy = signal(false);

  // Invite External
  protected readonly invite = signal<InviteDraft>(this.emptyInvite());
  protected readonly inviteResult = signal<InviteExternalUserResultDto | null>(null);
  protected readonly showFirm = computed(() => isAttorneyType(this.invite().userType));

  // Pending Invites
  protected readonly invites = signal<InvitationDto[]>([]);
  protected readonly pendingFilter = signal('');

  // Internal Users
  protected readonly internalUsers = signal<InternalUserRow[]>([]);
  protected readonly createForm = signal<CreateUserDraft | null>(null);
  protected readonly createResult = signal<InternalUserCreatedDto | null>(null);

  // Tenants
  protected readonly tenants = signal<TenantRow[]>([]);
  protected readonly tenantForm = signal<TenantFormState | null>(null);
  protected readonly editionOptions = signal<{ id: string; name: string }[]>([]);

  constructor() {
    this.route.data.pipe(takeUntilDestroyed()).subscribe((data) => {
      this.section.set((data['section'] as UsersSectionKey) ?? 'invite');
      this.closeModals();
      this.load();
    });
  }

  protected canSee(section: UsersSection): boolean {
    return this.permission.getGrantedPolicy(section.policy);
  }

  /** Creating a tenant needs Saas.Tenants.Create (IT-Admin-only); hide the
   *  button otherwise so non-IT-Admin roles never click into a 403. */
  protected canCreateTenant(): boolean {
    return this.permission.getGrantedPolicy('Saas.Tenants.Create');
  }

  private closeModals(): void {
    this.createForm.set(null);
    this.tenantForm.set(null);
  }

  private load(): void {
    const key = this.section();
    if (key === 'invite') {
      this.applyInvitePrefill();
      this.loading.set(false);
      return;
    }
    this.loading.set(true);
    if (key === 'pending') {
      this.gateway
        .listInvites(this.pendingFilter())
        .pipe(finalize(() => this.loading.set(false)))
        .subscribe({ next: (r) => this.invites.set(r), error: () => this.invites.set([]) });
    } else if (key === 'staff') {
      this.gateway
        .listInternalUsers()
        .pipe(finalize(() => this.loading.set(false)))
        .subscribe({
          next: (r) => this.internalUsers.set(r),
          error: () => this.internalUsers.set([]),
        });
    } else {
      this.gateway
        .listTenants()
        .pipe(finalize(() => this.loading.set(false)))
        .subscribe({ next: (r) => this.tenants.set(r), error: () => this.tenants.set([]) });
    }
  }

  // ---- Invite External ----
  private emptyInvite(): InviteDraft {
    return {
      firstName: '',
      lastName: '',
      email: '',
      userType: ExternalUserType.Patient,
      firmName: '',
    };
  }
  private applyInvitePrefill(): void {
    const q = this.route.snapshot.queryParamMap;
    const email = q.get('email');
    const userType = q.get('userType');
    if (email || userType) {
      this.invite.set({
        ...this.emptyInvite(),
        email: email ?? '',
        userType: userTypeFromName(userType),
      });
      this.inviteResult.set(null);
    }
  }
  protected patchInvite(partial: Partial<InviteDraft>): void {
    this.invite.set({ ...this.invite(), ...partial });
  }
  protected resetInvite(): void {
    this.invite.set(this.emptyInvite());
    this.inviteResult.set(null);
  }
  protected sendInvite(): void {
    const form = this.invite();
    if (this.isBusy()) {
      return;
    }
    if (!EMAIL_RE.test(form.email.trim())) {
      this.toaster.warn('A valid email is required.');
      return;
    }
    const isFirm = this.showFirm();
    const firmName = isFirm && form.firmName.trim() ? form.firmName.trim() : undefined;
    this.isBusy.set(true);
    this.gateway
      .sendInvite({
        email: form.email.trim(),
        // F-005 (2026-06-25): send First/Last for ALL roles (incl. attorneys) so
        // the invitation email can greet the invitee by name. Firm name is sent
        // only for the attorney roles (showFirm()).
        firstName: form.firstName.trim() || undefined,
        lastName: form.lastName.trim() || undefined,
        userType: form.userType,
        firmName,
      })
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: (r) => {
          this.inviteResult.set(r);
          this.toaster.success('Invite sent.');
        },
        error: () => undefined,
      });
  }
  protected copy(text: string | null | undefined): void {
    if (text && navigator.clipboard) {
      navigator.clipboard.writeText(text);
      this.toaster.success('Copied to clipboard.');
    }
  }

  // ---- Pending Invites ----
  protected applyPendingFilter(value: string): void {
    this.pendingFilter.set(value);
    this.load();
  }
  protected expiry(invitation: InvitationDto) {
    return expiryChip(
      invitation.expiresAt,
      invitation.status ?? InvitationStatus.Pending,
      Date.now(),
    );
  }
  protected status(invitation: InvitationDto) {
    return invitationStatusChip(invitation.status ?? InvitationStatus.Pending);
  }
  /** Resend / revoke are only meaningful while the invite is not yet accepted. */
  protected canManageInvite(invitation: InvitationDto): boolean {
    return (invitation.status ?? InvitationStatus.Pending) !== InvitationStatus.Accepted;
  }
  protected isPending(invitation: InvitationDto): boolean {
    return (invitation.status ?? InvitationStatus.Pending) === InvitationStatus.Pending;
  }
  protected resend(invitation: InvitationDto): void {
    if (this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.gateway
      .resendInvite(invitation.id ?? '')
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: (r) => {
          this.copy(r.inviteUrl);
          this.toaster.success('Invite re-sent; fresh link copied.');
          this.load();
        },
        error: () => undefined,
      });
  }
  protected revoke(invitation: InvitationDto): void {
    if (this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.gateway
      .revokeInvite(invitation.id ?? '')
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Invite revoked.');
          this.load();
        },
        error: () => undefined,
      });
  }

  // ---- Internal Users ----
  protected openCreateUser(): void {
    this.createResult.set(null);
    this.createForm.set({
      roleName: 'Intake Staff',
      firstName: '',
      lastName: '',
      email: '',
      phoneNumber: '',
    });
  }
  protected patchCreate(partial: Partial<CreateUserDraft>): void {
    const current = this.createForm();
    if (current) {
      this.createForm.set({ ...current, ...partial });
    }
  }
  protected closeCreateUser(): void {
    if (!this.isBusy()) {
      this.createForm.set(null);
    }
  }
  protected saveCreateUser(): void {
    const form = this.createForm();
    if (!form || this.isBusy()) {
      return;
    }
    if (!form.firstName.trim() || !form.lastName.trim() || !EMAIL_RE.test(form.email.trim())) {
      this.toaster.warn('First name, last name, and a valid email are required.');
      return;
    }
    this.isBusy.set(true);
    this.gateway
      .createInternalUser({
        // tenantId is intentionally omitted: internal operators are HOST logins (the
        // app service forces CurrentTenant.Change(null)); office access is granted
        // later via the assignment screen.
        roleName: form.roleName,
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        email: form.email.trim(),
        phoneNumber: form.phoneNumber.trim() || undefined,
      })
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: (r) => {
          this.createResult.set(r);
          this.createForm.set(null);
          this.toaster.success('Internal user created.');
          this.load();
        },
        error: () => undefined,
      });
  }
  protected toggleActive(row: InternalUserRow): void {
    if (this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.gateway
      .setUserActive(row.id, !row.isActive)
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success(row.isActive ? 'User deactivated.' : 'User reactivated.');
          this.load();
        },
        error: () => undefined,
      });
  }
  protected sendReset(row: InternalUserRow): void {
    if (this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.gateway
      .sendPasswordReset(row.id)
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: () => this.toaster.success('Password reset email queued.'),
        error: () => undefined,
      });
  }

  // ---- Tenants ----
  protected openNewTenant(): void {
    this.tenantForm.set({ id: null, name: '', editionId: null, adminEmail: '', isActive: true });
    this.ensureEditions();
  }
  protected openEditTenant(row: TenantRow): void {
    this.tenantForm.set({
      id: row.id,
      name: row.name,
      editionId: row.editionId,
      adminEmail: '',
      isActive: row.isActive,
      concurrencyStamp: row.concurrencyStamp,
    });
    this.ensureEditions();
  }
  private ensureEditions(): void {
    if (!this.editionOptions().length) {
      this.gateway.getEditionOptions().subscribe({
        next: (o) => this.editionOptions.set(o),
        error: () => this.editionOptions.set([]),
      });
    }
  }
  protected patchTenant(partial: Partial<TenantFormState>): void {
    const current = this.tenantForm();
    if (current) {
      this.tenantForm.set({ ...current, ...partial });
    }
  }
  protected closeTenant(): void {
    if (!this.isBusy()) {
      this.tenantForm.set(null);
    }
  }
  protected saveTenant(): void {
    const form = this.tenantForm();
    if (!form || this.isBusy()) {
      return;
    }
    if (!form.name.trim()) {
      this.toaster.warn('Subdomain is required.');
      return;
    }
    if (!form.id && !EMAIL_RE.test(form.adminEmail.trim())) {
      this.toaster.warn('A valid admin email is required for a new tenant.');
      return;
    }
    this.isBusy.set(true);
    const request$ = form.id ? this.gateway.updateTenant(form) : this.gateway.createTenant(form);
    request$.pipe(finalize(() => this.isBusy.set(false))).subscribe({
      next: () => {
        this.toaster.success(form.id ? 'Tenant saved.' : 'Tenant created.');
        this.tenantForm.set(null);
        this.load();
      },
      error: () => undefined,
    });
  }
  /**
   * "Switch into clinic" -- IT Admin only (the Tenants section is gated by
   * Saas.Tenants, which only IT Admin holds). Uses ABP tenant impersonation
   * (Saas.Tenants.Impersonation): signs in as the clinic's `admin` user with
   * full tenant access via an impersonation token, then ImpersonationService
   * reloads the app into that tenant's context. The token's tenant claim wins
   * over the admin.localhost subdomain because CurrentUserTenantResolveContributor
   * is registered before the domain resolver (CaseEvaluationHttpApiHostModule).
   * Staff Supervisor's business-only switch is a separate, deferred effort
   * (stock ABP cannot scope tenant impersonation down -- see the access-model plan).
   */
  protected switchTenant(row: TenantRow): void {
    if (this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.toaster.info('Switching into ' + row.name + '...');
    this.impersonation
      .impersonateTenant(row.id, 'admin')
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({ error: () => undefined });
  }
}
