import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { LocalizationPipe } from '@abp/ng.core';
import { RestService } from '@abp/ng.core';

/**
 * Admin-side invite form for external users (Patient, Applicant Attorney,
 * Defense Attorney, Claim Examiner). Backend constrains the role to those
 * four; internal roles never appear in this dropdown. Gated server-side
 * by the CaseEvaluation.UserManagement.InviteExternalUser permission;
 * granted to IT Admin, Staff Supervisor, and Clinic Staff.
 *
 * 2026-05-15 -- now a tokenized flow:
 *   1. POST /api/app/external-users/invite -> returns inviteUrl
 *      (`{authServerBaseUrl}/Account/Register?inviteToken=<raw>`),
 *      email, roleName, tenantName, expiresAt.
 *   2. The recipient receives the same URL via the InviteExternalUser
 *      NotificationTemplate (delivered through INotificationDispatcher
 *      + Hangfire, the same path as ResetPassword / PasswordChange).
 *   3. Clicking the link opens AuthServer Razor /Account/Register; the
 *      JS overlay validates the token, prefills + locks email + role,
 *      and atomically marks the invitation accepted on submit.
 *   4. The UI still shows the URL with a "Copy link" button so the
 *      admin can share manually when SMTP delivery is degraded.
 */
@Component({
  selector: 'app-invite-external-user',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, LocalizationPipe],
  templateUrl: './invite-external-user.component.html',
})
export class InviteExternalUserComponent {
  private readonly fb = inject(FormBuilder);
  private readonly restService = inject(RestService);
  private readonly router = inject(Router);

  // ExternalUserType enum values: Patient=1, ClaimExaminer=2,
  // ApplicantAttorney=3, DefenseAttorney=4. Order kept stable with the
  // proxy enum so the numeric values flow through to the backend.
  readonly roleOptions = [
    { value: 1, label: 'Patient' },
    { value: 3, label: 'Applicant Attorney' },
    { value: 4, label: 'Defense Attorney' },
    { value: 2, label: 'Claim Examiner' },
  ];

  readonly form = this.fb.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    userType: [1 as number | null, [Validators.required]],
  });

  readonly isSubmitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly result = signal<{
    inviteUrl: string;
    email: string;
    roleName: string;
    tenantName: string;
    expiresAt: string;
  } | null>(null);
  readonly copyConfirmation = signal<string | null>(null);

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
      email: (value.email ?? '').trim(),
      userType: Number(value.userType ?? 1),
    };

    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    this.result.set(null);
    this.copyConfirmation.set(null);

    try {
      const response = await firstValueFrom(
        this.restService.request<
          any,
          {
            inviteUrl: string;
            email: string;
            roleName: string;
            tenantName: string;
            expiresAt: string;
          }
        >(
          {
            method: 'POST',
            url: '/api/app/external-users/invite',
            body: payload,
          },
          { apiName: 'Default' },
        ),
      );
      this.result.set({
        inviteUrl: response.inviteUrl,
        email: response.email,
        roleName: response.roleName,
        tenantName: response.tenantName,
        expiresAt: response.expiresAt,
      });
    } catch (err: any) {
      const message =
        err?.error?.error?.message ??
        err?.error?.message ??
        err?.message ??
        'Failed to create invite. Try again or copy the link manually.';
      this.errorMessage.set(message);
    } finally {
      this.isSubmitting.set(false);
    }
  }

  async copyInviteUrl(): Promise<void> {
    const url = this.result()?.inviteUrl;
    if (!url) return;
    try {
      await navigator.clipboard.writeText(url);
      this.copyConfirmation.set('Copied!');
      setTimeout(() => this.copyConfirmation.set(null), 2000);
    } catch {
      this.copyConfirmation.set('Copy failed -- select the URL manually.');
    }
  }

  resetForm(): void {
    this.form.reset({ userType: 1 });
    this.result.set(null);
    this.errorMessage.set(null);
    this.copyConfirmation.set(null);
  }

  goBack(): void {
    this.router.navigateByUrl('/');
  }
}
