import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input, OnDestroy, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { PagedResultDto, RestService } from '@abp/ng.core';
import { Subscription } from 'rxjs';
import type { LookupDto } from '../../proxy/shared/models';

/**
 * Front-end transient shape for the "add injury" booking-form modal.
 * Bundles the AppointmentInjuryDetail core fields with the linked
 * PrimaryInsurance + ClaimExaminer rows so the user enters all three
 * in one modal step; the parent splits the nested shape across the
 * three dedicated endpoints (appointment-injury-details +
 * appointment-primary-insurances + appointment-claim-examiners) at
 * submit time via persistInjuryDraftsIfProvided.
 *
 * Re-exported from this section so the parent (and any future
 * consumer that reads from injuryDrafts) imports the type from a
 * single place.
 */
export interface AppointmentInjuryDraft {
  isCumulativeInjury: boolean;
  dateOfInjury: string | null;
  toDateOfInjury: string | null;
  claimNumber: string;
  wcabOfficeId: string | null;
  wcabAdj: string | null;
  bodyPartsSummary: string;
  primaryInsurance: {
    isActive: boolean;
    name: string | null;
    suite: string | null;
    attention: string | null;
    phoneNumber: string | null;
    faxNumber: string | null;
    street: string | null;
    city: string | null;
    stateId: string | null;
    zip: string | null;
  };
  claimExaminer: {
    isActive: boolean;
    name: string | null;
    email: string | null;
    phoneNumber: string | null;
    fax: string | null;
    street: string | null;
    suite: string | null;
    city: string | null;
    stateId: string | null;
    zip: string | null;
  };
}

/**
 * Pre-fill values for the Claim Examiner section when the booker is
 * the Claim Examiner role on a fresh appointment. Computed by the
 * parent (`isClaimExaminerRole && !isItAdmin`) and passed in null when
 * the prefill should not apply. Mirrors OLD parity:
 * appointment-add.component.ts:145-149.
 */
export interface ClaimExaminerPrefill {
  name: string | null;
  email: string | null;
}

/**
 * #121 phase T4 (2026-05-13) -- Claim Information section, extracted
 * from AppointmentAddComponent. Largest extraction in the
 * decomposition (~250 TS lines + 513 HTML lines). Owns:
 *
 *   - the in-memory `injuryDrafts` array (passed in by reference;
 *     parent consumes at submit time via persistInjuryDraftsIfProvided
 *     and at the Bug C email fan-out resolver)
 *   - the modal state (open / close, edit index, error banner)
 *   - the per-injury FLAT FormGroup (W2-10) -- field-name keys match
 *     the OLD-parity Playwright fill helper at
 *     .playwright-mcp/book-appointment-fill.js exactly
 *   - the wcabOffice + state lookup arrays, loaded on first modal open
 *   - the cumulative-injury toggle + Insurance / Claim Examiner
 *     conditional-validators wiring (OLD customValdiation*)
 *
 * Parent retains:
 *   - the `injuryDrafts` array reference (data; consumed at submit)
 *   - role-derived booleans (isInsuranceFieldsetDisabled,
 *     isClaimExaminerReadOnly) passed in as Inputs
 *   - the CE-role prefill values passed in as Input (or null when
 *     not applicable)
 *
 * The serialization round-trip between FLAT FormGroup keys and the
 * NESTED `AppointmentInjuryDraft` shape happens inside
 * `serializeInjuryForm` so the parent's submit handler can keep
 * iterating the array unchanged.
 */
