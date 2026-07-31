import { CommonModule } from '@angular/common';
import { Component, Injector, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ConfigStateService, RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { finalize } from 'rxjs';

import { MyAttorneyProfileService } from '../../proxy/my-attorney-profiles/my-attorney-profile.service';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { ExternalNavbarComponent } from '../../shared/components/external-navbar/external-navbar.component';
import { performFullLogout } from '../../shared/auth/full-logout';

interface StateOption {
  id: string;
  name: string;
}

/**
 * #9 (2026-06-19): external attorney "My profile". An applicant/defense attorney edits
 * their own name + firm + contact, which updates ONLY their master record (the
 * self-scoped MyAttorneyProfile endpoint resolves the caller from CurrentUser.Id). A
 * save here never rewrites past appointments -- those keep their booking-time snapshot.
 * Mirrors the patient my-profile (external-only, outside the internal shell, Default CD).
 */
@Component({
  selector: 'app-attorney-profile',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, IconComponent, ExternalNavbarComponent],
  templateUrl: './attorney-profile.component.html',
})
export class AttorneyProfileComponent implements OnInit {
  private readonly api = inject(MyAttorneyProfileService);
  private readonly rest = inject(RestService);
  private readonly fb = inject(FormBuilder);
  private readonly toaster = inject(ToasterService);
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);
  private readonly config = inject(ConfigStateService);

  protected isLoading = true;
  protected isBusy = false;
  protected notFound = false;
  protected states: StateOption[] = [];
  protected email = '';
  protected kind = '';
  private concurrencyStamp: string | undefined;

  protected navClinicName = 'Appointment Portal';
  protected navDisplayName = '';
  protected navRoleLabel = '';
  protected navUserEmail = '';

  readonly form = this.fb.group({
    firstName: [null as string | null, [Validators.maxLength(50)]],
    lastName: [null as string | null, [Validators.maxLength(50)]],
    firmName: [null as string | null, [Validators.maxLength(255)]],
    webAddress: [null as string | null, [Validators.maxLength(100)]],
    phoneNumber: [null as string | null, [Validators.maxLength(20)]],
    faxNumber: [null as string | null, [Validators.maxLength(20)]],
    street: [null as string | null, [Validators.maxLength(255)]],
    city: [null as string | null, [Validators.maxLength(50)]],
    stateId: [null as string | null],
    zipCode: [null as string | null, [Validators.maxLength(15)]],
  });

  ngOnInit(): void {
    this.loadNav();
    this.loadStates();
    this.loadProfile();
  }

  protected get roleLabel(): string {
    return this.kind === 'defense' ? 'Defense Attorney' : 'Applicant Attorney';
  }

  private loadProfile(): void {
    this.api
      .get()
      .pipe(finalize(() => (this.isLoading = false)))
      .subscribe({
        next: (p) => {
          this.kind = p.kind ?? '';
          this.email = p.email ?? '';
          this.concurrencyStamp = p.concurrencyStamp;
          this.form.patchValue({
            firstName: p.firstName ?? null,
            lastName: p.lastName ?? null,
            firmName: p.firmName ?? null,
            webAddress: p.webAddress ?? null,
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
    // Use the non-gated lookup external users can call. The gated /api/app/states
    // CRUD endpoint 403s for an external attorney (and trips the global access
    // overlay). Mirrors the external booking state dropdown.
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
    this.api
      .update({ ...v, concurrencyStamp: this.concurrencyStamp })
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
