import { CommonModule } from '@angular/common';
import { Component, Injector, OnDestroy, OnInit, inject } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ConfigStateService, PagedResultDto, RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { finalize } from 'rxjs/operators';

import { PatientProfileComponent } from './patient-profile.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';
import { ExternalNavbarComponent } from '../../../shared/components/external-navbar/external-navbar.component';
import { performFullLogout } from '../../../shared/auth/full-logout';
import { resolveExternalUserDisplayName } from '../../../shared/auth/external-user-display-name';
import { environment } from '../../../../environments/environment';
import type { PatientDto, PatientUpdateDto } from '../../../proxy/patients/models';
import type { LookupDto } from '../../../proxy/shared/models';

/** A bound {id,label} option for the State / Language selects. */
interface MpOption {
  id: string;
  label: string;
}

type MpSection = 'personal' | 'contact' | 'address' | 'preferences';

/**
 * My Profile (redesign). EXTENDS PatientProfileComponent so it inherits the
 * whole profile engine -- the reactive form, the two-load topology
 * (GET /patients/me for Patient, GET /external-users/me for AA/DA/CE), the role
 * discriminator, and the never-clear SSN contract -- with zero duplication.
 * This subclass adds only the redesigned .mp-* shell: avatar header, the four
 * per-section-editable patient cards (Personal / Contact / Address /
 * Preferences) with a shared save-confirm modal, the attorney/examiner
 * read-only card, and the Account & Security card. SSN is intentionally omitted
 * (parity with the prototype). Mounted at a temp route; the navbar swaps to it
 * after live sign-off, then the legacy page is deleted.
 */
