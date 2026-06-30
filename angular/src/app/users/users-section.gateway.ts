import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';
import { EditionService, TenantService } from '@volo/abp.ng.saas/proxy';
import type { SaasTenantCreateDto, SaasTenantUpdateDto } from '@volo/abp.ng.saas/proxy';
import { ExternalUserService } from '../proxy/external-users/external-user.service';
import { ExternalSignupService } from '../proxy/external-signups/external-signup.service';
import { InternalUsersService } from '../proxy/internal-users/internal-users.service';
import { UserExtendedService } from '../proxy/users/user-extended.service';
import { DashboardService } from '../proxy/dashboards/dashboard.service';
import type {
  InvitationDto,
  InviteExternalUserDto,
  InviteExternalUserResultDto,
} from '../proxy/external-signups/models';
import type {
  CreateInternalUserDto,
  InternalUserCreatedDto,
  InternalUserListDto,
} from '../proxy/internal-users/models';
import type { OfficeListDto } from '../proxy/dashboards/models';
import type { LookupDto } from '../proxy/shared/models';
import type {
  ManagedTablePage,
  ManagedTableQuery,
} from '../shared/components/managed-table/managed-table.models';

/** Draft for the create/edit tenant modal. */
export interface TenantFormState {
  id: string | null;
  name: string;
  editionId: string | null;
  adminEmail: string;
  isActive: boolean;
  concurrencyStamp?: string;
}

/**
 * Routes each Users-hub section to the right proxy service so the hub component
 * stays free of per-section wiring. Invitations + internal-user management ride
 * the custom CaseEvaluation endpoints; the Tenants section combines the stock
 * Volo SaaS TenantService (list/create/update + impersonation) with the custom
 * per-tenant counts endpoint (A-B4).
 */
@Injectable({ providedIn: 'root' })
export class UsersSectionGateway {
  private readonly externalUsers = inject(ExternalUserService);
  private readonly externalSignup = inject(ExternalSignupService);
  private readonly internalUsers = inject(InternalUsersService);
  private readonly userExtended = inject(UserExtendedService);
  private readonly dashboard = inject(DashboardService);
  private readonly tenants = inject(TenantService);
  private readonly editions = inject(EditionService);

  // ---- Invite External ----
  sendInvite(input: InviteExternalUserDto): Observable<InviteExternalUserResultDto> {
    return this.externalUsers.inviteExternalUser(input);
  }
  /**
   * QA item C: offices an external user can be invited into. Populated only at
   * HOST scope (the backend returns an empty list inside an office, where the
   * tenant is implicit), so a non-empty result means "show the office picker".
   */
  getInviteTenantOptions(): Observable<LookupDto<string>[]> {
    return this.externalSignup.getTenantOptions().pipe(map((r) => r.items ?? []));
  }

  // ---- Pending Invites ----
  /** Server-paged data source for the Pending Invites table (getInvites is already paged). */
  invitesPage(query: ManagedTableQuery): Observable<ManagedTablePage<InvitationDto>> {
    return this.externalUsers
      .getInvites({
        filter: query.search.trim() || undefined,
        sorting: query.sorting || undefined,
        skipCount: query.skipCount,
        maxResultCount: query.maxResultCount,
      })
      .pipe(map((r) => ({ items: r.items ?? [], totalCount: r.totalCount ?? 0 })));
  }
  resendInvite(id: string): Observable<InviteExternalUserResultDto> {
    return this.externalUsers.resendInvite(id);
  }
  revokeInvite(id: string): Observable<void> {
    return this.externalUsers.revokeInvite(id);
  }

