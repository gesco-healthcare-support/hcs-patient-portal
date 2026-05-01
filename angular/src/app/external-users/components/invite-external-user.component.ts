import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { LocalizationPipe } from '@abp/ng.core';
import { RestService } from '@abp/ng.core';

/**
 * D.2 (2026-04-30): admin-side invite form for external users (Patient,
 * Applicant Attorney, Defense Attorney, Claim Examiner). Backend constrains
 * the role to those four; internal roles never appear in this dropdown.
 *
 * Submission flow:
 *   1. Admin (tenant `admin`, Staff Supervisor, or host IT Admin) submits.
 *   2. POST /api/app/external-users/invite returns the constructed register
 *      URL plus an `emailEnqueued` flag. The Hangfire pipeline writes the
 *      email body via the same `SendAppointmentEmailJob` used by the 6.1
 *      fan-out, so when SMTP credentials are real, the recipient gets the
 *      invite via email.
 *   3. The UI ALWAYS displays the URL with a "Copy link" button so the
 *      admin can share it manually -- the dev stack swallows email silently
 *      until ACS credentials land (S-5.7), and Mailtrap-class sandboxes
 *      do not deliver to real inboxes either.
 *
 * Visual gate: a yellow banner explicitly labels the page as DEV-ONLY so the
 * mechanism is not confused with production-grade invite tracking (no token,
 * no expiry, no acceptance state machine).
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
    emailEnqueued: boolean;
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
            emailEnqueued: boolean;
            email: string;
            roleName: string;
            tenantName: string;
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
        emailEnqueued: response.emailEnqueued,
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
