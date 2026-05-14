import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { LocalizationPipe, PagedResultDto } from '@abp/ng.core';
import { AppLookupSelectComponent } from '../../shared/components/app-lookup-select.component';
import { NgbDatepickerModule, NgbDateStruct } from '@ng-bootstrap/ng-bootstrap';
import { NgxMaskDirective } from 'ngx-mask';
import { Observable } from 'rxjs';
import { genderOptions } from '../../proxy/enums/gender.enum';
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
    ReactiveFormsModule,
    LocalizationPipe,
    AppLookupSelectComponent,
    NgbDatepickerModule,
    NgxMaskDirective,
  ],
  templateUrl: './appointment-add-patient-demographics.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppointmentAddPatientDemographicsComponent {
  @Input({ required: true }) form!: FormGroup;
  @Input({ required: true }) isExternalUserNonPatient = false;
  @Input({ required: true }) isItAdmin = false;
  @Input({ required: true }) patientListCache: LookupDto<string>[] = [];
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

  readonly genderOptions = genderOptions;
}
