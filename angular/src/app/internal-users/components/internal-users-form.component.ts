import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { ConfigStateService, RestService } from '@abp/ng.core';

/**
 * Internal-user creation form, reachable by host IT Admin (at
 * admin.localhost) and by per-tenant `admin` users on their own
 * subdomain. Hits POST /api/app/internal-users on submit. The
 * auto-generated temporary password is emailed to the new user via
 * INotificationDispatcher (Hangfire queue); the password is NEVER
 * shown in the response or in this UI -- the email is the only
 * channel it leaves the server through.
 *
 * Tenant resolution:
 *   - Host caller (IT Admin): no `currentTenant.id`. Render the
 *     dropdown as an editable picker fed by
 *     GET /api/app/internal-users/tenants so the IT Admin chooses
 *     which tenant the new user lands in.
 *   - Tenant caller (tenant admin): `currentTenant.id` is set.
 *     Render the dropdown as a single-option, disabled select
 *     pre-filled with the current tenant's name. The reactive form
 *     still carries the id so the existing payload shape is
 *     unchanged; the server-side gate in InternalUsersAppService
 *     rejects mismatched tenant ids defensively.
 *
 * Server-side enforcement of role allow-list (Clinic Staff, Staff
 * Supervisor) is authoritative; this component's dropdown only shows
 * the two creatable roles as a UX convenience.
 */
@Component({
  selector: 'app-internal-users-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './internal-users-form.component.html',
})
export class InternalUsersFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly restService = inject(RestService);
  private readonly router = inject(Router);
  private readonly configState = inject(ConfigStateService);

  // Server-side allow-list (must mirror InternalUsersAppService.CreatableRoleNames).
  // Order matches the OLD UI's dropdown order.
  readonly roleOptions = [
    { value: 'Clinic Staff', label: 'Clinic Staff' },
    { value: 'Staff Supervisor', label: 'Staff Supervisor' },
  ];

  readonly form = this.fb.group({
    tenantId: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    firstName: ['', [Validators.required, Validators.maxLength(64)]],
    lastName: ['', [Validators.required, Validators.maxLength(64)]],
    roleName: ['', [Validators.required]],
    phoneNumber: ['', [Validators.maxLength(20)]],
  });

  readonly isSubmitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly tenantsLoading = signal<boolean>(true);
  readonly tenants = signal<Array<{ id: string; displayName: string }>>([]);
  // 2026-05-19 -- true when the form is being rendered for a tenant
  // admin (current session already has a tenant). The dropdown is
  // disabled in this branch so the user can see which tenant they
  // are operating in but cannot pick a different one.
  readonly tenantLocked = signal<boolean>(false);
  readonly result = signal<{
    email: string;
    firstName: string;
    lastName: string;
    roleName: string;
    tenantName: string;
    welcomeEmailQueued: boolean;
  } | null>(null);

  async ngOnInit(): Promise<void> {
    // Branch on session scope: tenant admins are pre-bound to their
    // own tenant, host IT Admins choose from the full tenant list.
    const tenantFromSession = this.currentTenantFromSession();
    if (tenantFromSession) {
      // Tenant admin path: single-option list, disabled dropdown,
      // form pre-filled with the tenant id we already know.
      this.tenants.set([tenantFromSession]);
      this.tenantLocked.set(true);
      this.form.patchValue({ tenantId: tenantFromSession.id });
      this.form.get('tenantId')?.disable({ emitEvent: false });
      this.tenantsLoading.set(false);
      return;
    }

    // Host IT Admin path: hit the tenant lookup endpoint to populate
    // the picker. `tenantId` stays required so the submit is blocked
    // until an explicit choice is made.
    try {
      const response = await firstValueFrom(
        this.restService.request<
          void,
          { items: Array<{ id: string; displayName: string }>; totalCount?: number }
        >(
          {
            method: 'GET',
            url: '/api/app/internal-users/tenants',
          },
          { apiName: 'Default' },
        ),
      );
      this.tenants.set(response?.items ?? []);
      // Preselect the only tenant if there's exactly one (Phase 1A
      // single-tenant convenience; harmless when >1).
      if (response?.items?.length === 1) {
        this.form.patchValue({ tenantId: response.items[0].id });
      }
    } catch {
      // Tenant load failure is non-blocking for the page render; the
      // form's required-validator on tenantId stops the submit until
      // the user picks one, and a retry-on-error toast is overkill.
      this.tenants.set([]);
    } finally {
      this.tenantsLoading.set(false);
    }
  }

  /**
   * Read the current ABP tenant from ConfigStateService. Returns null
   * when the SPA is rendering on the host (admin.localhost) -- the
   * `currentTenant.id` field is absent there. Mirrors the shape used
   * in `appointment-add.component.ts` / `home.component.ts`.
   */
  private currentTenantFromSession(): { id: string; displayName: string } | null {
    const ct = this.configState.getOne('currentTenant') as
      | { id?: string; name?: string }
      | null
      | undefined;
    if (!ct?.id) {
      return null;
    }
    return { id: ct.id, displayName: ct.name ?? ct.id };
  }

  async onSubmit(): Promise<void> {
    if (this.isSubmitting()) {
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const payload = {
      tenantId: value.tenantId ?? '',
      email: (value.email ?? '').trim(),
      firstName: (value.firstName ?? '').trim(),
      lastName: (value.lastName ?? '').trim(),
      roleName: value.roleName ?? '',
      phoneNumber: (value.phoneNumber ?? '').trim() || null,
    };

    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    this.result.set(null);

    try {
      const response = await firstValueFrom(
        this.restService.request<
          typeof payload,
          {
            userId: string;
            email: string;
            firstName: string;
            lastName: string;
            roleName: string;
            tenantName: string;
            welcomeEmailQueued: boolean;
          }
        >(
          {
            method: 'POST',
            url: '/api/app/internal-users',
            body: payload,
          },
          { apiName: 'Default' },
        ),
      );
      this.result.set({
        email: response.email,
        firstName: response.firstName,
        lastName: response.lastName,
        roleName: response.roleName,
        tenantName: response.tenantName,
        welcomeEmailQueued: response.welcomeEmailQueued,
      });
      // Reset only the per-user fields so the IT Admin can quickly
      // create another user in the same tenant.
      this.form.patchValue({
        email: '',
        firstName: '',
        lastName: '',
        roleName: '',
        phoneNumber: '',
      });
      this.form.markAsUntouched();
    } catch (err: unknown) {
      const e = err as
        | { error?: { error?: { message?: string }; message?: string }; message?: string }
        | undefined;
      const message =
        e?.error?.error?.message ??
        e?.error?.message ??
        e?.message ??
        'Could not create the user. Please try again.';
      this.errorMessage.set(message);
    } finally {
      this.isSubmitting.set(false);
    }
  }

  resetForm(): void {
    this.form.reset({
      tenantId: this.tenants().length === 1 ? this.tenants()[0].id : '',
      email: '',
      firstName: '',
      lastName: '',
      roleName: '',
      phoneNumber: '',
    });
    // `form.reset()` re-enables disabled controls. Re-apply the lock so
    // the tenant-admin branch keeps the dropdown visually pinned.
    if (this.tenantLocked()) {
      this.form.get('tenantId')?.disable({ emitEvent: false });
    }
    this.result.set(null);
    this.errorMessage.set(null);
  }

  goBack(): void {
    this.router.navigateByUrl('/');
  }
}
