import { Injectable, inject } from '@angular/core';
import { forkJoin, Observable } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';
import { EditionService, TenantService } from '@volo/abp.ng.saas/proxy';
import type { SaasTenantCreateDto, SaasTenantUpdateDto } from '@volo/abp.ng.saas/proxy';
import { ExternalUserService } from '../proxy/external-users/external-user.service';
import { InternalUsersService } from '../proxy/internal-users/internal-users.service';
import { UserExtendedService } from '../proxy/users/user-extended.service';
import { DashboardService } from '../proxy/dashboards/dashboard.service';
import type {
  InvitationDto,
  InviteExternalUserDto,
  InviteExternalUserResultDto,
} from '../proxy/external-signups/models';
import type { CreateInternalUserDto, InternalUserCreatedDto } from '../proxy/internal-users/models';
import { INTERNAL_ROLE_NAMES, primaryInternalRole } from './users-hub.util';

const PAGE = { maxResultCount: 500, skipCount: 0 };

/** A normalized internal-user row for the Internal Users table. */
export interface InternalUserRow {
  id: string;
  fullName: string;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  isActive: boolean;
}

/** A normalized tenant row for the Tenants table (SaaS tenant + per-tenant counts). */
export interface TenantRow {
  id: string;
  name: string;
  subdomain: string;
  editionName: string;
  editionId: string | null;
  userCount: number;
  appointmentCount: number;
  isActive: boolean;
  concurrencyStamp?: string;
}

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
  private readonly internalUsers = inject(InternalUsersService);
  private readonly userExtended = inject(UserExtendedService);
  private readonly dashboard = inject(DashboardService);
  private readonly tenants = inject(TenantService);
  private readonly editions = inject(EditionService);

  // ---- Invite External ----
  sendInvite(input: InviteExternalUserDto): Observable<InviteExternalUserResultDto> {
    return this.externalUsers.inviteExternalUser(input);
  }

  // ---- Pending Invites ----
  listInvites(filter: string): Observable<InvitationDto[]> {
    return this.externalUsers
      .getInvites({ filter: filter.trim() || undefined, ...PAGE })
      .pipe(map((r) => r.items ?? []));
  }
  resendInvite(id: string): Observable<InviteExternalUserResultDto> {
    return this.externalUsers.resendInvite(id);
  }
  revokeInvite(id: string): Observable<void> {
    return this.externalUsers.revokeInvite(id);
  }

  // ---- Internal Users ----
  listInternalUsers(): Observable<InternalUserRow[]> {
    return this.userExtended.getList({ ...PAGE }).pipe(
      map((r) =>
        (r.items ?? [])
          .filter((u) => (u.roleNames ?? []).some((rn) => INTERNAL_ROLE_NAMES.includes(rn)))
          .map((u) => ({
            id: u.id ?? '',
            fullName: `${u.name ?? ''} ${u.surname ?? ''}`.trim() || (u.userName ?? ''),
            firstName: u.name ?? '',
            lastName: u.surname ?? '',
            email: u.email ?? '',
            role: primaryInternalRole(u.roleNames),
            isActive: u.isActive ?? true,
          })),
      ),
    );
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
  listTenants(): Observable<TenantRow[]> {
    return forkJoin({
      page: this.tenants.getList({ getEditionNames: true, ...PAGE }),
      stats: this.dashboard.getTenantSummaries(),
    }).pipe(
      map(({ page, stats }) => {
        const byId = new Map((stats ?? []).map((s) => [s.tenantId, s]));
        return (page.items ?? []).map((t) => {
          const id = t.id ?? '';
          const summary = byId.get(id);
          return {
            id,
            name: t.name ?? '',
            subdomain: (t.name ?? '').toLowerCase(),
            editionName: t.editionName ?? '',
            editionId: t.editionId ?? null,
            userCount: summary?.userCount ?? 0,
            appointmentCount: summary?.appointmentCount ?? 0,
            // TenantActivationState: 0 Active, 1 ActiveWithLimitedTime, 2 Passive.
            isActive: Number(t.activationState ?? 0) !== 2,
            concurrencyStamp: t.concurrencyStamp,
          };
        });
      }),
    );
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
