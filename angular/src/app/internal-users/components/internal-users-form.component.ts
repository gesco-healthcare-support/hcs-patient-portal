import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { RestService } from '@abp/ng.core';

/**
 * IT-Admin-only form for creating a new internal user (Clinic Staff or
 * Staff Supervisor). Hits POST /api/app/internal-users on submit.
 * Auto-generated temporary password is emailed to the new user via
 * INotificationDispatcher (Hangfire queue); the password is NEVER
 * shown in the response or in this UI -- the email is the only
 * channel it leaves the server through.
 *
 * Tenant resolution: IT Admin is host-scoped (admin.localhost), so
 * the form carries a tenant picker dropdown populated from
 * GET /api/app/internal-users/tenants.
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
  readonly result = signal<{
    email: string;
    firstName: string;
    lastName: string;
    roleName: string;
    tenantName: string;
    welcomeEmailQueued: boolean;
  } | null>(null);

  async ngOnInit(): Promise<void> {
    // Populate the tenant dropdown on mount. The endpoint runs in host
    // context and returns every active tenant; the form's `tenantId`
    // control is required so the user has to make an explicit choice.
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
    this.result.set(null);
    this.errorMessage.set(null);
  }

  goBack(): void {
    this.router.navigateByUrl('/');
  }
}