@Component({
  selector: 'app-appointment-add-claim-information',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './appointment-add-claim-information.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppointmentAddClaimInformationComponent implements OnDestroy {
  @Input({ required: true }) injuryDrafts!: AppointmentInjuryDraft[];
  @Input() isInsuranceFieldsetDisabled = false;
  @Input() isClaimExaminerReadOnly = false;
  @Input() claimExaminerPrefill: ClaimExaminerPrefill | null = null;

  private readonly fb = inject(FormBuilder);
  private readonly restService = inject(RestService);

  // Field-init order matters: injuryToggleSubscriptions must exist
  // before buildInjuryForm runs (it pushes into the array).
  private injuryToggleSubscriptions: Subscription[] = [];
  injuryForm: FormGroup = this.buildInjuryForm();
  isInjuryModalOpen = false;
  injuryEditingIndex = -1;
  injuryModalError: string | null = null;
  wcabOfficeOptions: LookupDto<string>[] = [];
  injuryStateOptions: LookupDto<string>[] = [];

  ngOnDestroy(): void {
    this.clearInjuryToggleSubscriptions();
  }

  /**
   * W2-10 (2026-05-07): builds the reactive FormGroup that backs the
   * per-injury modal. FLAT shape (not nested under primaryInsurance /
   * claimExaminer) because formControlName cannot traverse nested
   * groups without formGroupName scopes; serialization back into the
   * nested AppointmentInjuryDraft happens in saveInjuryModal.
   *
   * Always-required: dateOfInjury, claimNumber, bodyPartsSummary.
   * Conditionally required: injuryInsuranceName when insurance toggle
   * is on; 8 CE fields when CE toggle is on.
   */
  private buildInjuryForm(initial?: AppointmentInjuryDraft): FormGroup {
    const src = initial ?? this.makeEmptyInjuryDraft();
    const group = this.fb.group({
      injuryCumulative: [src.isCumulativeInjury],
      injuryDateOfInjury: [src.dateOfInjury, [Validators.required]],
      injuryToDateOfInjury: [src.toDateOfInjury],
      injuryClaimNumber: [src.claimNumber, [Validators.required]],
      injuryBodyPartsSummary: [src.bodyPartsSummary, [Validators.required]],
      injuryWcabOfficeId: [src.wcabOfficeId],
      injuryWcabAdj: [src.wcabAdj],
      injuryInsuranceEnabled: [src.primaryInsurance.isActive],
      injuryInsuranceName: [src.primaryInsurance.name],
      injuryInsuranceAttention: [src.primaryInsurance.attention],
      injuryInsurancePhone: [src.primaryInsurance.phoneNumber],
      injuryInsuranceFax: [src.primaryInsurance.faxNumber],
      injuryInsuranceStreet: [src.primaryInsurance.street],
      injuryInsuranceSte: [src.primaryInsurance.suite],
      injuryInsuranceCity: [src.primaryInsurance.city],
      injuryInsuranceStateId: [src.primaryInsurance.stateId],
      injuryInsuranceZip: [src.primaryInsurance.zip],
      injuryClaimExaminerEnabled: [src.claimExaminer.isActive],
      injuryClaimExaminerName: [src.claimExaminer.name],
      injuryClaimExaminerEmail: [src.claimExaminer.email],
      injuryClaimExaminerPhone: [src.claimExaminer.phoneNumber],
      injuryClaimExaminerFax: [src.claimExaminer.fax],
      injuryClaimExaminerStreet: [src.claimExaminer.street],
      injuryClaimExaminerSte: [src.claimExaminer.suite],
      injuryClaimExaminerCity: [src.claimExaminer.city],
      injuryClaimExaminerStateId: [src.claimExaminer.stateId],
      injuryClaimExaminerZip: [src.claimExaminer.zip],
    });

    this.clearInjuryToggleSubscriptions();

    this.applyInsuranceRequiredValidators(
      group,
      group.get('injuryInsuranceEnabled')?.value === true,
    );
    this.applyClaimExaminerRequiredValidators(
      group,
      group.get('injuryClaimExaminerEnabled')?.value === true,
    );

    const insuranceSub = group
      .get('injuryInsuranceEnabled')
      ?.valueChanges.subscribe((on) => this.applyInsuranceRequiredValidators(group, on === true));
    if (insuranceSub) this.injuryToggleSubscriptions.push(insuranceSub);

    const examinerSub = group
      .get('injuryClaimExaminerEnabled')
      ?.valueChanges.subscribe((on) =>
        this.applyClaimExaminerRequiredValidators(group, on === true),
      );
    if (examinerSub) this.injuryToggleSubscriptions.push(examinerSub);

    return group;
  }

  private makeEmptyInjuryDraft(): AppointmentInjuryDraft {
    return {
      isCumulativeInjury: false,
      dateOfInjury: null,
      toDateOfInjury: null,
      claimNumber: '',
      wcabOfficeId: null,
      wcabAdj: null,
      bodyPartsSummary: '',
      primaryInsurance: {
        isActive: true,
        name: null,
        suite: null,
        attention: null,
        phoneNumber: null,
        faxNumber: null,
        street: null,
        city: null,
        stateId: null,
        zip: null,
      },
      claimExaminer: {
        isActive: true,
        name: null,
        email: null,
        phoneNumber: null,
        fax: null,
        street: null,
        suite: null,
        city: null,
        stateId: null,
        zip: null,
      },
    };
  }

  /** OLD parity: customValdiationForPrimaryInsurance -- toggles
   * Validators.required on injuryInsuranceName based on the include
   * switch. */
  private applyInsuranceRequiredValidators(group: FormGroup, required: boolean): void {
    const ctrl = group.get('injuryInsuranceName');
    if (!ctrl) return;
    if (required) {
      ctrl.setValidators([Validators.required]);
    } else {
      ctrl.clearValidators();
    }
    ctrl.updateValueAndValidity({ emitEvent: false });
  }

  /** OLD parity: customValdiationForClaimExaminer -- 8 fields toggle
   * Validators.required when the include switch is on (email also gets
   * Validators.email). STE stays optional. */
  private applyClaimExaminerRequiredValidators(group: FormGroup, required: boolean): void {
    const fields: Array<{ key: string; extra?: any[] }> = [
      { key: 'injuryClaimExaminerName' },
      { key: 'injuryClaimExaminerEmail', extra: [Validators.email] },
      { key: 'injuryClaimExaminerPhone' },
      { key: 'injuryClaimExaminerFax' },
      { key: 'injuryClaimExaminerStreet' },
      { key: 'injuryClaimExaminerCity' },
      { key: 'injuryClaimExaminerStateId' },
      { key: 'injuryClaimExaminerZip' },
    ];
    for (const f of fields) {
      const ctrl = group.get(f.key);
      if (!ctrl) continue;
      if (required) {
        ctrl.setValidators([Validators.required, ...(f.extra ?? [])]);
      } else {
        ctrl.clearValidators();
      }
      ctrl.updateValueAndValidity({ emitEvent: false });
    }
  }

  private clearInjuryToggleSubscriptions(): void {
    for (const s of this.injuryToggleSubscriptions) {
      s.unsubscribe();
    }
    this.injuryToggleSubscriptions = [];
  }

  /** Serialize the flat FormGroup value back into the nested
   * AppointmentInjuryDraft shape `injuryDrafts[]` carries. */
  private serializeInjuryForm(group: FormGroup): AppointmentInjuryDraft {
    const v = group.getRawValue();
    return {
      isCumulativeInjury: v.injuryCumulative === true,
      dateOfInjury: v.injuryDateOfInjury ?? null,
      toDateOfInjury: v.injuryToDateOfInjury ?? null,
      claimNumber: v.injuryClaimNumber ?? '',
      wcabOfficeId: v.injuryWcabOfficeId ?? null,
      wcabAdj: v.injuryWcabAdj ?? null,
      bodyPartsSummary: v.injuryBodyPartsSummary ?? '',
      primaryInsurance: {
        isActive: v.injuryInsuranceEnabled === true,
        name: v.injuryInsuranceName ?? null,
        suite: v.injuryInsuranceSte ?? null,
        attention: v.injuryInsuranceAttention ?? null,
        phoneNumber: v.injuryInsurancePhone ?? null,
        faxNumber: v.injuryInsuranceFax ?? null,
        street: v.injuryInsuranceStreet ?? null,
        city: v.injuryInsuranceCity ?? null,
        stateId: v.injuryInsuranceStateId ?? null,
        zip: v.injuryInsuranceZip ?? null,
      },
      claimExaminer: {
        isActive: v.injuryClaimExaminerEnabled === true,
        name: v.injuryClaimExaminerName ?? null,
        email: v.injuryClaimExaminerEmail ?? null,
        phoneNumber: v.injuryClaimExaminerPhone ?? null,
        fax: v.injuryClaimExaminerFax ?? null,
        street: v.injuryClaimExaminerStreet ?? null,
        suite: v.injuryClaimExaminerSte ?? null,
        city: v.injuryClaimExaminerCity ?? null,
        stateId: v.injuryClaimExaminerStateId ?? null,
        zip: v.injuryClaimExaminerZip ?? null,
      },
    };
  }

  loadInjuryLookups(): void {
    if (this.wcabOfficeOptions.length === 0) {
      this.restService
        .request<any, PagedResultDto<LookupDto<string>>>(
          {
            method: 'GET',
            url: '/api/app/appointment-injury-details/wcab-office-lookup',
            params: { skipCount: 0, maxResultCount: 200 },
          },
          { apiName: 'Default' },
        )
        .subscribe({ next: (r) => (this.wcabOfficeOptions = r?.items ?? []) });
    }
    if (this.injuryStateOptions.length === 0) {
      this.restService
        .request<any, PagedResultDto<LookupDto<string>>>(
          {
            method: 'GET',
            url: '/api/app/applicant-attorneys/state-lookup',
            params: { skipCount: 0, maxResultCount: 200 },
          },
          { apiName: 'Default' },
        )
        .subscribe({ next: (r) => (this.injuryStateOptions = r?.items ?? []) });
    }
  }

  openAddInjuryModal(): void {
    this.injuryEditingIndex = -1;
    this.injuryForm = this.buildInjuryForm();
    this.applyClaimExaminerRolePrefill();
    this.loadInjuryLookups();
    this.injuryModalError = null;
    this.isInjuryModalOpen = true;
  }

  /**
   * OLD parity (appointment-add.component.ts:145-149): when the booker
   * is an Adjuster (NEW = Claim Examiner role) on a fresh appointment,
   * pre-fill the per-injury claim examiner row with the logged-in
   * user's name + email. The parent decides whether to pass a prefill
   * (it sets claimExaminerPrefill = null when the role gate fails);
   * this section just applies whatever it receives.
   */
  private applyClaimExaminerRolePrefill(): void {
    if (!this.claimExaminerPrefill) return;
    const nameCtrl = this.injuryForm.get('injuryClaimExaminerName');
    const emailCtrl = this.injuryForm.get('injuryClaimExaminerEmail');
    const enabledCtrl = this.injuryForm.get('injuryClaimExaminerEnabled');
    if (nameCtrl) {
      nameCtrl.setValue(this.claimExaminerPrefill.name ?? nameCtrl.value);
    }
    if (emailCtrl) {
      emailCtrl.setValue(this.claimExaminerPrefill.email ?? emailCtrl.value);
    }
    if (enabledCtrl) {
      enabledCtrl.setValue(true);
    }
  }

  openEditInjuryModal(index: number): void {
    const existing = this.injuryDrafts[index];
    if (!existing) return;
    const cloned: AppointmentInjuryDraft = JSON.parse(JSON.stringify(existing));
    this.injuryForm = this.buildInjuryForm(cloned);
    this.injuryEditingIndex = index;
    this.loadInjuryLookups();
    this.injuryModalError = null;
    this.isInjuryModalOpen = true;
  }

  closeInjuryModal(): void {
    this.isInjuryModalOpen = false;
    this.injuryEditingIndex = -1;
    this.clearInjuryToggleSubscriptions();
    this.injuryForm = this.buildInjuryForm();
    this.injuryModalError = null;
  }

  saveInjuryModal(): void {
    this.injuryForm.markAllAsTouched();
    if (this.injuryForm.invalid) {
      this.injuryModalError = 'Please complete the required fields highlighted below.';
      return;
    }
    this.injuryModalError = null;
    const draft = this.serializeInjuryForm(this.injuryForm);
    if (this.injuryEditingIndex >= 0) {
      this.injuryDrafts[this.injuryEditingIndex] = draft;
    } else {
      this.injuryDrafts.push(draft);
    }
    this.closeInjuryModal();
  }

  removeInjury(index: number): void {
    if (index >= 0 && index < this.injuryDrafts.length) {
      this.injuryDrafts.splice(index, 1);
    }
  }

  injuryWcabOfficeName(id: string | null | undefined): string {
    if (!id) return '';
    const opt = this.wcabOfficeOptions.find((o) => o.id === id);
    return opt?.displayName ?? '';
  }
}
