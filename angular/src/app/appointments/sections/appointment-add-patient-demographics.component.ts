import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { FormGroup, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { LocalizationPipe, PagedResultDto } from '@abp/ng.core';
import { AppLookupSelectComponent } from '../../shared/components/app-lookup-select.component';
import {
  NgbDatepickerModule,
  NgbDateStruct,
  NgbTypeaheadModule,
  NgbTypeaheadSelectItemEvent,
} from '@ng-bootstrap/ng-bootstrap';
import { SsnInputComponent } from '../../shared/components/ssn-input.component';
import {
  AddressAutocompleteComponent,
  AddressFieldMap,
} from '../../shared/address/address-autocomplete.component';
import { Observable } from 'rxjs';
import { Gender, genderOptions } from '../../proxy/enums/gender.enum';
import type { LookupDto, LookupRequestDto } from '../../proxy/shared/models';

/**
 * #121 phase T6 (2026-05-13) -- Patient Demographics + Address section.
 * Extracted from `AppointmentAddComponent`. 23 FormControls (firstName,
 * lastName, middleName, genderId, dateOfBirth, email, cellPhoneNumber,
 * phoneNumber, phoneNumberTypeId, socialSecurityNumber, street, address,
 * city, stateId, zipCode, appointmentLanguageId, needsInterpreter,
 * interpreterVendorName, refferedBy, patientId, identityUserId).
 *
 * State ownership:
 *   - parent  -> every FormControl, plus the patient profile state
 *                (currentPatientProfile, patientLabel, patientLoadMessage,
 *                patientListCache, isProfileLoading) and every patient-
 *                touching method (loadCurrentPatientProfile,
 *                loadPatientByEmail, loadPatientProfile,
 *                getOrCreatePatientForAppointment,
 *                formatDateOfBirthForApi, normalizePatientDateOfBirth,
 *                onPatientSelected, onPatientEmailInputChanged). Submit-
 *                time logic still reads `this.form.getRawValue()` and
 *                `this.currentPatientProfile`, so nothing changes about
 *                where that data lives.
 *   - parent  -> the section visibility decision -- this section always
 *                renders for booker flows. No `@if` wrapper at parent.
 *   - child   -> template rendering only, plus the local
 *                `genderOptions` template alias for the radio loop.
 *
 * Inputs are intentionally a flat list of primitives + the form
 * reference + two lookup fns -- matching the T3 / T5 sibling-section
 * pattern. The two action handlers are surfaced as `@Output()` events
 * (patientSelected, patientEmailChanged) so the parent retains full
 * control over the patient-lookup HTTP roundtrips.
 *
 * Trade-off: this is a minimum-viable template extraction. A deeper
 * refactor could move the patient lookup into a child-owned service,
 * but submit-time reads of `currentPatientProfile` make that a larger
 * @ViewChild plumbing exercise. Out of scope for T6; revisit when the
 * lookup logic needs to be reused outside of the booker form.
 */
@Component({
  selector: 'app-appointment-add-patient-demographics',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    LocalizationPipe,
    AppLookupSelectComponent,
    NgbDatepickerModule,
    NgbTypeaheadModule,
    SsnInputComponent,
    AddressAutocompleteComponent,
  ],
  templateUrl: './appointment-add-patient-demographics.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppointmentAddPatientDemographicsComponent {
  @Input({ required: true }) form!: FormGroup;

  // F2 (2026-05-29): control-name map for the patient address autocomplete.
  // "Unit #" is the `address` control; state is the StateId <select>.
  readonly addressFields: AddressFieldMap = {
    street: 'street',
    suite: 'address',
    city: 'city',
    state: 'stateId',
    zip: 'zipCode',
  };

  @Input({ required: true }) isExternalUserNonPatient = false;
  @Input({ required: true }) isItAdmin = false;
  /**
   * 2026-06-11 (PII): NgbTypeahead source for the "find existing patient"
   * email search. Owned by the parent (where the HTTP roundtrip + debounce
   * live, per the template-only section contract); the server returns nothing
   * until 2 chars and scopes results to patients the booker has already worked
   * with, so this never surfaces a default list of every patient's email.
   */
  @Input({ required: true }) searchPatientByEmail!: (
    text$: Observable<string>,
  ) => Observable<LookupDto<string>[]>;
  /**
   * 2026-06-11: whether the patient Email field is required (drives the label
   * asterisk). Owned + recomputed by the parent so the "*" mirrors the actual
   * conditional requirement (patient-is-booker OR self-represented) instead of
   * always showing. Default true (the safe/required state).
   */
  @Input() patientEmailRequired = true;
  @Input({ required: true }) patientLoadMessage = '';
  @Input({ required: true }) dobMinDate!: NgbDateStruct;
  @Input({ required: true }) dobMaxDate!: NgbDateStruct;
  @Input({ required: true }) getStateLookup!: (
    input: LookupRequestDto,
  ) => Observable<PagedResultDto<LookupDto<string>>>;
  @Input({ required: true }) getAppointmentLanguageLookup!: (
    input: LookupRequestDto,
  ) => Observable<PagedResultDto<LookupDto<string>>>;
  @Input() isFieldInvalid: (name: string) => boolean = () => false;

  @Output() patientSelected = new EventEmitter<string | null>();
  @Output() patientEmailChanged = new EventEmitter<void>();

  // I6 (2026-06-08): drop the Gender.Unspecified (value 0) radio -- it has no
  // localized label (renders the raw "Enum:Gender.0" key) and is not a valid
  // selection.
  readonly genderOptions = genderOptions.filter((option) => option.value !== Gender.Unspecified);

  // 2026-06-11 (PII): local model for the email search box. Holds the selected
  // LookupDto (display = email) after a pick; never part of the reactive form
  // (the chosen patient id flows to the parent via patientSelected).
  patientSearchModel: LookupDto<string> | string = '';

  /** Typeahead display: show the patient's email (LookupDto.displayName). */
  readonly patientResultFormatter = (result: LookupDto<string>): string => result.displayName ?? '';

  /** Input display after a pick: the email; raw strings pass through. */
  readonly patientInputFormatter = (result: LookupDto<string> | string): string =>
    typeof result === 'string' ? result : (result.displayName ?? '');

  /** A typeahead pick raises the chosen patient id to the parent's onPatientSelected. */
  onPatientResultSelected(event: NgbTypeaheadSelectItemEvent): void {
    const item = event.item as LookupDto<string>;
    this.patientSelected.emit(item?.id ?? null);
  }
}
