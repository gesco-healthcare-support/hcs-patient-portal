import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { ExternalSignupService } from '../../proxy/external-signups/external-signup.service';
import { ExternalUserType } from '../../proxy/external-signups/external-user-type.enum';

/**
 * R1 (2026-05-05): public external-user registration form. Mirrors OLD's
 * /users/add page (P:\PatientPortalOld\patientappointment-portal\src\app\
 * components\user\users\add\user-add.component.{html,ts}) ported to NEW's
 * standalone-component + ABP proxy stack.
 *
 * Tenant is resolved from the subdomain by DomainTenantResolveContributor
 * (ADR-006); this form does NOT collect a tenant id. The backend AppService
 * uses CurrentTenant.Id directly.
 *
 * Role list is hardcoded to OLD's 4 canonical external roles (Patient,
 * Adjuster, Applicant Attorney, Defense Attorney). ClaimExaminer is omitted
 * per project_role-model memory.
 *
 * Conditional fields per OLD html:23-36:
 *   - Patient / Adjuster -> First Name + Last Name
 *   - Applicant Attorney / Defense Attorney -> Firm Name
 *   - Always -> Email, Password, Confirm Password
 *
 * On success: verbatim OLD toast + redirect to /account/login after 1s.
 */
@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, LocalizationPipe],
  templateUrl: './register.component.html',
})
export class RegisterComponent {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly externalSignup = inject(ExternalSignupService);
  private readonly toaster = inject(ToasterService);

  // OLD's 4 external roles (Roles.cs:14-17). Numeric values match the proxy
  // ExternalUserType enum so the role select binds straight through.
  readonly roleOptions = [
    { value: ExternalUserType.Patient, label: 'Patient' },
    { value: ExternalUserType.Adjuster, label: 'Adjuster' },
    { value: ExternalUserType.ApplicantAttorney, label: 'Applicant Attorney' },
    { value: ExternalUserType.DefenseAttorney, label: 'Defense Attorney' },
  ];

  readonly form = this.fb.group({
    userType: [ExternalUserType.Patient as ExternalUserType, [Validators.required]],
    firstName: ['', [Validators.required, Validators.maxLength(50)]],
    lastName: ['', [Validators.required, Validators.maxLength(50)]],
    firmName: ['', [Validators.maxLength(50)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(100)]],
    password: ['', [Validators.required]],
    confirmPassword: ['', [Validators.required]],
  });

  readonly isSubmitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  constructor() {
    // OLD selectChangeHandler (user-add.component.ts:84-101) toggles the
    // required validator between firstName/lastName and firmName when the
    // role changes. Reactive form mirror: subscribe to userType changes,
    // re-apply validators, recompute validity.
    this.form.controls.userType.valueChanges.subscribe((role) => {
      this.applyRoleValidators(role ?? ExternalUserType.Patient);
    });
  }

  get isAttorney(): boolean {
    const role = this.form.controls.userType.value;
    return role === ExternalUserType.ApplicantAttorney || role === ExternalUserType.DefenseAttorney;
  }

  private applyRoleValidators(role: ExternalUserType): void {
    const isAttorney =
      role === ExternalUserType.ApplicantAttorney || role === ExternalUserType.DefenseAttorney;

    if (isAttorney) {
      this.form.controls.firstName.clearValidators();
      this.form.controls.lastName.clearValidators();
      this.form.controls.firmName.setValidators([Validators.required, Validators.maxLength(50)]);
    } else {
      this.form.controls.firstName.setValidators([Validators.required, Validators.maxLength(50)]);
      this.form.controls.lastName.setValidators([Validators.required, Validators.maxLength(50)]);
      this.form.controls.firmName.setValidators([Validators.maxLength(50)]);
    }
    this.form.controls.firstName.updateValueAndValidity({ emitEvent: false });
    this.form.controls.lastName.updateValueAndValidity({ emitEvent: false });
    this.form.controls.firmName.updateValueAndValidity({ emitEvent: false });
  }

  async onSubmit(): Promise<void> {
    if (this.isSubmitting()) return;

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    if ((v.password ?? '') !== (v.confirmPassword ?? '')) {
      this.errorMessage.set('Password and confirm password do not match.');
      return;
    }

    const isAttorney = this.isAttorney;
    const payload = {
      userType: v.userType ?? ExternalUserType.Patient,
      email: (v.email ?? '').trim(),
      password: v.password ?? '',
      confirmPassword: v.confirmPassword ?? '',
      firstName: isAttorney ? null : (v.firstName ?? '').trim() || null,
      lastName: isAttorney ? null : (v.lastName ?? '').trim() || null,
      firmName: isAttorney ? (v.firmName ?? '').trim() || null : null,
      // tenantId intentionally omitted: backend resolves from CurrentTenant
      // (set by DomainTenantResolveContributor from the subdomain per ADR-006).
    };

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    try {
      await firstValueFrom(this.externalSignup.register(payload));

      // OLD user-add.component.ts:73-76 -- success toast then setTimeout
      // navigate to login after 1s. Replicate exactly.
      this.toaster.success(
        'Your registration is successfully done, please verify your email to login.',
      );
      setTimeout(() => {
        this.router.navigateByUrl('/account/login');
      }, 1000);
    } catch (err: unknown) {
      const message = this.extractErrorMessage(err) ?? 'Registration failed. Please try again.';
      this.errorMessage.set(message);
    } finally {
      this.isSubmitting.set(false);
    }
  }

  private extractErrorMessage(err: unknown): string | null {
    if (!err || typeof err !== 'object') return null;
    const e = err as Record<string, unknown>;
    const errBody = e['error'] as Record<string, unknown> | undefined;
    const inner = errBody?.['error'] as Record<string, unknown> | undefined;
    return (
      (inner?.['message'] as string | undefined) ??
      (errBody?.['message'] as string | undefined) ??
      (e['message'] as string | undefined) ??
      null
    );
  }

  goToLogin(): void {
    this.router.navigateByUrl('/account/login');
  }
}
