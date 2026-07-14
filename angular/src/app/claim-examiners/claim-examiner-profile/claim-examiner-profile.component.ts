import { CommonModule } from '@angular/common';
import { Component, Injector, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ConfigStateService, RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { finalize } from 'rxjs';

import { IconComponent } from '../../shared/ui/icon/icon.component';
import { ExternalNavbarComponent } from '../../shared/components/external-navbar/external-navbar.component';
import { performFullLogout } from '../../shared/auth/full-logout';

interface StateOption {
  id: string;
  name: string;
}

/** Shape of the self-scoped MyClaimExaminerProfile endpoint (camelCase). */
interface ClaimExaminerProfile {
  firstName?: string | null;
  lastName?: string | null;
  phoneNumber?: string | null;
  faxNumber?: string | null;
  street?: string | null;
  city?: string | null;
  stateId?: string | null;
  zipCode?: string | null;
  email?: string | null;
  concurrencyStamp?: string;
}

/**
 * R2-4 (2026-06-22): external claim-examiner "My profile". Mirrors the attorney
 * my-profile (#9) but for the claim-examiner master, which has no firm fields. Edits
 * ONLY the caller's own master (the self-scoped MyClaimExaminerProfile endpoint
 * resolves the caller from CurrentUser.Id). A save here never rewrites past
 * appointments. External-only, outside the internal shell, Default change-detection.
 */
@Component({
  selector: 'app-claim-examiner-profile',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, IconComponent, ExternalNavbarComponent],
  templateUrl: './claim-examiner-profile.component.html',
})
export class ClaimExaminerProfileComponent implements OnInit {
  private readonly rest = inject(RestService);
  private readonly fb = inject(FormBuilder);
  private readonly toaster = inject(ToasterService);
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);
  private readonly config = inject(ConfigStateService);

  private static readonly profileUrl = '/api/app/my-claim-examiner-profile';

  protected isLoading = true;
  protected isBusy = false;
  protected notFound = false;
  protected states: StateOption[] = [];
  protected email = '';
  private concurrencyStamp: string | undefined;

  protected navClinicName = 'Appointment Portal';
  protected navDisplayName = '';
  protected navRoleLabel = '';
  protected navUserEmail = '';

  readonly form = this.fb.group({
    firstName: [null as string | null, [Validators.maxLength(50)]],
    lastName: [null as string | null, [Validators.maxLength(50)]],
    phoneNumber: [null as string | null, [Validators.maxLength(20)]],
    faxNumber: [null as string | null, [Validators.maxLength(19)]],
    street: [null as string | null, [Validators.maxLength(255)]],
    city: [null as string | null, [Validators.maxLength(50)]],
    stateId: [null as string | null],
    zipCode: [null as string | null, [Validators.maxLength(10)]],
  });

  ngOnInit(): void {
    this.loadNav();
    this.loadStates();
    this.loadProfile();
  }

  protected get roleLabel(): string {
    return 'Claim Examiner';
  }

  private loadProfile(): void {
    this.rest
      .request<unknown, ClaimExaminerProfile>(
        { method: 'GET', url: ClaimExaminerProfileComponent.profileUrl },
        { apiName: 'Default' },
      )
      .pipe(finalize(() => (this.isLoading = false)))
      .subscribe({
        next: (p) => {
          this.email = p.email ?? '';
          this.concurrencyStamp = p.concurrencyStamp;
          this.form.patchValue({
            firstName: p.firstName ?? null,
            lastName: p.lastName ?? null,
            phoneNumber: p.phoneNumber ?? null,
            faxNumber: p.faxNumber ?? null,
            street: p.street ?? null,
            city: p.city ?? null,
            stateId: p.stateId ?? null,
            zipCode: p.zipCode ?? null,
          });
        },
        error: () => (this.notFound = true),
      });
  }

  private loadStates(): void {
    // Non-gated lookup external users can call (the gated /api/app/states CRUD 403s
    // for an external party). Mirrors the external booking + attorney-profile dropdown.
    this.rest
      .request<unknown, { items?: { id?: string; displayName?: string }[] }>(
        {
          method: 'GET',
          url: '/api/app/patients/state-lookup',
          params: { maxResultCount: 100, skipCount: 0 },
        },
        { apiName: 'Default' },
      )
      .subscribe({
        next: (res) =>
          (this.states = (res.items ?? []).map((s) => ({
            id: s.id ?? '',
            name: s.displayName ?? '',
          }))),
      });
  }

  private loadNav(): void {
    const u = this.config.getOne('currentUser') as {
      name?: string;
      surname?: string;
      email?: string;
      userName?: string;
      roles?: string[];
    } | null;
    const t = this.config.getOne('currentTenant') as { name?: string } | null;
    this.navClinicName = t?.name || 'Appointment Portal';
    this.navDisplayName =
      [u?.name, u?.surname].filter(Boolean).join(' ').trim() || u?.userName || u?.email || '';
    this.navUserEmail = u?.email || u?.userName || '';
    this.navRoleLabel = u?.roles?.[0] ?? '';
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.isBusy = true;
    const v = this.form.getRawValue();
    this.rest
      .request<ClaimExaminerProfile, ClaimExaminerProfile>(
        {
          method: 'PUT',
          url: ClaimExaminerProfileComponent.profileUrl,
          body: { ...v, concurrencyStamp: this.concurrencyStamp },
        },
        { apiName: 'Default' },
      )
      .pipe(finalize(() => (this.isBusy = false)))
      .subscribe({
        next: (updated) => {
          this.concurrencyStamp = updated.concurrencyStamp;
          this.toaster.success('Profile changes saved.');
        },
      });
  }

  protected backHome(): void {
    void this.router.navigateByUrl('/');
  }

  protected signOut(): void {
    void performFullLogout(this.injector);
  }
}
