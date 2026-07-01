import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ImpersonationService } from '@volo/abp.commercial.ng.ui/config';
import { Subject } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { OfficeNamePipe } from '../shared/pipes/office-name.pipe';
import { ManagedTableComponent } from '../shared/components/managed-table/managed-table.component';
import {
  ManagedTableCellDirective,
  ManagedTableRowActionsDirective,
} from '../shared/components/managed-table/managed-table-cell.directive';
import type {
  ManagedTableColumn,
  ManagedTableDataSource,
} from '../shared/components/managed-table/managed-table.models';
import { ExternalUserType } from '../proxy/external-signups/external-user-type.enum';
import { InvitationStatus } from '../proxy/invitations/invitation-status.enum';
import type { InvitationDto, InviteExternalUserResultDto } from '../proxy/external-signups/models';
import type { InternalUserCreatedDto, InternalUserListDto } from '../proxy/internal-users/models';
import type { OfficeListDto } from '../proxy/dashboards/models';
import type { LookupDto } from '../proxy/shared/models';
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
import { TenantFormState, UsersSectionGateway } from './users-section.gateway';

interface InviteDraft {
  firstName: string;
  lastName: string;
  email: string;
  userType: ExternalUserType;
  firmName: string;
  /** Target office; required + used only at host scope (see tenantPickerRequired). */
  tenantId: string;
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
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    IconComponent,
    OfficeNamePipe,
    ManagedTableComponent,
    ManagedTableCellDirective,
    ManagedTableRowActionsDirective,
  ],
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
  protected readonly isBusy = signal(false);

  /** Forces the currently-mounted app-managed-table to refetch after a mutation. */
  protected readonly reload$ = new Subject<void>();

  // Invite External
  protected readonly invite = signal<InviteDraft>(this.emptyInvite());
  protected readonly inviteResult = signal<InviteExternalUserResultDto | null>(null);
  protected readonly showFirm = computed(() => isAttorneyType(this.invite().userType));
  // QA item C: offices the invitee can be invited into. Non-empty only at host
  // scope; a non-empty list means the office picker is shown + required.
  protected readonly tenantOptions = signal<LookupDto<string>[]>([]);
  protected readonly tenantPickerRequired = computed(() => this.tenantOptions().length > 0);

  // Pending Invites (server-driven table)
  protected readonly invitesColumns: ManagedTableColumn[] = [
    { key: 'email', header: 'Email', sortable: true, sortKey: 'email' },
    { key: 'roleName', header: 'Role' },
    { key: 'invitedByName', header: 'Invited by' },
    { key: 'creationTime', header: 'Sent', sortable: true, sortKey: 'creationTime' },
    { key: 'expiresAt', header: 'Expires' },
    { key: 'status', header: 'Status' },
  ];
  protected readonly invitesDataSource: ManagedTableDataSource<InvitationDto> = (q) =>
    this.gateway.invitesPage(q);

  // Internal Users (server-driven table)
  protected readonly internalUsersColumns: ManagedTableColumn[] = [
    { key: 'fullName', header: 'Name', sortable: true, sortKey: 'fullName' },
    { key: 'email', header: 'Email', sortable: true, sortKey: 'email' },
    { key: 'role', header: 'Role', sortable: true, sortKey: 'role' },
    { key: 'isActive', header: 'Status', sortable: true, sortKey: 'isActive' },
  ];
  protected readonly internalUsersDataSource: ManagedTableDataSource<InternalUserListDto> = (q) =>
    this.gateway.internalUsersPage(q);
  protected readonly createForm = signal<CreateUserDraft | null>(null);
  protected readonly createResult = signal<InternalUserCreatedDto | null>(null);

  // Tenants / Offices (server-driven table)
  protected readonly officesColumns: ManagedTableColumn[] = [
    // UI label: header 'Practice' (code key: name)
    { key: 'name', header: 'Practice', sortable: true, sortKey: 'name' },
    { key: 'subdomain', header: 'Subdomain' },
    { key: 'userCount', header: 'Users' },
    { key: 'appointmentCount', header: 'Appointments' },
    { key: 'isActive', header: 'Status' },
  ];
  protected readonly officesDataSource: ManagedTableDataSource<OfficeListDto> = (q) =>
    this.gateway.officesPage(q);
  protected readonly tenantForm = signal<TenantFormState | null>(null);

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

  /** O5: editing a practice needs Saas.Tenants.Update; hide the row Edit button
   *  otherwise so a view-only operator never clicks into a 403. */
  protected canEditTenant(): boolean {
    return this.permission.getGrantedPolicy('Saas.Tenants.Update');
  }

  /** IU2: deactivate/reactivate routes through the ABP identity user update
   *  (UserExtendedAppService : IdentityUserAppService, no override), gated by
   *  AbpIdentity.Users.Update; hide the toggle otherwise (deny-by-default). */
  protected canDeactivateUser(): boolean {
    return this.permission.getGrantedPolicy('AbpIdentity.Users.Update');
  }

  /** O3: base domain for the subdomain preview, derived from the current host so
   *  it self-corrects per environment (admin.localhost -> 'localhost';
   *  admin.example.com -> 'example.com'). Falls back to the full host when there
   *  is no leading subdomain label to strip (e.g. a bare 'localhost'). */
  protected get tenantBaseDomain(): string {
    const host = window.location.hostname;
    const labels = host.split('.');
    return labels.length > 1 ? labels.slice(1).join('.') : host;
  }

  private closeModals(): void {
    this.createForm.set(null);
    this.tenantForm.set(null);
  }

  private load(): void {
    // The three list sections (pending / staff / tenants) fetch through
    // app-managed-table's own server data source, so only the invite form needs
    // per-section setup here.
    if (this.section() === 'invite') {
      this.applyInvitePrefill();
      this.loadInviteTenantOptions();
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
      tenantId: '',
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
  /** QA item C: load the host-scope office picker source (empty in-office). */
  private loadInviteTenantOptions(): void {
    this.gateway.getInviteTenantOptions().subscribe({
      next: (o) => this.tenantOptions.set(o),
      error: () => this.tenantOptions.set([]),
    });
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
    if (this.tenantPickerRequired() && !form.tenantId) {
      // UI label: 'practice' (code: office)
      this.toaster.warn('Select a practice for the invitation.');
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
        // QA item C: host-scope invites carry the chosen office; in-office
        // callers leave it undefined and the backend uses the ambient tenant.
        tenantId: form.tenantId || undefined,
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
          this.reload$.next();
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
          this.reload$.next();
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
          this.reload$.next();
        },
        error: () => undefined,
      });
  }
  protected toggleActive(row: InternalUserListDto): void {
    if (this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.gateway
      .setUserActive(row.id ?? '', !row.isActive)
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success(row.isActive ? 'User deactivated.' : 'User reactivated.');
          this.reload$.next();
        },
        error: () => undefined,
      });
  }
  protected sendReset(row: InternalUserListDto): void {
    if (this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.gateway
      .sendPasswordReset(row.id ?? '')
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: () => this.toaster.success('Password reset email queued.'),
        error: () => undefined,
      });
  }

  // ---- Tenants ----
  protected openNewTenant(): void {
    this.tenantForm.set({ id: null, name: '', editionId: null, adminEmail: '', isActive: true });
  }
  protected openEditTenant(row: OfficeListDto): void {
    // editionId is preserved on the DTO but no longer editable (O4 removed the
    // unused SaaS-edition selector); new practices are always created edition-less.
    this.tenantForm.set({
      id: row.id ?? null,
      name: row.name ?? '',
      editionId: row.editionId ?? null,
      adminEmail: '',
      isActive: row.isActive ?? true,
      concurrencyStamp: row.concurrencyStamp,
    });
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
      // UI label: 'practice' (code: tenant)
      this.toaster.warn('A valid admin email is required for a new practice.');
      return;
    }
    this.isBusy.set(true);
    const request$ = form.id ? this.gateway.updateTenant(form) : this.gateway.createTenant(form);
    request$.pipe(finalize(() => this.isBusy.set(false))).subscribe({
      next: () => {
        // UI label: 'Practice' (code: tenant)
        this.toaster.success(form.id ? 'Practice saved.' : 'Practice created.');
        this.tenantForm.set(null);
        this.reload$.next();
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
  protected switchTenant(row: OfficeListDto): void {
    if (this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.toaster.info('Switching into ' + (row.name ?? '') + '...');
    this.impersonation
      .impersonateTenant(row.id ?? '', 'admin')
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({ error: () => undefined });
  }
}