@Component({
  selector: 'app-patient-profile-redesign',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, IconComponent, ExternalNavbarComponent],
  templateUrl: './patient-profile-redesign.component.html',
})
export class PatientProfileRedesignComponent
  extends PatientProfileComponent
  implements OnInit, OnDestroy
{
  // Re-injected under distinct names: the base declares these private, so they
  // are on the instance but not accessible from the subclass.
  private readonly mpRest = inject(RestService);
  private readonly mpInjector = inject(Injector);
  private readonly mpConfig = inject(ConfigStateService);
  private readonly mpRouter = inject(Router);
  private readonly toaster = inject(ToasterService);

  protected editingSection: MpSection | null = null;
  protected confirmVisible = false;
  /** UI-only toggle derived from interpreterVendorName (no backend boolean). */
  protected needsInterpreter = false;
  protected firmName = '';
  protected states: MpOption[] = [];
  protected languages: MpOption[] = [];

  /** Pre-edit form snapshot so Cancel reverts a section's changes. */
  private snapshot: Record<string, unknown> | null = null;

  override ngOnInit(): void {
    super.ngOnInit(); // loads /patients/me or /external-users/me into the inherited form
    document.body.classList.add('redesign-shell');
    this.loadFirmName();
    this.loadLookups();
  }

  ngOnDestroy(): void {
    document.body.classList.remove('redesign-shell');
  }

  protected get isPatient(): boolean {
    return !this.isExternalUserNonPatient;
  }

  // ---- header / display ----
  /** Hero name: the patient's loaded name, or the firm-aware name for AA/DA/CE. */
  protected get profileDisplayName(): string {
    return this.isPatient ? this.patientName : this.navDisplayName;
  }
  protected get patientName(): string {
    const parts = [this.form.get('firstName')?.value, this.form.get('lastName')?.value];
    return parts.filter(Boolean).join(' ').trim() || this.displayUserName;
  }
  protected get initials(): string {
    const parts = this.profileDisplayName.trim().split(/\s+/).filter(Boolean);
    const a = parts[0]?.charAt(0) ?? '?';
    const b = parts.length > 1 ? (parts[parts.length - 1].charAt(0) ?? '') : '';
    return (a + b).toUpperCase();
  }
  protected get avatarColor(): string {
    const name = this.profileDisplayName || 'User';
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
      hash = (hash * 31 + name.charCodeAt(i)) % 360;
    }
    return `hsl(${hash}, 42%, 42%)`;
  }
  protected get userEmail(): string {
    const email = (this.mpConfig.getOne('currentUser') as { email?: string } | null)?.email;
    return email || String(this.form.get('email')?.value ?? '');
  }
  protected get roleLabel(): string {
    return this.displayRoleName;
  }
  protected get navDisplayName(): string {
    const u = this.mpConfig.getOne('currentUser') as {
      name?: string;
      surname?: string;
      userName?: string;
    } | null;
    return resolveExternalUserDisplayName(u?.name, u?.surname, this.firmName, u?.userName);
  }

  // ---- field display helpers ----
  protected fv(name: string): string {
    const v = this.form.get(name)?.value;
    return v === null || v === undefined || v === '' ? '' : String(v);
  }
  protected genderLabel(): string {
    const id = this.form.get('genderId')?.value;
    return this.genderOptions.find((o) => o.value === id)?.key ?? '';
  }
  protected phoneTypeLabel(): string {
    const id = this.form.get('phoneNumberTypeId')?.value;
    return this.phoneNumberTypeOptions.find((o) => o.value === id)?.key ?? '';
  }
  protected stateLabel(): string {
    const id = this.form.get('stateId')?.value;
    return this.states.find((o) => o.id === id)?.label ?? this.fv('stateId');
  }
  protected languageLabel(): string {
    const id = this.form.get('appointmentLanguageId')?.value;
    return this.languages.find((o) => o.id === id)?.label ?? this.fv('appointmentLanguageId');
  }
  protected interpreterSummary(): string {
    const vendor = this.fv('interpreterVendorName');
    return vendor ? `Yes -- ${vendor}` : 'No';
  }
  /** DOB stored as a full ISO datetime; show + edit only the YYYY-MM-DD date part. */
  protected dobDisplay(): string {
    return this.fv('dateOfBirth').slice(0, 10);
  }

  // ---- per-section edit + confirm ----
  protected startEdit(section: MpSection): void {
    this.snapshot = this.form.getRawValue();
    if (section === 'personal') {
      // Native <input type="date"> needs YYYY-MM-DD; the loaded value carries a
      // time component. Trim it so the picker populates (and the save sends a
      // date the API accepts).
      const dob = this.form.get('dateOfBirth');
      const v = dob?.value;
      if (typeof v === 'string' && v.length > 10) {
        dob!.setValue(v.slice(0, 10));
      }
    } else if (section === 'preferences') {
      this.needsInterpreter = !!this.form.get('interpreterVendorName')?.value;
    }
    this.editingSection = section;
  }
  protected cancelEdit(): void {
    if (this.snapshot) {
      this.form.patchValue(this.snapshot);
    }
    this.editingSection = null;
  }
  protected requestSave(): void {
    this.confirmVisible = true;
  }
  protected cancelConfirm(): void {
    this.confirmVisible = false;
  }

  /**
   * Commit the edited section. Mirrors the inherited save() PUT but exposes a
   * success hook (the base subscribes internally with no callback) so we can
   * close the editor + toast. One PUT persists the whole form; non-edited
   * sections carry their loaded values. SSN stays null (never-clear).
   */
  protected confirmSave(): void {
    if (this.form.invalid || !this.selected?.patient?.id) {
      this.form.markAllAsTouched();
      this.confirmVisible = false;
      return;
    }
    const raw = this.form.getRawValue();
    if (this.editingSection === 'preferences' && !this.needsInterpreter) {
      raw.interpreterVendorName = null;
    }
    this.isBusy = true;
    this.mpRest
      .request<PatientUpdateDto, PatientDto>(
        {
          method: 'PUT',
          url: '/api/app/patients/me',
          body: {
            ...raw,
            dateOfBirth: raw.dateOfBirth ?? undefined,
            concurrencyStamp: this.selected.patient.concurrencyStamp,
          } as PatientUpdateDto,
        },
        { apiName: 'Default' },
      )
      .pipe(finalize(() => (this.isBusy = false)))
      .subscribe({
        next: (updated) => {
          if (this.selected?.patient) {
            this.selected.patient = { ...this.selected.patient, ...updated };
          }
          this.editingSection = null;
          this.confirmVisible = false;
          this.snapshot = null;
          this.toaster.success('Profile changes saved.');
        },
        error: () => {
          this.confirmVisible = false;
        },
      });
  }

  // ---- navbar / account actions ----
  protected onDocuments(): void {
    void this.mpRouter.navigateByUrl('/');
  }
  protected backHome(): void {
    void this.mpRouter.navigateByUrl('/');
  }
  protected onLogout(): void {
    void performFullLogout(this.mpInjector);
  }
  /** Deep-link to the AuthServer account page (password lives there). */
  protected changePassword(): void {
    const issuer = String(environment.oAuthConfig?.issuer ?? '').replace(/\/$/, '');
    window.location.href = `${issuer}/Account/Manage`;
  }

  // ---- lookups + firm name ----
  private loadLookups(): void {
    const map = (r: PagedResultDto<LookupDto<string>>): MpOption[] =>
      (r.items ?? [])
        .filter((i) => !!i.id)
        .map((i) => ({ id: i.id as string, label: i.displayName ?? '' }));
    this.mpRest
      .request<unknown, PagedResultDto<LookupDto<string>>>(
        {
          method: 'GET',
          url: '/api/app/patients/state-lookup',
          params: { maxResultCount: 1000, skipCount: 0 },
        },
        { apiName: 'Default' },
      )
      .subscribe({ next: (r) => (this.states = map(r)), error: () => undefined });
    this.mpRest
      .request<unknown, PagedResultDto<LookupDto<string>>>(
        {
          method: 'GET',
          url: '/api/app/patients/appointment-language-lookup',
          params: { maxResultCount: 1000, skipCount: 0 },
        },
        { apiName: 'Default' },
      )
      .subscribe({ next: (r) => (this.languages = map(r)), error: () => undefined });
  }

  private loadFirmName(): void {
    this.mpRest
      .request<
        unknown,
        { firmName?: string }
      >({ method: 'GET', url: '/api/app/external-users/me' }, { apiName: 'Default' })
      .subscribe({
        next: (p) => (this.firmName = p?.firmName ?? ''),
        error: () => undefined,
      });
  }
}