  // ---- Internal Users ----
  /**
   * Server-paged data source for the Internal Users table. The backend
   * (GetInternalUsersAsync) scopes to the internal roles and applies the
   * filter/sort/page, replacing the old client-side load-500-then-filter (which
   * truncated the staff list past 500 total identity users).
   */
  internalUsersPage(query: ManagedTableQuery): Observable<ManagedTablePage<InternalUserListDto>> {
    return this.internalUsers
      .getInternalUsers({
        filter: query.search.trim() || undefined,
        sorting: query.sorting || undefined,
        skipCount: query.skipCount,
        maxResultCount: query.maxResultCount,
      })
      .pipe(map((r) => ({ items: r.items ?? [], totalCount: r.totalCount ?? 0 })));
  }
  createInternalUser(input: CreateInternalUserDto): Observable<InternalUserCreatedDto> {
    return this.internalUsers.create(input);
  }
  sendPasswordReset(id: string): Observable<void> {
    return this.internalUsers.sendPasswordResetEmail(id);
  }
  /**
   * Toggle a user's active flag. ABP's update endpoint replaces the whole DTO, so
   * fetch the current user first and resend every field with only isActive flipped
   * (preserving roles + concurrency stamp).
   */
  setUserActive(id: string, isActive: boolean): Observable<void> {
    return this.userExtended.get(id).pipe(
      switchMap((u) =>
        this.userExtended.update(id, {
          userName: u.userName ?? u.email ?? '',
          name: u.name,
          surname: u.surname,
          email: u.email ?? '',
          phoneNumber: u.phoneNumber,
          isActive,
          lockoutEnabled: u.lockoutEnabled,
          roleNames: u.roleNames,
          shouldChangePasswordOnNextLogin: u.shouldChangePasswordOnNextLogin,
          emailConfirmed: u.emailConfirmed,
          concurrencyStamp: u.concurrencyStamp,
        }),
      ),
      map(() => undefined),
    );
  }

  // ---- Tenants ----
  /**
   * Server-paged data source for the Offices/Tenants table. One call to
   * GetOfficesAsync returns the page's tenants WITH their edition, activation, and
   * per-office user/appointment counts -- replacing the old client forkJoin of the
   * Volo SaaS list + getTenantSummaries.
   */
  officesPage(query: ManagedTableQuery): Observable<ManagedTablePage<OfficeListDto>> {
    return this.dashboard
      .getOffices({
        filter: query.search.trim() || undefined,
        sorting: query.sorting || undefined,
        skipCount: query.skipCount,
        maxResultCount: query.maxResultCount,
      })
      .pipe(map((r) => ({ items: r.items ?? [], totalCount: r.totalCount ?? 0 })));
  }
  getEditionOptions(): Observable<{ id: string; name: string }[]> {
    return this.editions
      .getList({ maxResultCount: 100, skipCount: 0 })
      .pipe(
        map((r) => (r.items ?? []).map((e) => ({ id: e.id ?? '', name: e.displayName ?? '' }))),
      );
  }

  createTenant(form: TenantFormState): Observable<unknown> {
    const input: SaasTenantCreateDto = {
      name: form.name.trim().toLowerCase(),
      editionId: form.editionId ?? undefined,
      // Omit activationState on create -> SaaS defaults to Active.
      adminEmailAddress: form.adminEmail.trim(),
      // The new tenant admin sets their own password via the forgot-password flow;
      // we seed a strong throwaway so the SaaS create (which requires one) succeeds.
      adminPassword: this.generatePassword(),
      connectionStrings: { databases: [] },
    };
    return this.tenants.create(input);
  }
  updateTenant(form: TenantFormState): Observable<unknown> {
    const input: SaasTenantUpdateDto = {
      name: form.name.trim().toLowerCase(),
      editionId: form.editionId ?? undefined,
      activationState: (form.isActive ? 0 : 2) as SaasTenantUpdateDto['activationState'],
      concurrencyStamp: form.concurrencyStamp,
    };
    return this.tenants.update(form.id as string, input);
  }

  private generatePassword(): string {
    const sets = ['ABCDEFGHJKLMNPQRSTUVWXYZ', 'abcdefghijkmnpqrstuvwxyz', '23456789', '!@#$%*'];
    const all = sets.join('');
    const bytes = new Uint8Array(16);
    crypto.getRandomValues(bytes);
    const pick = (chars: string, byte: number) => chars.charAt(byte % chars.length);
    let password = sets.map((chars, i) => pick(chars, bytes[i])).join('');
    for (let i = 4; i < bytes.length; i++) {
      password += pick(all, bytes[i]);
    }
    return password;
  }
}
