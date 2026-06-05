import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input, inject } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { PagedResultDto, RestService } from '@abp/ng.core';
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
  /**
   * OBS-41 (2026-05-27): structured per-body-part descriptions. The
   * authoritative list; `bodyPartsSummary` is derived from it (comma-join)
   * on submit so existing readers (view cell, repo filter-text, reschedule
   * cloner) keep working. Description-only -- no body-part lookup (deferred).
   */
  bodyParts: string[];
  bodyPartsSummary: string;
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
export class AppointmentAddClaimInformationComponent {
  @Input({ required: true }) injuryDrafts!: AppointmentInjuryDraft[];

  private readonly fb = inject(FormBuilder);
  private readonly restService = inject(RestService);

  injuryForm: FormGroup = this.buildInjuryForm();

  isInjuryModalOpen = false;
  injuryEditingIndex = -1;
  injuryModalError: string | null = null;
  wcabOfficeOptions: LookupDto<string>[] = [];
  injuryStateOptions: LookupDto<string>[] = [];

  /**
   * W2-10 (2026-05-07): builds the reactive FormGroup that backs the
   * per-injury modal. FLAT shape (not nested under primaryInsurance /
   * claimExaminer) because formControlName cannot traverse nested
   * groups without formGroupName scopes; serialization back into the
   * nested AppointmentInjuryDraft happens in saveInjuryModal.
   *
   * Always-required: dateOfInjury, claimNumber, bodyPartsSummary, wcabAdj (CI3).
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
      // OBS-41: repeatable structured body parts. Seed one required row when
      // empty so the modal always shows at least one input; saveInjuryModal
      // blocks submit until every row is filled.
      injuryBodyParts: this.fb.array(
        src.bodyParts && src.bodyParts.length > 0
          ? src.bodyParts.map((d) =>
              this.fb.control<string | null>(d, [Validators.required, Validators.maxLength(500)]),
            )
          : [
              this.fb.control<string | null>(null, [
                Validators.required,
                Validators.maxLength(500),
              ]),
            ],
      ),
      injuryWcabOfficeId: [src.wcabOfficeId],
      injuryWcabAdj: [src.wcabAdj, [Validators.required]],
    });

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
      bodyParts: [],
      bodyPartsSummary: '',
    };
  }

  /** Serialize the flat FormGroup value back into the nested
   * AppointmentInjuryDraft shape `injuryDrafts[]` carries. */
  private serializeInjuryForm(group: FormGroup): AppointmentInjuryDraft {
    const v = group.getRawValue();
    // OBS-41: structured rows are authoritative; derive the legacy summary
    // (comma-join) so existing readers keep working. Cap at 500 to satisfy
    // AppointmentInjuryDetail.BodyPartsSummary's NotNull/500 constraint.
    const bodyParts = ((v.injuryBodyParts as Array<string | null>) ?? [])
      .map((d) => (d ?? '').trim())
      .filter((d) => d.length > 0);
    return {
      isCumulativeInjury: v.injuryCumulative === true,
      dateOfInjury: v.injuryDateOfInjury ?? null,
      toDateOfInjury: v.injuryToDateOfInjury ?? null,
      claimNumber: v.injuryClaimNumber ?? '',
      wcabOfficeId: v.injuryWcabOfficeId ?? null,
      wcabAdj: v.injuryWcabAdj ?? null,
      bodyParts,
      bodyPartsSummary: bodyParts.join(', ').slice(0, 500),
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
    this.loadInjuryLookups();
    this.injuryModalError = null;
    this.isInjuryModalOpen = true;
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

  /** OBS-41: the structured body-part rows FormArray on the current injury form. */
  get bodyPartsArray(): FormArray {
    return this.injuryForm.get('injuryBodyParts') as FormArray;
  }

  addBodyPart(): void {
    this.bodyPartsArray.push(
      this.fb.control<string | null>(null, [Validators.required, Validators.maxLength(500)]),
    );
  }

  removeBodyPart(index: number): void {
    // Keep at least one row so the section never collapses to empty
    // (>= 1 body part is required, mirroring OLD's mandatory body parts).
    if (this.bodyPartsArray.length > 1) {
      this.bodyPartsArray.removeAt(index);
    }
  }

  injuryWcabOfficeName(id: string | null | undefined): string {
    if (!id) return '';
    const opt = this.wcabOfficeOptions.find((o) => o.id === id);
    return opt?.displayName ?? '';
  }
}
