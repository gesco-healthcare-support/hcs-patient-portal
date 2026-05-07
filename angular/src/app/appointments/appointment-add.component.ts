import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import {
  FormArray,
  FormBuilder,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  ConfigStateService,
  ListService,
  LocalizationPipe,
  PagedResultDto,
  RestService,
} from '@abp/ng.core';
import { DateAdapter, TimeAdapter } from '@abp/ng.theme.shared';
import { LookupSelectComponent } from '@volo/abp.commercial.ng.ui';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { finalize } from 'rxjs/operators';
import { firstValueFrom } from 'rxjs';
import { TopHeaderNavbarComponent } from '../shared/components/top-header-navbar/top-header-navbar.component';
import {
  NgbDateAdapter,
  NgbDateStruct,
  NgbDatepickerModule,
  NgbNavModule,
  NgbTimeAdapter,
} from '@ng-bootstrap/ng-bootstrap';
import type { AppointmentCreateDto } from '../proxy/appointments/models';
import { BookingStatus } from '../proxy/enums/booking-status.enum';
import { AppointmentStatusType } from '../proxy/enums/appointment-status-type.enum';
import { genderOptions } from '../proxy/enums/gender.enum';
import { phoneNumberTypeOptions } from '../proxy/enums/phone-number-type.enum';
import type {
  PatientUpdateDto,
  PatientWithNavigationPropertiesDto,
} from '../proxy/patients/models';
import type { LookupDto, LookupRequestDto } from '../proxy/shared/models';
import { AppointmentViewService } from './appointment/services/appointment.service';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';
import { CustomFieldsService } from '../proxy/custom-fields-controllers/custom-fields.service';
import type { CustomFieldDto, CustomFieldValueInputDto } from '../proxy/custom-fields/models';
import { CustomFieldType } from '../proxy/enums/custom-field-type.enum';

// W2-8 -- transient front-end shape for the "add injury" booking-form
// modal. Bundles the AppointmentInjuryDetail core fields with the
// linked PrimaryInsurance + ClaimExaminer rows so the user can enter
// all three in one modal step; on submit the booking flow splits them
// across the dedicated endpoints (appointment-injury-details +
// appointment-primary-insurances + appointment-claim-examiners).
//
// Pre-regen this shape lived in
// `proxy/appointment-injury-details/models.ts` as `AppointmentInjuryDraft`.
// The post-merge backend dropped the C# class, so the proxy regen
// removed it. This is a frontend-only transient type, not an API DTO --
// declared locally to keep the modal logic intact without a
// non-functional proxy round-trip.
interface AppointmentInjuryDraft {
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
    insuranceNumber: string | null;
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
    claimExaminerNumber: string | null;
    city: string | null;
    stateId: string | null;
    zip: string | null;
  };
}

// W2-5: per-AppointmentType field-config row, returned by
// GET /api/app/appointment-type-field-configs/by-appointment-type/:id.
// Inlined here until the auto-generated proxy is regenerated via
// `abp generate-proxy` post-W2-5 ship.
type AppointmentTypeFieldConfigDto = {
  id: string;
  tenantId?: string | null;
  appointmentTypeId: string;
  fieldName: string;
  hidden: boolean;
  readOnly: boolean;
  defaultValue?: string | null;
};

type ExternalAuthorizedUserOption = {
  identityUserId: string;
  firstName: string;
  lastName: string;
  email: string;
  userRole: string;
};

type AppointmentAuthorizedUserDraft = {
  id?: string;
  identityUserId: string;
  firstName: string;
  lastName: string;
  email: string;
  userRole: string;
  accessTypeId: number;
};

@Component({
  selector: 'app-appointment-add',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    NgxDatatableModule,
    LocalizationPipe,
    TopHeaderNavbarComponent,
    LookupSelectComponent,
    NgxValidateCoreModule,
    NgbDatepickerModule,
    NgbNavModule,
  ],
  providers: [
    ListService,
    AppointmentViewService,
    { provide: NgbDateAdapter, useClass: DateAdapter },
    { provide: NgbTimeAdapter, useClass: TimeAdapter },
  ],
  templateUrl: './appointment-add.component.html',
  styleUrls: ['./appointment-add.component.scss'],
})
export class AppointmentAddComponent {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly configState = inject(ConfigStateService);
  private readonly restService = inject(RestService);
  // B1 (2026-05-05): per-AppointmentType custom-field catalog fetcher.
  private readonly customFieldsService = inject(CustomFieldsService);

  // B1: expose enum to template for *ngIf branches.
  readonly CustomFieldType = CustomFieldType;

  // B8 (2026-05-06): NgbDatepicker defaults to a +/-10-year navigation
  // window. For DOB we want the full century. Setting [minDate]/[maxDate]
  // and `navigation="select"` switches the header to month + year selects
  // that span the full configured range.
  readonly dobMinDate: NgbDateStruct = { year: 1920, month: 1, day: 1 };
  readonly dobMaxDate: NgbDateStruct = (() => {
    const today = new Date();
    return { year: today.getFullYear(), month: today.getMonth() + 1, day: today.getDate() };
  })();

  activeTabId = 'appointment';
  isSaving = false;
  isProfileLoading = true;
  patientLabel = '';
  patientLoadMessage = '';
  isLocationSelected = false;
  checkForAppointmentTypeSelected = false;
  isAvailableDatesLoading = false;

  // W2-5: per-AppointmentType field-config state. The booker form fetches the
  // matching config set on AppointmentType selection and applies Hidden /
  // ReadOnly / DefaultValue to the FormControls below. The Set is also
  // exposed for HTML to drive [hidden] bindings via isFieldHidden().
  private readonly hiddenFieldNames = new Set<string>();
  private readonly readOnlyFieldNames = new Set<string>();
  private fieldConfigsRequestVersion = 0;
  private readonly availableDateKeys = new Set<string>();
  private readonly availableSlotsByDate = new Map<
    string,
    Array<{ time: string; doctorAvailabilityId: string }>
  >();
  private availableSlotsRequestVersion = 0;
  readonly minimumBookingDays = 3;
  readonly minimumBookingRuleMessage = `You can book appointment after ${this.minimumBookingDays} days of today's date.`;
  appointmentTimeOptions: Array<{ value: string; label: string; doctorAvailabilityId: string }> =
    [];
  readonly genderOptions = genderOptions;
  readonly phoneNumberTypeOptions = phoneNumberTypeOptions;
  readonly accessTypeOptions = [
    { value: 23, label: 'View' },
    { value: 24, label: 'Edit' },
  ];
  private currentPatientProfile?: PatientWithNavigationPropertiesDto;
  patientListCache: LookupDto<string>[] = [];
  externalAuthorizedUserOptions: ExternalAuthorizedUserOption[] = [];
  applicantAttorneyEmailSearch = '';
  applicantAttorneyOptions: ExternalAuthorizedUserOption[] = [];
  isApplicantAttorneyLoading = false;
  applicantAttorneyId: string | null = null;
  applicantAttorneyConcurrencyStamp: string | null = null;
  defenseAttorneyEmailSearch = '';
  defenseAttorneyOptions: ExternalAuthorizedUserOption[] = [];
  isDefenseAttorneyLoading = false;
  defenseAttorneyId: string | null = null;
  defenseAttorneyConcurrencyStamp: string | null = null;

  // W2-8 -- Claim Information (injury workflow). Multi-injury support per OLD:
  // booker can add multiple AppointmentInjuryDetails (each with its own insurance
  // + claim examiner) to a single appointment via the modal. injuryDrafts holds
  // the in-memory list rendered as a table; injuryEditing holds the row being
  // edited in the modal (or a fresh draft for "Add").
  // W-H-1 / D-1 (Adrian 2026-04-30): Path B re-evaluation. The route enters
  // here as either `?type=1` (Initial) or `?type=2` (Re-evaluation). For
  // MVP we surface the distinction via the page heading + a flag carried
  // through to the submit payload. Future enhancements (filter
  // AppointmentType lookup to PQMEREEVAL/AMEREEVAL only, surface a
  // "prior appointment" picker, send a different email subject) hook off
  // this same flag.
  isReevaluation = false;

  injuryDrafts: AppointmentInjuryDraft[] = [];
  isInjuryModalOpen = false;
  injuryEditingIndex = -1;
  injuryEditing: AppointmentInjuryDraft = this.makeEmptyInjuryDraft();
  // W-A-4 (2026-04-30): inline validation error displayed inside the
  // Claim Information modal when the booker clicks Add/Save with required
  // fields blank. Previously saveInjuryModal silently returned, leaving the
  // booker confused why the Add button "did nothing".
  injuryModalError: string | null = null;
  wcabOfficeOptions: LookupDto<string>[] = [];
  injuryStateOptions: LookupDto<string>[] = [];
  appointmentAuthorizedUsers: AppointmentAuthorizedUserDraft[] = [];
  isAuthorizedUserModalOpen = false;
  authorizedUserModalMode: 'create' | 'edit' = 'create';
  editingAuthorizedUserIndex = -1;
  selectedAuthorizedUser: ExternalAuthorizedUserOption | null = null;
  readonly authorizedUserForm = this.fb.group({
    identityUserId: [null as string | null, [Validators.required]],
    accessTypeId: [23, [Validators.required]],
  });

  readonly title = '::Menu:Appointments';

  readonly getAppointmentStatusLookup = (input: LookupRequestDto) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>(
      {
        method: 'GET',
        url: '/api/app/appointments/appointment-status-lookup',
        params: {
          filter: input.filter,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: 'Default' },
    );

  readonly getAppointmentTypeLookup = (input: LookupRequestDto) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>(
      {
        method: 'GET',
        url: '/api/app/appointments/appointment-type-lookup',
        params: {
          filter: input.filter,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: 'Default' },
    );

  readonly getLocationLookup = (input: LookupRequestDto) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>(
      {
        method: 'GET',
        url: '/api/app/appointments/location-lookup',
        params: {
          filter: input.filter,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: 'Default' },
    );

  readonly getStateLookup = (input: LookupRequestDto) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>(
      {
        method: 'GET',
        url: '/api/app/patients/state-lookup',
        params: {
          filter: input.filter,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: 'Default' },
    );

  readonly getAppointmentLanguageLookup = (input: LookupRequestDto) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>(
      {
        method: 'GET',
        url: '/api/app/patients/appointment-language-lookup',
        params: {
          filter: input.filter,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: 'Default' },
    );

  readonly getPatientLookup = (input: LookupRequestDto) =>
    this.restService.request<any, PagedResultDto<LookupDto<string>>>(
      {
        method: 'GET',
        url: '/api/app/appointments/patient-lookup',
        params: {
          filter: input.filter,
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      { apiName: 'Default' },
    );

  readonly form = this.fb.group({
    panelNumber: [null as string | null, [Validators.maxLength(50)]],
    appointmentDate: [null as string | null, [Validators.required]],
    requestConfirmationNumber: ['A' as string | null, [Validators.maxLength(50)]],
    dueDate: [null as string | null],
    patientId: [null as string | null, [Validators.required]],
    identityUserId: [null as string | null, [Validators.required]],
    appointmentTypeId: [null as string | null, [Validators.required]],
    locationId: [null as string | null, [Validators.required]],
    appointmentTime: [null as string | null, [Validators.required]],
    doctorAvailabilityId: [null as string | null, [Validators.required]],
    firstName: [null as string | null, [Validators.required, Validators.maxLength(50)]],
    lastName: [null as string | null, [Validators.required, Validators.maxLength(50)]],
    middleName: [null as string | null, [Validators.maxLength(50)]],
    // 2026-05-07 (#14): drop `disabled: true`. Disabled controls skip
    // validators, so the previous shape silently bypassed Validators.required
    // for Patient bookers (their loadPatientProfile path patched a value but
    // never enabled the control). The HTML now uses [readonly] per OLD
    // parity to gate editing -- readonly preserves submit + validation.
    email: [
      null as string | null,
      [Validators.required, Validators.maxLength(50), Validators.email],
    ],
    genderId: [null as number | null],
    // OLD parity (live audit 2026-05-07): DOB is required for every
    // external role per OLD's "Mandatory Fields" submit modal.
    dateOfBirth: [null as string | null, [Validators.required]],
    cellPhoneNumber: [null as string | null, [Validators.maxLength(12)]],
    phoneNumber: [null as string | null, [Validators.maxLength(20)]],
    phoneNumberTypeId: [null as number | null],
    socialSecurityNumber: [null as string | null, [Validators.maxLength(20)]],
    street: [null as string | null, [Validators.maxLength(255)]],
    address: [null as string | null, [Validators.maxLength(100)]],
    city: [null as string | null, [Validators.maxLength(50)]],
    stateId: [null as string | null],
    zipCode: [null as string | null, [Validators.maxLength(15)]],
    // OLD parity (live audit 2026-05-07): Language is required.
    appointmentLanguageId: [null as string | null, [Validators.required]],
    needsInterpreter: [null as boolean | null],
    interpreterVendorName: [null as string | null, [Validators.maxLength(255)]],
    refferedBy: [null as string | null, [Validators.maxLength(50)]],
    // OLD parity: Employer Name + Occupation required.
    employerName: [null as string | null, [Validators.required, Validators.maxLength(255)]],
    employerOccupation: [null as string | null, [Validators.required, Validators.maxLength(255)]],
    employerPhoneNumber: [null as string | null, [Validators.maxLength(12)]],
    employerStreet: [null as string | null, [Validators.maxLength(255)]],
    employerCity: [null as string | null, [Validators.maxLength(255)]],
    employerStateId: [null as string | null],
    employerZipCode: [null as string | null, [Validators.maxLength(10)]],
    applicantAttorneyEnabled: [true],
    applicantAttorneyIdentityUserId: [null as string | null],
    applicantAttorneyFirstName: [null as string | null, [Validators.maxLength(50)]],
    applicantAttorneyLastName: [null as string | null, [Validators.maxLength(50)]],
    applicantAttorneyEmail: [null as string | null, [Validators.maxLength(50), Validators.email]],
    applicantAttorneyFirmName: [null as string | null, [Validators.maxLength(50)]],
    applicantAttorneyWebAddress: [null as string | null, [Validators.maxLength(100)]],
    applicantAttorneyPhoneNumber: [null as string | null, [Validators.maxLength(20)]],
    applicantAttorneyFaxNumber: [null as string | null, [Validators.maxLength(19)]],
    applicantAttorneyStreet: [null as string | null, [Validators.maxLength(255)]],
    applicantAttorneyCity: [null as string | null, [Validators.maxLength(50)]],
    applicantAttorneyStateId: [null as string | null],
    applicantAttorneyZipCode: [null as string | null, [Validators.maxLength(10)]],
    // OLD parity 2026-05-06: Defense Attorney section is enabled by default
    // (matching OLD's two-attorney row with both toggles ON). Booker can
    // turn it off explicitly if not needed. Same for Claim Examiner below.
    defenseAttorneyEnabled: [true],
    defenseAttorneyIdentityUserId: [null as string | null],
    defenseAttorneyFirstName: [null as string | null, [Validators.maxLength(50)]],
    defenseAttorneyLastName: [null as string | null, [Validators.maxLength(50)]],
    defenseAttorneyEmail: [null as string | null, [Validators.maxLength(50), Validators.email]],
    defenseAttorneyFirmName: [null as string | null, [Validators.maxLength(50)]],
    defenseAttorneyWebAddress: [null as string | null, [Validators.maxLength(100)]],
    defenseAttorneyPhoneNumber: [null as string | null, [Validators.maxLength(20)]],
    defenseAttorneyFaxNumber: [null as string | null, [Validators.maxLength(19)]],
    defenseAttorneyStreet: [null as string | null, [Validators.maxLength(255)]],
    defenseAttorneyCity: [null as string | null, [Validators.maxLength(50)]],
    defenseAttorneyStateId: [null as string | null],
    defenseAttorneyZipCode: [null as string | null, [Validators.maxLength(10)]],
    // The top-level claimExaminer{Enabled,Name,Email} controls are
    // vestigial -- the actual per-injury Claim Examiner data is captured
    // in the injury modal's child FormGroup (built around line 727).
    // Flipping `claimExaminerEnabled` to true engaged a required
    // validator on `claimExaminerEmail` which has NO matching DOM input,
    // making the form unsubmittable. Stays `false` until the per-injury
    // child FormGroup gets the same OLD-parity required treatment.
    claimExaminerEnabled: [false],
    claimExaminerName: [null as string | null, [Validators.maxLength(50)]],
    claimExaminerEmail: [null as string | null, [Validators.maxLength(50), Validators.email]],
    // B1 (2026-05-05): per-AppointmentType custom-field answers. Mirrors
    // OLD's `appointment.customFieldsValues` FormArray rebuilt on
    // appointmentTypeId change. Each child FormGroup carries the static
    // CustomField metadata (id / label / type / options / mandatory)
    // alongside the booker-supplied `customFieldValue` control.
    customFieldsValues: this.fb.array([] as FormGroup[]),
  });

  // B1: monotonically-incrementing version so that an older slow lookup
  // response cannot overwrite the FormArray after the user has switched
  // AppointmentType (mirrors the same pattern at fieldConfigsRequestVersion).
  private customFieldsRequestVersion = 0;

  get customFieldsArray(): FormArray<FormGroup> {
    return this.form.get('customFieldsValues') as FormArray<FormGroup>;
  }

  constructor() {
    // W-H-1 / D-1: read ?type=2 to detect Re-evaluation flow. We use
    // queryParamMap.subscribe so deep-links + future programmatic switches
    // both flip the flag.
    this.route.queryParamMap.subscribe((params) => {
      this.isReevaluation = params.get('type') === '2';
    });

    this.form
      .get('locationId')
      ?.valueChanges.subscribe((locationId) => this.updateLocationSelection(locationId));
    this.form.get('locationId')?.valueChanges.subscribe(() => this.loadAvailableDatesBySelection());
    this.form.get('appointmentTypeId')?.valueChanges.subscribe((appointmentTypeId) => {
      this.loadAvailableDatesBySelection();
      this.applyFieldConfigsForAppointmentType(appointmentTypeId);
      // B1 (2026-05-05): rebuild the custom-field FormArray for the newly
      // selected AppointmentType. Mirrors OLD's `clearFormDataAsPerAppointmentType`
      // which re-binds `customFieldsValues` on AppointmentType change.
      this.loadCustomFieldsForAppointmentType(appointmentTypeId);
    });
    this.form
      .get('appointmentDate')
      ?.valueChanges.subscribe((value) => this.onAppointmentDateChanged(value));
    this.form
      .get('appointmentTime')
      ?.valueChanges.subscribe((value) => this.onAppointmentTimeChanged(value));
    this.updateLocationSelection(this.form.get('locationId')?.value ?? null);
    this.loadCurrentPatientProfile();
    this.loadExternalAuthorizedUsers();
    this.authorizedUserForm.get('identityUserId')?.valueChanges.subscribe((value) => {
      this.onAuthorizedUserIdentityChanged(value);
    });

    // S-NEW-2 (Adrian 2026-04-30): when the booker enables the AA or DA
    // section, the corresponding email field becomes required (in addition to
    // the existing format check). This drives the post-submit fan-out --
    // each party we name on the appointment must have a deliverable email.
    //
    // B12 (2026-05-06): also clear the email field whenever the checkbox
    // flips off so a stale typed value cannot ride along on a later submit
    // (the @if hides the input but the FormControl retains its value).
    // Use setValue(null, { emitEvent: false }) to avoid recursion through
    // the validator subscription -- emitEvent: false suppresses the
    // valueChanges event on the email field itself, not on the enabled
    // checkbox.
    this.form.get('applicantAttorneyEnabled')?.valueChanges.subscribe((enabled) => {
      this.applyConditionalEmailValidator('applicantAttorneyEmail', !!enabled);
      this.applyConditionalAttorneySectionValidators('applicantAttorney', !!enabled);
      if (!enabled) {
        this.form.get('applicantAttorneyEmail')?.setValue(null, { emitEvent: false });
      }
    });
    this.form.get('defenseAttorneyEnabled')?.valueChanges.subscribe((enabled) => {
      this.applyConditionalEmailValidator('defenseAttorneyEmail', !!enabled);
      this.applyConditionalAttorneySectionValidators('defenseAttorney', !!enabled);
      if (!enabled) {
        this.form.get('defenseAttorneyEmail')?.setValue(null, { emitEvent: false });
      }
    });
    this.form.get('claimExaminerEnabled')?.valueChanges.subscribe((enabled) => {
      this.applyConditionalEmailValidator('claimExaminerEmail', !!enabled);
      if (!enabled) {
        this.form.get('claimExaminerEmail')?.setValue(null, { emitEvent: false });
      }
    });
    // Apply once at construction for the initial enabled state.
    const aaInitialEnabled = !!this.form.get('applicantAttorneyEnabled')?.value;
    const daInitialEnabled = !!this.form.get('defenseAttorneyEnabled')?.value;
    const ceInitialEnabled = !!this.form.get('claimExaminerEnabled')?.value;
    this.applyConditionalEmailValidator('applicantAttorneyEmail', aaInitialEnabled);
    this.applyConditionalAttorneySectionValidators('applicantAttorney', aaInitialEnabled);
    this.applyConditionalEmailValidator('defenseAttorneyEmail', daInitialEnabled);
    this.applyConditionalAttorneySectionValidators('defenseAttorney', daInitialEnabled);
    this.applyConditionalEmailValidator('claimExaminerEmail', ceInitialEnabled);

    // B11-followup (2026-05-07): the earlier "hide AA/DA for CE" auto-
    // flip-off is no longer needed -- shouldShowApplicantAttorneySection
    // / shouldShowDefenseAttorneySection now always return true to match
    // OLD's behavior (see the comment on those methods).
    this.applyOwnRoleAttorneyPrefill();
  }

  /**
   * OLD parity (appointment-add.component.ts:159-162): when the booker is
   * the Applicant Attorney, pre-fill that section's email from their
   * identity. Same idea for Defense Attorney (DA email is also readonly
   * for DA-role bookers per OLD HTML line 749). The HTML pairs this with
   * `[readonly]` on those email fields so the booker cannot retype their
   * own address.
   */
  private applyOwnRoleAttorneyPrefill(): void {
    const user = this.currentUser;
    if (!user) return;
    // OLD parity: AA/DA have a single "Name" field. We map the combined
    // name into the existing firstName form control and leave lastName
    // empty so downstream display ("firstName lastName") renders the
    // single name without trailing whitespace.
    const fullName = [user.name, user.surname].filter(Boolean).join(' ').trim();
    if (this.isApplicantAttorney && !this.isItAdmin) {
      this.form.patchValue(
        {
          applicantAttorneyEmail: user.email ?? this.form.get('applicantAttorneyEmail')?.value,
          applicantAttorneyFirstName:
            fullName || this.form.get('applicantAttorneyFirstName')?.value,
          applicantAttorneyLastName: '',
        },
        { emitEvent: false },
      );
    }
    if (this.isDefenseAttorney && !this.isItAdmin) {
      this.form.patchValue(
        {
          defenseAttorneyEmail: user.email ?? this.form.get('defenseAttorneyEmail')?.value,
          defenseAttorneyFirstName: fullName || this.form.get('defenseAttorneyFirstName')?.value,
          defenseAttorneyLastName: '',
        },
        { emitEvent: false },
      );
    }
  }

  private applyConditionalEmailValidator(fieldName: string, required: boolean): void {
    const control = this.form.get(fieldName);
    if (!control) return;
    const validators = required
      ? [Validators.required, Validators.email, Validators.maxLength(50)]
      : [Validators.email, Validators.maxLength(50)];
    control.setValidators(validators);
    control.updateValueAndValidity({ emitEvent: false });
  }

  /**
   * OLD parity (live audit 2026-05-07): when the Applicant Attorney or
   * Defense Attorney "Include" toggle is on, the OLD `Mandatory Fields`
   * submit modal lists Name, Firm Name, Phone Number, Fax Number,
   * Street, City, State, Zip as required for that section. NEW only
   * required the email. Toggling the section off must strip those
   * required validators (parallel to applyConditionalEmailValidator).
   *
   * Flips Validators.required on a fixed list of non-email field names
   * keyed by the section's prefix (`applicantAttorney` / `defenseAttorney`).
   * Email itself stays handled by applyConditionalEmailValidator.
   */
  private applyConditionalAttorneySectionValidators(
    prefix: 'applicantAttorney' | 'defenseAttorney',
    required: boolean,
  ): void {
    // Field-name suffix -> existing maxLength so we preserve format checks.
    const suffixes: Array<{ name: string; maxLength: number }> = [
      { name: 'FirstName', maxLength: 50 },
      { name: 'FirmName', maxLength: 50 },
      { name: 'PhoneNumber', maxLength: 20 },
      { name: 'FaxNumber', maxLength: 19 },
      { name: 'Street', maxLength: 255 },
      { name: 'City', maxLength: 50 },
      { name: 'StateId', maxLength: 0 },
      { name: 'ZipCode', maxLength: 10 },
    ];
    for (const { name, maxLength } of suffixes) {
      const control = this.form.get(prefix + name);
      if (!control) continue;
      const validators = [];
      if (required) {
        validators.push(Validators.required);
      }
      if (maxLength > 0) {
        validators.push(Validators.maxLength(maxLength));
      }
      control.setValidators(validators);
      control.updateValueAndValidity({ emitEvent: false });
    }
  }

  /**
   * W2-5: HTML helper -- returns true when the per-AppointmentType config
   * marks this field key as hidden. Use as `[hidden]="isFieldHidden('claimNumber')"`
   * on the corresponding form-row container so the input is suppressed
   * without unmounting the FormControl. Form-rows added in W2-7 / W2-8
   * wire this binding alongside their introduction.
   */
  isFieldHidden(fieldName: string): boolean {
    return this.hiddenFieldNames.has(fieldName);
  }

  /**
   * W2-5: HTML helper -- returns true when the per-AppointmentType config
   * marks this field key as read-only. Backed by the same fetched config
   * set; complements the FormControl.disable() call below for sections
   * that need a separate visual treatment.
   */
  isFieldReadOnly(fieldName: string): boolean {
    return this.readOnlyFieldNames.has(fieldName);
  }

  /**
   * W2-5: when AppointmentType changes, reset all prior config + fetch the
   * new set + apply Hidden (state set + control disable) / ReadOnly (state
   * set + control disable) / DefaultValue (control setValue). Race-safe via
   * a request-version counter so a rapid type-change cancels the prior
   * fetch's apply.
   */
  private applyFieldConfigsForAppointmentType(appointmentTypeId: string | null): void {
    this.resetFieldConfigsState();

    if (!appointmentTypeId) {
      return;
    }

    const requestVersion = ++this.fieldConfigsRequestVersion;
    this.restService
      .request<null, AppointmentTypeFieldConfigDto[]>(
        {
          method: 'GET',
          url: `/api/app/appointment-type-field-configs/by-appointment-type/${appointmentTypeId}`,
        },
        { apiName: 'Default' },
      )
      .subscribe({
        next: (rows) => {
          if (requestVersion !== this.fieldConfigsRequestVersion) {
            // A newer AppointmentType change cancelled this one.
            return;
          }
          for (const row of rows ?? []) {
            const control = this.form.get(row.fieldName);
            if (row.hidden) {
              this.hiddenFieldNames.add(row.fieldName);
              control?.disable({ emitEvent: false });
            }
            if (row.readOnly) {
              this.readOnlyFieldNames.add(row.fieldName);
              control?.disable({ emitEvent: false });
            }
            if (
              row.defaultValue !== null &&
              row.defaultValue !== undefined &&
              row.defaultValue !== ''
            ) {
              control?.setValue(row.defaultValue, { emitEvent: false });
            }
          }
        },
      });
  }

  /**
   * Resets every field's config-driven state so a subsequent AppointmentType
   * change starts from a clean baseline. Without this, switching from PQME
   * to AME would carry over PQME's hidden/disabled fields.
   */
  private resetFieldConfigsState(): void {
    for (const fieldName of this.hiddenFieldNames) {
      this.form.get(fieldName)?.enable({ emitEvent: false });
    }
    for (const fieldName of this.readOnlyFieldNames) {
      this.form.get(fieldName)?.enable({ emitEvent: false });
    }
    this.hiddenFieldNames.clear();
    this.readOnlyFieldNames.clear();
  }

  /**
   * B1 (2026-05-05) -- when AppointmentType changes, fetch the active
   * <see cref="CustomFieldDto"/> rows for that type and rebuild the
   * `customFieldsValues` FormArray. Mirrors OLD's
   * `clearFormDataAsPerAppointmentType` (P:\PatientPortalOld\
   * patientappointment-portal\src\app\components\appointment-request\
   * appointments\add\appointment-add.component.ts:281-297) which resets
   * `appointment.customFieldsValues` on AppointmentType change.
   *
   * Race-safety pattern matches `applyFieldConfigsForAppointmentType` --
   * a `customFieldsRequestVersion` counter discards stale responses.
   *
   * Validators per type follow the renderer matrix in
   * `docs/research/stage-2-3-booking-and-view.md` section B1.4.
   */
  private loadCustomFieldsForAppointmentType(appointmentTypeId: string | null): void {
    this.customFieldsArray.clear();

    if (!appointmentTypeId) {
      return;
    }

    const requestVersion = ++this.customFieldsRequestVersion;

    this.customFieldsService.getActiveForAppointmentType(appointmentTypeId).subscribe({
      next: (rows) => {
        if (requestVersion !== this.customFieldsRequestVersion) {
          // Newer AppointmentType change cancelled this fetch.
          return;
        }
        // Rebuild from scratch -- order by DisplayOrder ascending so the
        // booker sees fields in the order IT Admin configured.
        const ordered = (rows ?? [])
          .filter((r) => r.isActive !== false)
          .sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0));
        for (const row of ordered) {
          this.customFieldsArray.push(this.buildCustomFieldGroup(row));
        }
      },
      error: () => {
        // Same race protection as success: only reset state on the
        // currently-pending request.
        if (requestVersion === this.customFieldsRequestVersion) {
          this.customFieldsArray.clear();
        }
      },
    });
  }

  /**
   * B1 -- construct one FormGroup per CustomField. Carries the static
   * metadata (id / label / type / options / mandatory / length) alongside
   * the booker-supplied `customFieldValue` control.
   */
  private buildCustomFieldGroup(row: CustomFieldDto): FormGroup {
    const validators = [];
    if (row.isMandatory) {
      validators.push(Validators.required);
    }
    if (row.fieldType === CustomFieldType.Alphanumeric && row.fieldLength) {
      validators.push(Validators.maxLength(row.fieldLength));
    }
    if (row.fieldType === CustomFieldType.Numeric) {
      // OLD's column is plain string; allow integers + decimals + leading minus.
      validators.push(Validators.pattern(/^-?\d+(\.\d+)?$/));
    }

    return this.fb.group({
      customFieldId: [row.id ?? ''],
      fieldType: [row.fieldType ?? CustomFieldType.Alphanumeric],
      fieldLabel: [row.fieldLabel ?? ''],
      fieldLength: [row.fieldLength ?? null],
      multipleValues: [row.multipleValues ?? null],
      isMandatory: [!!row.isMandatory],
      customFieldValue: [row.defaultValue ?? null, validators],
    });
  }

  /**
   * B1 -- helper consumed by the template to render Picklist / Tickbox /
   * Radio option lists. OLD stores options as a comma-separated string in
   * `MultipleValues` (no separate option entity); split + trim + drop empty.
   */
  optionsFromMultipleValues(multipleValues: string | null | undefined): string[] {
    if (!multipleValues) return [];
    return multipleValues
      .split(',')
      .map((s) => s.trim())
      .filter((s) => s.length > 0);
  }

  /**
   * B1 -- map the customFieldsValues FormArray into CustomFieldValueInputDto[]
   * for the booking POST. Each child FormGroup carries the static
   * CustomField metadata + the booker-supplied `customFieldValue` control;
   * here we serialize per-type and drop empties:
   *   - Date    : ISO yyyy-MM-dd from the NgbDateStruct (the form binds a
   *               yyyy-MM-dd string when the picker fires; OLD persisted MM/DD/YYYY
   *               but server stores it as a string column either way, so the
   *               wire format only matters for reciprocal display).
   *   - Time    : HH:mm string from the timepicker.
   *   - Tickbox : the form binds an array of selected option labels for
   *               multi-option tickboxes; we comma-join. Single-option
   *               tickboxes bind a boolean which we serialise as "true"/"false".
   *   - Other   : raw string from the control.
   */
  private serializeCustomFieldValues(): CustomFieldValueInputDto[] {
    const out: CustomFieldValueInputDto[] = [];
    for (const group of this.customFieldsArray.controls) {
      const v = group.value as {
        customFieldId?: string;
        fieldType?: CustomFieldType;
        multipleValues?: string | null;
        customFieldValue?: unknown;
      };
      if (!v.customFieldId) continue;
      const serialized = this.serializeOneCustomFieldValue(v);
      if (serialized === null || serialized === '') continue;
      out.push({ customFieldId: v.customFieldId, value: serialized });
    }
    return out;
  }

  private serializeOneCustomFieldValue(v: {
    fieldType?: CustomFieldType;
    multipleValues?: string | null;
    customFieldValue?: unknown;
  }): string | null {
    const raw = v.customFieldValue;
    if (raw === null || raw === undefined) return null;

    if (v.fieldType === CustomFieldType.Tickbox) {
      // Multi-option: array of selected option strings. Single-option:
      // boolean. Serialize uniformly to a string.
      if (Array.isArray(raw)) return raw.filter((x) => !!x).join(',');
      if (typeof raw === 'boolean') return raw ? 'true' : 'false';
      return String(raw);
    }

    if (typeof raw === 'string') return raw.trim();
    return String(raw);
  }

  get displayUserName(): string {
    const user = this.currentUser;
    if (!user) return '';
    const fullName = [user.name, user.surname].filter(Boolean).join(' ').trim();
    return fullName || user.userName || '';
  }

  get displayTenantName(): string {
    const tenant = this.currentTenant;
    return tenant?.name || tenant?.tenantName || 'Tenant';
  }

  get displayRoleName(): string {
    return this.currentUser?.roles?.[0] || 'Patient';
  }

  /**
   * True when the booker is anyone OTHER than the Patient role. Covers
   * Applicant Attorney, Defense Attorney, Claim Examiner, and internal
   * users (admin, Clinic Staff, Staff Supervisor, Doctor) booking on
   * behalf of a patient. Drives:
   *   - profile load: Patient -> /patients/me; everyone else -> /external-users/me
   *     (W-B-2 fix, 2026-04-30: previously CE + internal bookers fell through
   *     to /patients/me and got 404 because their IdentityUser has no Patient row).
   *   - patient-section behavior: non-Patient bookers create-on-behalf via
   *     /patients/for-appointment-booking; Patients self-update their own row.
   */
  get isExternalUserNonPatient(): boolean {
    const roles = this.currentUser?.roles ?? [];
    if (roles.length === 0) {
      // Unknown role at construction time -- safer to treat as "non-Patient"
      // so the form does not call /patients/me on a not-yet-loaded user
      // (the alternative is a guaranteed 404 that breaks the form globally).
      return true;
    }
    return !roles.some((r: string) => r?.toLowerCase() === 'patient');
  }

  /** True when current user is Applicant Attorney (hide load/select UI for them). */
  get isApplicantAttorney(): boolean {
    const roles = this.currentUser?.roles ?? [];
    return roles.some((r: string) => r?.toLowerCase() === 'applicant attorney');
  }

  /** True when current user is Defense Attorney. OLD parity: own email field readonly + auto-filled. */
  get isDefenseAttorney(): boolean {
    const roles = this.currentUser?.roles ?? [];
    return roles.some((r: string) => r?.toLowerCase() === 'defense attorney');
  }

  /**
   * True when current user is Claim Examiner. OLD parity: their per-injury
   * claim examiner name + email auto-fill from their identity and become
   * readonly. NEW's "Claim Examiner" role is the same as OLD's "Adjuster"
   * (renamed for clarity, see shared/auth/external-user-roles.ts).
   */
  get isClaimExaminerRole(): boolean {
    const roles = this.currentUser?.roles ?? [];
    return roles.some((r: string) => r?.toLowerCase() === 'claim examiner');
  }

  // B11 reversed (2026-05-07): the earlier interpretation hid the
  // Applicant Attorney / Defense Attorney / Additional Authorized User
  // cards for the Claim Examiner (= OLD's Adjuster) booker. A live
  // walkthrough of the OLD app under `adjuster@local.test` showed that
  // OLD shows ALL three sections to the Adjuster; only the Insurance
  // fieldset is `[disabled]` and the Claim Examiner Name + Email fields
  // auto-fill from the booker identity and become readonly (OLD
  // appointment-add.component.html:378 + :461). The methods below stay
  // for any future role-specific gating but currently always return
  // true for parity.
  shouldShowApplicantAttorneySection(): boolean {
    return true;
  }

  shouldShowDefenseAttorneySection(): boolean {
    return true;
  }

  shouldShowAuthorizedUserSection(): boolean {
    return true;
  }

  /**
   * OLD parity: when the booker is a Claim Examiner (= OLD's Adjuster),
   * the Primary Insurance fieldset is rendered but `[disabled]`. The
   * Claim Examiner sub-section is rendered with Name + Email auto-filled
   * and readonly (handled separately in the per-injury modal). Mirrors
   * OLD `appointment-add.component.html:378` `[disabled]="isAdjusterLogin"`.
   */
  get isInsuranceFieldsetDisabled(): boolean {
    return this.isClaimExaminerRole && !this.isItAdmin;
  }

  /**
   * True when current user holds the IT Admin internal role. OLD HTML uses
   * `userRoleId != roleEnum.ITAdmin` as an override that lets IT Admins
   * edit otherwise-readonly own-role email fields when booking on behalf.
   */
  get isItAdmin(): boolean {
    const roles = this.currentUser?.roles ?? [];
    return roles.some((r: string) => r?.toLowerCase() === 'it admin');
  }

  isFieldInvalid(fieldName: string): boolean {
    const field = this.form.get(fieldName);
    return field ? field.invalid && (field.dirty || field.touched) : false;
  }

  async onSubmit(): Promise<void> {
    const raw = this.form.getRawValue();
    if (this.isExternalUserNonPatient && !raw.patientId) {
      const requiredForNew = raw.firstName && raw.lastName && raw.email && raw.dateOfBirth;
      if (!requiredForNew) {
        this.patientLoadMessage =
          'To create a new patient, First Name, Last Name, Email and Date of Birth are required.';
        this.form.get('firstName')?.markAsTouched();
        this.form.get('lastName')?.markAsTouched();
        this.form.get('email')?.markAsTouched();
        this.form.get('dateOfBirth')?.markAsTouched();
        this.form.markAllAsTouched();
        return;
      }
    } else if (!this.isExternalUserNonPatient && !raw.patientId) {
      this.form.get('patientId')?.setErrors({ required: true });
      this.form.markAllAsTouched();
      return;
    }

    if (this.form.invalid) {
      this.patientLoadMessage = 'Please complete all required fields before saving.';
      Object.keys(this.form.controls).forEach((key) => {
        this.form.get(key)?.markAsTouched();
      });
      return;
    }

    this.isSaving = true;
    try {
      const rawSubmit = this.form.getRawValue();

      if (this.isExternalUserNonPatient && !rawSubmit.patientId) {
        const patientProfile = await this.getOrCreatePatientForAppointment(rawSubmit);
        if (patientProfile?.patient?.id) {
          this.currentPatientProfile = patientProfile;
          this.form.patchValue({ patientId: patientProfile.patient.id }, { emitEvent: false });
        } else {
          throw new Error('Failed to get or create patient.');
        }
      }

      await this.updatePatientProfile();

      const rawAfter = this.form.getRawValue();
      const payload: AppointmentCreateDto = {
        panelNumber: rawAfter.panelNumber ?? undefined,
        appointmentDate:
          this.combineAppointmentDateAndTime(rawAfter.appointmentDate, rawAfter.appointmentTime) ??
          undefined,
        requestConfirmationNumber: rawAfter.requestConfirmationNumber || 'A',
        dueDate: rawAfter.dueDate ?? undefined,
        appointmentStatus: AppointmentStatusType.Pending,
        patientId: rawAfter.patientId ?? '',
        identityUserId: rawAfter.identityUserId ?? '',
        appointmentTypeId: rawAfter.appointmentTypeId ?? '',
        locationId: rawAfter.locationId ?? '',
        doctorAvailabilityId: rawAfter.doctorAvailabilityId ?? '',
        // S-5.1: party emails captured at booking time so email fan-out (step 6.1)
        // and auto-link on registration (step 5.2) have the addresses immediately.
        patientEmail: rawAfter.email ?? undefined,
        applicantAttorneyEmail: rawAfter.applicantAttorneyEnabled
          ? (rawAfter.applicantAttorneyEmail ?? undefined)
          : undefined,
        defenseAttorneyEmail: rawAfter.defenseAttorneyEnabled
          ? (rawAfter.defenseAttorneyEmail ?? undefined)
          : undefined,
        claimExaminerEmail: rawAfter.claimExaminerEnabled
          ? (rawAfter.claimExaminerEmail ?? undefined)
          : undefined,
        // B1 (2026-05-05): map the FormArray into CustomFieldValueInputDto[].
        // Empty / whitespace values are dropped to match OLD's "no answer"
        // semantics; the backend AppService also drops them defensively.
        customFieldValues: this.serializeCustomFieldValues(),
      };

      const createdAppointment = await firstValueFrom(
        this.restService.request<any, any>(
          {
            method: 'POST',
            url: '/api/app/appointments',
            body: payload,
          },
          { apiName: 'Default' },
        ),
      );

      await this.createEmployerDetailsIfProvided(createdAppointment?.id);
      await this.upsertApplicantAttorneyForAppointmentIfProvided(createdAppointment?.id);
      await this.upsertDefenseAttorneyForAppointmentIfProvided(createdAppointment?.id);
      await this.persistInjuryDraftsIfProvided(createdAppointment?.id);
      await this.createAppointmentAccessorsIfProvided(createdAppointment?.id);

      this.router.navigateByUrl('/');
    } finally {
      this.isSaving = false;
    }
  }

  save(): void {
    this.onSubmit();
  }

  reset(): void {
    this.form.reset();
    this.updateLocationSelection(null);
    this.clearTimeSlots();
  }

  goBack(): void {
    this.router.navigateByUrl('/');
  }

  cancel(): void {
    this.goBack();
  }

  openMyProfile(): void {
    this.router.navigateByUrl('/doctor-management/patients/my-profile');
  }

  clearAppointmentDate(): void {
    this.form.patchValue(
      { appointmentDate: null, appointmentTime: null, doctorAvailabilityId: null },
      { emitEvent: false },
    );
    this.clearTimeSlots();
  }

  readonly markAppointmentDateDisabled = (date: NgbDateStruct): boolean => {
    if (!this.checkForAppointmentTypeSelected) {
      return false;
    }

    if (this.isBeforeMinimumBookingDate(date)) {
      return true;
    }

    if (this.availableDateKeys.size === 0) {
      return true;
    }

    return !this.availableDateKeys.has(this.toDateKey(date.year, date.month, date.day));
  };

  readonly isAvailableAppointmentDate = (date: NgbDateStruct): boolean =>
    this.availableDateKeys.has(this.toDateKey(date.year, date.month, date.day));

  get showMinimumBookingRuleWarning(): boolean {
    if (this.isAvailableDatesLoading) {
      return false;
    }

    const selectedDate = this.toDateKeyFromControl(this.form.get('appointmentDate')?.value ?? null);
    if (!selectedDate) {
      return false;
    }

    return this.isBeforeMinimumBookingDateKey(selectedDate);
  }

  clearDueDate(): void {
    this.form.patchValue({ dueDate: null });
  }

  private loadCurrentPatientProfile(): void {
    if (this.isExternalUserNonPatient) {
      this.loadExternalUserProfile();
    } else {
      this.loadPatientProfile();
    }
  }

  private loadExternalUserProfile(): void {
    this.restService
      .request<
        any,
        {
          identityUserId: string;
          firstName: string;
          lastName: string;
          email: string;
          userRole?: string;
        }
      >(
        {
          method: 'GET',
          url: '/api/app/external-users/me',
        },
        { apiName: 'Default' },
      )
      .pipe(finalize(() => (this.isProfileLoading = false)))
      .subscribe((profile) => {
        if (!profile?.identityUserId) {
          return;
        }
        this.patientLabel = [profile.firstName, profile.lastName].filter(Boolean).join(' ').trim();
        // 2026-05-07 (#14): the email control is no longer disabled by
        // default (see form-build site), so an explicit enable() here is
        // redundant. The HTML applies [readonly] for Patient bookers to
        // gate editing without skipping validators.
        this.form.get('patientId')?.clearValidators();
        this.form.get('patientId')?.updateValueAndValidity({ emitEvent: false });
        this.loadPatientListCache();
        this.form.patchValue({
          identityUserId: profile.identityUserId ?? this.currentUser?.id ?? null,
          patientId: null,
          firstName: null,
          lastName: null,
          middleName: null,
          email: null,
          genderId: null,
          dateOfBirth: null,
          cellPhoneNumber: null,
          phoneNumber: null,
          phoneNumberTypeId: null,
          socialSecurityNumber: null,
          street: null,
          address: null,
          city: null,
          stateId: null,
          zipCode: null,
          appointmentLanguageId: null,
          interpreterVendorName: null,
          needsInterpreter: null,
          refferedBy: null,
          employerName: null,
          employerOccupation: null,
          employerPhoneNumber: null,
          employerStreet: null,
          employerCity: null,
          employerStateId: null,
          employerZipCode: null,
        });
        const identityUserId = profile.identityUserId ?? this.currentUser?.id ?? null;
        const isApplicantAttorney =
          profile.userRole?.toLowerCase() === 'applicant attorney' ||
          (this.currentUser?.roles ?? []).some(
            (r: string) => r?.toLowerCase() === 'applicant attorney',
          );
        if (isApplicantAttorney && identityUserId) {
          this.loadApplicantAttorneyForCurrentUser(identityUserId);
        } else {
          this.form.patchValue({
            applicantAttorneyIdentityUserId: null,
            applicantAttorneyFirstName: null,
            applicantAttorneyLastName: null,
            applicantAttorneyEmail: null,
            applicantAttorneyFirmName: null,
            applicantAttorneyWebAddress: null,
            applicantAttorneyPhoneNumber: null,
            applicantAttorneyFaxNumber: null,
            applicantAttorneyStreet: null,
            applicantAttorneyCity: null,
            applicantAttorneyStateId: null,
            applicantAttorneyZipCode: null,
          });
          this.applicantAttorneyId = null;
          this.applicantAttorneyConcurrencyStamp = null;
        }
      });
  }

  private loadPatientListCache(): void {
    this.getPatientLookup({
      filter: '',
      skipCount: 0,
      maxResultCount: 500,
    }).subscribe(({ items }) => {
      this.patientListCache = items ?? [];
    });
  }

  private loadPatientProfile(): void {
    this.restService
      .request<any, PatientWithNavigationPropertiesDto>(
        {
          method: 'GET',
          url: '/api/app/patients/me',
        },
        { apiName: 'Default' },
      )
      .pipe(finalize(() => (this.isProfileLoading = false)))
      .subscribe((profile) => {
        const patient = profile?.patient;
        if (!patient?.id) {
          return;
        }

        this.currentPatientProfile = profile;
        this.patientLabel = [patient.firstName, patient.lastName].filter(Boolean).join(' ').trim();
        this.form.patchValue({
          patientId: patient.id,
          identityUserId: patient.identityUserId ?? this.currentUser?.id ?? null,
          firstName: patient.firstName ?? null,
          lastName: patient.lastName ?? null,
          middleName: patient.middleName ?? null,
          email: patient.email ?? null,
          genderId: (patient.genderId as number | undefined) ?? null,
          dateOfBirth: this.normalizePatientDateOfBirth(patient.dateOfBirth as string | null),
          cellPhoneNumber: patient.cellPhoneNumber ?? null,
          phoneNumber: patient.phoneNumber ?? null,
          phoneNumberTypeId: (patient.phoneNumberTypeId as number | undefined) ?? null,
          socialSecurityNumber: patient.socialSecurityNumber ?? null,
          street: patient.street ?? null,
          address: patient.address ?? null,
          city: patient.city ?? null,
          stateId: patient.stateId ?? null,
          zipCode: patient.zipCode ?? null,
          appointmentLanguageId: patient.appointmentLanguageId ?? null,
          interpreterVendorName: patient.interpreterVendorName ?? null,
          needsInterpreter: !!patient.interpreterVendorName,
          refferedBy: patient.refferedBy ?? null,
          employerName: null,
          employerOccupation: null,
          employerPhoneNumber: null,
          employerStreet: null,
          employerCity: null,
          employerStateId: null,
          employerZipCode: null,
        });
      });
  }

  private async getOrCreatePatientForAppointment(
    raw: ReturnType<typeof this.form.getRawValue>,
  ): Promise<PatientWithNavigationPropertiesDto | null> {
    const dateOfBirth = this.formatDateOfBirthForApi(raw.dateOfBirth);
    if (!dateOfBirth) {
      throw new Error('Date of birth is required for new patient.');
    }
    const body = {
      firstName: raw.firstName || '',
      lastName: raw.lastName || '',
      middleName: raw.middleName ?? undefined,
      email: raw.email || '',
      genderId: Number(raw.genderId ?? 0),
      dateOfBirth,
      phoneNumberTypeId: Number(raw.phoneNumberTypeId ?? 1),
      phoneNumber: raw.phoneNumber ?? undefined,
      socialSecurityNumber: raw.socialSecurityNumber ?? undefined,
      address: raw.address ?? undefined,
      city: raw.city ?? undefined,
      zipCode: raw.zipCode ?? undefined,
      refferedBy: raw.refferedBy ?? undefined,
      cellPhoneNumber: raw.cellPhoneNumber ?? undefined,
      street: raw.street ?? undefined,
      interpreterVendorName: raw.needsInterpreter
        ? (raw.interpreterVendorName ?? undefined)
        : undefined,
      stateId: raw.stateId ?? undefined,
      appointmentLanguageId: raw.appointmentLanguageId ?? undefined,
    };
    const created = await firstValueFrom(
      this.restService.request<any, PatientWithNavigationPropertiesDto | null>(
        {
          method: 'POST',
          url: '/api/app/patients/for-appointment-booking/get-or-create',
          body,
        },
        { apiName: 'Default' },
      ),
    );

    if (created?.patient?.id) {
      return created;
    }

    // Some backend flows may return 204 without body; fetch by email as fallback.
    return firstValueFrom(
      this.restService.request<any, PatientWithNavigationPropertiesDto | null>(
        {
          method: 'GET',
          url: '/api/app/patients/for-appointment-booking/by-email',
          params: { email: raw.email || '' },
        },
        { apiName: 'Default' },
      ),
    );
  }

  async loadPatientByEmail(): Promise<void> {
    const email = this.form.get('email')?.value?.trim();
    if (!email) {
      return;
    }
    this.patientLoadMessage = '';
    this.isProfileLoading = true;
    try {
      const profile = await firstValueFrom(
        this.restService.request<any, PatientWithNavigationPropertiesDto | null>(
          {
            method: 'GET',
            url: '/api/app/patients/for-appointment-booking/by-email',
            params: { email },
          },
          { apiName: 'Default' },
        ),
      );
      if (profile?.patient?.id) {
        this.currentPatientProfile = profile;
        this.patientLabel = [profile.patient.firstName, profile.patient.lastName]
          .filter(Boolean)
          .join(' ')
          .trim();
        this.form.patchValue({
          patientId: profile.patient.id,
          identityUserId: profile.patient.identityUserId ?? null,
          firstName: profile.patient.firstName ?? null,
          lastName: profile.patient.lastName ?? null,
          middleName: profile.patient.middleName ?? null,
          email: profile.patient.email ?? null,
          genderId: (profile.patient.genderId as number | undefined) ?? null,
          dateOfBirth: this.normalizePatientDateOfBirth(
            profile.patient.dateOfBirth as string | null,
          ),
          cellPhoneNumber: profile.patient.cellPhoneNumber ?? null,
          phoneNumber: profile.patient.phoneNumber ?? null,
          phoneNumberTypeId: (profile.patient.phoneNumberTypeId as number | undefined) ?? null,
          socialSecurityNumber: profile.patient.socialSecurityNumber ?? null,
          street: profile.patient.street ?? null,
          address: profile.patient.address ?? null,
          city: profile.patient.city ?? null,
          stateId: profile.patient.stateId ?? null,
          zipCode: profile.patient.zipCode ?? null,
          appointmentLanguageId: profile.patient.appointmentLanguageId ?? null,
          interpreterVendorName: profile.patient.interpreterVendorName ?? null,
          needsInterpreter: !!profile.patient.interpreterVendorName,
          refferedBy: profile.patient.refferedBy ?? null,
        });
        this.patientLoadMessage = 'Patient loaded. You can edit details below if needed.';
      } else {
        this.form.patchValue({ patientId: null }, { emitEvent: false });
        this.currentPatientProfile = undefined;
        this.patientLabel = '';
        this.patientLoadMessage =
          'No patient found with this email. Fill in the form below to create a new patient.';
      }
    } catch {
      this.patientLoadMessage =
        'Unable to load patient. Please try again or fill in the form to create new.';
    } finally {
      this.isProfileLoading = false;
    }
  }

  private formatDateOfBirthForApi(value: unknown): string | null {
    if (!value) return null;
    if (typeof value === 'string') return value;
    const obj = value as { year?: number; month?: number; day?: number };
    if (obj?.year && obj?.month && obj?.day) {
      const d = new Date(obj.year, obj.month - 1, obj.day);
      return d.toISOString().split('T')[0];
    }
    return null;
  }

  private normalizePatientDateOfBirth(value: string | null | undefined): string | null {
    if (!value) return null;
    const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
    if (!match) return null;
    const year = Number(match[1]);
    const month = Number(match[2]);
    const day = Number(match[3]);
    if (year < 1900) return null;
    const today = new Date();
    if (year === today.getFullYear() && month === today.getMonth() + 1 && day === today.getDate()) {
      return null;
    }
    return value;
  }

  onPatientSelected(patientId: string | null): void {
    if (!this.isExternalUserNonPatient) {
      return;
    }

    if (!patientId) {
      this.form.patchValue(
        {
          patientId: null,
          identityUserId: this.currentUser?.id ?? null,
          firstName: null,
          lastName: null,
          middleName: null,
          email: null,
          genderId: null,
          dateOfBirth: null,
          cellPhoneNumber: null,
          phoneNumber: null,
          phoneNumberTypeId: null,
          socialSecurityNumber: null,
          street: null,
          address: null,
          city: null,
          stateId: null,
          zipCode: null,
          appointmentLanguageId: null,
          interpreterVendorName: null,
          needsInterpreter: null,
          refferedBy: null,
        },
        { emitEvent: false },
      );
      this.currentPatientProfile = undefined;
      this.patientLabel = '';
      this.patientLoadMessage = '';
      return;
    }
    this.patientLoadMessage = '';
    this.restService
      .request<any, PatientWithNavigationPropertiesDto>(
        {
          method: 'GET',
          url: `/api/app/patients/for-appointment-booking/${patientId}`,
        },
        { apiName: 'Default' },
      )
      .subscribe((profile) => {
        const patient = profile?.patient;
        if (!patient?.id) {
          return;
        }
        this.currentPatientProfile = profile;
        this.patientLabel = [patient.firstName, patient.lastName].filter(Boolean).join(' ').trim();
        this.form.patchValue({
          patientId: patient.id,
          identityUserId: patient.identityUserId ?? null,
          firstName: patient.firstName ?? null,
          lastName: patient.lastName ?? null,
          middleName: patient.middleName ?? null,
          email: patient.email ?? null,
          genderId: (patient.genderId as number | undefined) ?? null,
          dateOfBirth: this.normalizePatientDateOfBirth(patient.dateOfBirth as string | null),
          cellPhoneNumber: patient.cellPhoneNumber ?? null,
          phoneNumber: patient.phoneNumber ?? null,
          phoneNumberTypeId: (patient.phoneNumberTypeId as number | undefined) ?? null,
          socialSecurityNumber: patient.socialSecurityNumber ?? null,
          street: patient.street ?? null,
          address: patient.address ?? null,
          city: patient.city ?? null,
          stateId: patient.stateId ?? null,
          zipCode: patient.zipCode ?? null,
          appointmentLanguageId: patient.appointmentLanguageId ?? null,
          interpreterVendorName: patient.interpreterVendorName ?? null,
          needsInterpreter: !!patient.interpreterVendorName,
          refferedBy: patient.refferedBy ?? null,
        });
      });
  }

  onPatientEmailInputChanged(): void {
    if (!this.isExternalUserNonPatient) {
      return;
    }

    const selectedPatient = this.currentPatientProfile?.patient;
    if (!selectedPatient?.id) {
      return;
    }

    const email = (this.form.get('email')?.value ?? '').trim().toLowerCase();
    const selectedEmail = (selectedPatient.email ?? '').trim().toLowerCase();
    if (email === selectedEmail) {
      return;
    }

    this.onPatientSelected(null);
  }

  private hasEmployerDetails(raw: ReturnType<typeof this.form.getRawValue>): boolean {
    return !!(
      raw.employerName ||
      raw.employerOccupation ||
      raw.employerPhoneNumber ||
      raw.employerStreet ||
      raw.employerCity ||
      raw.employerStateId ||
      raw.employerZipCode
    );
  }

  private async createEmployerDetailsIfProvided(appointmentId?: string): Promise<void> {
    const raw = this.form.getRawValue();
    if (!appointmentId || !this.hasEmployerDetails(raw)) {
      return;
    }

    if (!raw.employerName || !raw.employerOccupation) {
      return;
    }

    await firstValueFrom(
      this.restService.request<any, any>(
        {
          method: 'POST',
          url: '/api/app/appointment-employer-details',
          body: {
            appointmentId,
            employerName: raw.employerName,
            occupation: raw.employerOccupation,
            phoneNumber: raw.employerPhoneNumber ?? undefined,
            street: raw.employerStreet ?? undefined,
            city: raw.employerCity ?? undefined,
            stateId: raw.employerStateId ?? undefined,
            zipCode: raw.employerZipCode ?? undefined,
          },
        },
        { apiName: 'Default' },
      ),
    );
  }

  openAddAuthorizedUserModal(): void {
    this.authorizedUserModalMode = 'create';
    this.editingAuthorizedUserIndex = -1;
    this.selectedAuthorizedUser = null;
    this.authorizedUserForm.reset({ identityUserId: null, accessTypeId: 23 });
    this.isAuthorizedUserModalOpen = true;
  }

  openEditAuthorizedUserModal(index: number): void {
    const item = this.appointmentAuthorizedUsers[index];
    if (!item) {
      return;
    }

    this.authorizedUserModalMode = 'edit';
    this.editingAuthorizedUserIndex = index;
    this.authorizedUserForm.reset({
      identityUserId: item.identityUserId,
      accessTypeId: item.accessTypeId,
    });
    this.selectedAuthorizedUser =
      this.externalAuthorizedUserOptions.find((x) => x.identityUserId === item.identityUserId) ??
      null;
    this.isAuthorizedUserModalOpen = true;
  }

  closeAuthorizedUserModal(): void {
    this.isAuthorizedUserModalOpen = false;
  }

  saveAuthorizedUserFromModal(): void {
    if (this.authorizedUserForm.invalid || !this.selectedAuthorizedUser) {
      this.authorizedUserForm.markAllAsTouched();
      return;
    }

    const raw = this.authorizedUserForm.getRawValue();
    const identityUserId = raw.identityUserId ?? '';
    const accessTypeId = Number(raw.accessTypeId ?? 23);

    const duplicateIndex = this.appointmentAuthorizedUsers.findIndex(
      (x, i) => x.identityUserId === identityUserId && i !== this.editingAuthorizedUserIndex,
    );
    if (duplicateIndex >= 0) {
      return;
    }

    const mapped: AppointmentAuthorizedUserDraft = {
      id:
        this.authorizedUserModalMode === 'edit'
          ? this.appointmentAuthorizedUsers[this.editingAuthorizedUserIndex]?.id
          : undefined,
      identityUserId: this.selectedAuthorizedUser.identityUserId,
      firstName: this.selectedAuthorizedUser.firstName,
      lastName: this.selectedAuthorizedUser.lastName,
      email: this.selectedAuthorizedUser.email,
      userRole: this.selectedAuthorizedUser.userRole,
      accessTypeId,
    };

    if (this.authorizedUserModalMode === 'edit' && this.editingAuthorizedUserIndex >= 0) {
      this.appointmentAuthorizedUsers[this.editingAuthorizedUserIndex] = mapped;
    } else {
      this.appointmentAuthorizedUsers.push(mapped);
    }

    this.closeAuthorizedUserModal();
  }

  removeAuthorizedUser(index: number): void {
    this.appointmentAuthorizedUsers.splice(index, 1);
  }

  getAccessTypeLabel(value: number): string {
    return this.accessTypeOptions.find((x) => x.value === value)?.label ?? '';
  }

  private onAuthorizedUserIdentityChanged(identityUserId: string | null): void {
    this.selectedAuthorizedUser =
      this.externalAuthorizedUserOptions.find((x) => x.identityUserId === identityUserId) ?? null;
  }

  private loadExternalAuthorizedUsers(): void {
    this.restService
      .request<any, { items: ExternalAuthorizedUserOption[] }>(
        {
          method: 'GET',
          url: '/api/public/external-signup/external-user-lookup',
        },
        { apiName: 'Default' },
      )
      .subscribe({
        next: (result) => {
          this.externalAuthorizedUserOptions = result?.items ?? [];
          this.applicantAttorneyOptions = (result?.items ?? []).filter(
            (x: ExternalAuthorizedUserOption) => x.userRole?.toLowerCase() === 'applicant attorney',
          );
          this.defenseAttorneyOptions = (result?.items ?? []).filter(
            (x: ExternalAuthorizedUserOption) => x.userRole?.toLowerCase() === 'defense attorney',
          );
        },
      });
  }

  onApplicantAttorneyEmailSearch(event: Event): void {
    this.applicantAttorneyEmailSearch = (event.target as HTMLInputElement)?.value?.trim() ?? '';
  }

  loadApplicantAttorneyByEmail(): void {
    const email = this.applicantAttorneyEmailSearch?.trim();
    if (!email) return;
    this.isApplicantAttorneyLoading = true;
    this.restService
      .request<
        any,
        {
          applicantAttorneyId?: string;
          identityUserId: string;
          firstName: string;
          lastName: string;
          email: string;
          firmName?: string;
          webAddress?: string;
          phoneNumber?: string;
          faxNumber?: string;
          street?: string;
          city?: string;
          stateId?: string;
          zipCode?: string;
          concurrencyStamp?: string;
        } | null
      >(
        {
          method: 'GET',
          url: '/api/app/appointments/applicant-attorney-details-for-booking',
          params: { email },
        },
        { apiName: 'Default' },
      )
      .pipe(finalize(() => (this.isApplicantAttorneyLoading = false)))
      .subscribe({
        next: (data) => {
          if (data) {
            this.applicantAttorneyId = data.applicantAttorneyId ?? null;
            this.applicantAttorneyConcurrencyStamp = data.concurrencyStamp ?? null;
            this.form.patchValue({
              applicantAttorneyIdentityUserId: data.identityUserId,
              applicantAttorneyFirstName: data.firstName ?? null,
              applicantAttorneyLastName: data.lastName ?? null,
              applicantAttorneyEmail: data.email ?? null,
              applicantAttorneyFirmName: data.firmName ?? null,
              applicantAttorneyWebAddress: data.webAddress ?? null,
              applicantAttorneyPhoneNumber: data.phoneNumber ?? null,
              applicantAttorneyFaxNumber: data.faxNumber ?? null,
              applicantAttorneyStreet: data.street ?? null,
              applicantAttorneyCity: data.city ?? null,
              applicantAttorneyStateId: data.stateId ?? null,
              applicantAttorneyZipCode: data.zipCode ?? null,
            });
          }
        },
      });
  }

  onApplicantAttorneySelected(identityUserId: string | null): void {
    if (!identityUserId) {
      this.form.patchValue({
        applicantAttorneyFirstName: null,
        applicantAttorneyLastName: null,
        applicantAttorneyEmail: null,
        applicantAttorneyFirmName: null,
        applicantAttorneyWebAddress: null,
        applicantAttorneyPhoneNumber: null,
        applicantAttorneyFaxNumber: null,
        applicantAttorneyStreet: null,
        applicantAttorneyCity: null,
        applicantAttorneyStateId: null,
        applicantAttorneyZipCode: null,
      });
      this.applicantAttorneyId = null;
      this.applicantAttorneyConcurrencyStamp = null;
      return;
    }
    this.isApplicantAttorneyLoading = true;
    this.restService
      .request<
        any,
        {
          applicantAttorneyId?: string;
          identityUserId: string;
          firstName: string;
          lastName: string;
          email: string;
          firmName?: string;
          webAddress?: string;
          phoneNumber?: string;
          faxNumber?: string;
          street?: string;
          city?: string;
          stateId?: string;
          zipCode?: string;
          concurrencyStamp?: string;
        } | null
      >(
        {
          method: 'GET',
          url: '/api/app/appointments/applicant-attorney-details-for-booking',
          params: { identityUserId },
        },
        { apiName: 'Default' },
      )
      .pipe(finalize(() => (this.isApplicantAttorneyLoading = false)))
      .subscribe({
        next: (data) => {
          if (data) {
            this.applicantAttorneyId = data.applicantAttorneyId ?? null;
            this.applicantAttorneyConcurrencyStamp = data.concurrencyStamp ?? null;
            this.form.patchValue({
              applicantAttorneyIdentityUserId: data.identityUserId,
              applicantAttorneyFirstName: data.firstName ?? null,
              applicantAttorneyLastName: data.lastName ?? null,
              applicantAttorneyEmail: data.email ?? null,
              applicantAttorneyFirmName: data.firmName ?? null,
              applicantAttorneyWebAddress: data.webAddress ?? null,
              applicantAttorneyPhoneNumber: data.phoneNumber ?? null,
              applicantAttorneyFaxNumber: data.faxNumber ?? null,
              applicantAttorneyStreet: data.street ?? null,
              applicantAttorneyCity: data.city ?? null,
              applicantAttorneyStateId: data.stateId ?? null,
              applicantAttorneyZipCode: data.zipCode ?? null,
            });
          }
        },
      });
  }

  private loadApplicantAttorneyForCurrentUser(identityUserId: string): void {
    this.restService
      .request<
        any,
        {
          applicantAttorneyId?: string;
          identityUserId: string;
          firstName: string;
          lastName: string;
          email: string;
          firmName?: string;
          webAddress?: string;
          phoneNumber?: string;
          faxNumber?: string;
          street?: string;
          city?: string;
          stateId?: string;
          zipCode?: string;
          concurrencyStamp?: string;
        } | null
      >(
        {
          method: 'GET',
          url: '/api/app/appointments/applicant-attorney-details-for-booking',
          params: { identityUserId },
        },
        { apiName: 'Default' },
      )
      .subscribe({
        next: (data) => {
          if (data) {
            this.applicantAttorneyId = data.applicantAttorneyId ?? null;
            this.applicantAttorneyConcurrencyStamp = data.concurrencyStamp ?? null;
            this.form.patchValue({
              applicantAttorneyEnabled: true,
              applicantAttorneyIdentityUserId: data.identityUserId,
              applicantAttorneyFirstName: data.firstName ?? null,
              applicantAttorneyLastName: data.lastName ?? null,
              applicantAttorneyEmail: data.email ?? null,
              applicantAttorneyFirmName: data.firmName ?? null,
              applicantAttorneyWebAddress: data.webAddress ?? null,
              applicantAttorneyPhoneNumber: data.phoneNumber ?? null,
              applicantAttorneyFaxNumber: data.faxNumber ?? null,
              applicantAttorneyStreet: data.street ?? null,
              applicantAttorneyCity: data.city ?? null,
              applicantAttorneyStateId: data.stateId ?? null,
              applicantAttorneyZipCode: data.zipCode ?? null,
            });
          }
        },
      });
  }

  private async upsertApplicantAttorneyForAppointmentIfProvided(
    appointmentId?: string,
  ): Promise<void> {
    const raw = this.form.getRawValue();
    // Bonus issue (2026-05-07): drop the IdentityUserId precondition. Send
    // the upsert whenever the AA section is enabled AND the booker typed at
    // least an email; the backend resolves IdentityUser by email or stores
    // the row with a null IdentityUserId, which the registration linkback
    // contributor patches when the AA later registers.
    if (!appointmentId || !raw.applicantAttorneyEnabled || !raw.applicantAttorneyEmail) {
      return;
    }
    const body = {
      applicantAttorneyId: this.applicantAttorneyId ?? undefined,
      // Send Guid.Empty so the backend's ResolveIdentityUserIdForBookingAsync
      // helper falls through to the email-based lookup when no existing
      // IdentityUser was matched at search time.
      identityUserId: raw.applicantAttorneyIdentityUserId ?? '00000000-0000-0000-0000-000000000000',
      firstName: raw.applicantAttorneyFirstName ?? '',
      lastName: raw.applicantAttorneyLastName ?? '',
      email: raw.applicantAttorneyEmail ?? '',
      firmName: raw.applicantAttorneyFirmName ?? undefined,
      webAddress: raw.applicantAttorneyWebAddress ?? undefined,
      phoneNumber: raw.applicantAttorneyPhoneNumber ?? undefined,
      faxNumber: raw.applicantAttorneyFaxNumber ?? undefined,
      street: raw.applicantAttorneyStreet ?? undefined,
      city: raw.applicantAttorneyCity ?? undefined,
      stateId: raw.applicantAttorneyStateId ?? undefined,
      zipCode: raw.applicantAttorneyZipCode ?? undefined,
      concurrencyStamp: this.applicantAttorneyConcurrencyStamp ?? undefined,
    };
    await firstValueFrom(
      this.restService.request<any, any>(
        {
          method: 'POST',
          url: `/api/app/appointments/${appointmentId}/applicant-attorney`,
          body,
        },
        { apiName: 'Default' },
      ),
    );
  }

  // W2-7: defense-attorney section parallel to applicant-attorney. Booker can
  // populate Both sections on the same appointment. Each section maintains
  // its own form-control prefix + cached identity/firm references.
  onDefenseAttorneyEmailSearch(event: Event): void {
    this.defenseAttorneyEmailSearch = (event.target as HTMLInputElement)?.value?.trim() ?? '';
  }

  loadDefenseAttorneyByEmail(): void {
    const email = this.defenseAttorneyEmailSearch?.trim();
    if (!email) return;
    this.isDefenseAttorneyLoading = true;
    this.restService
      .request<
        any,
        {
          defenseAttorneyId?: string;
          identityUserId: string;
          firstName: string;
          lastName: string;
          email: string;
          firmName?: string;
          webAddress?: string;
          phoneNumber?: string;
          faxNumber?: string;
          street?: string;
          city?: string;
          stateId?: string;
          zipCode?: string;
          concurrencyStamp?: string;
        } | null
      >(
        {
          method: 'GET',
          url: '/api/app/appointments/defense-attorney-details-for-booking',
          params: { email },
        },
        { apiName: 'Default' },
      )
      .pipe(finalize(() => (this.isDefenseAttorneyLoading = false)))
      .subscribe({
        next: (data) => {
          if (data) {
            this.defenseAttorneyId = data.defenseAttorneyId ?? null;
            this.defenseAttorneyConcurrencyStamp = data.concurrencyStamp ?? null;
            this.form.patchValue({
              defenseAttorneyIdentityUserId: data.identityUserId,
              defenseAttorneyFirstName: data.firstName ?? null,
              defenseAttorneyLastName: data.lastName ?? null,
              defenseAttorneyEmail: data.email ?? null,
              defenseAttorneyFirmName: data.firmName ?? null,
              defenseAttorneyWebAddress: data.webAddress ?? null,
              defenseAttorneyPhoneNumber: data.phoneNumber ?? null,
              defenseAttorneyFaxNumber: data.faxNumber ?? null,
              defenseAttorneyStreet: data.street ?? null,
              defenseAttorneyCity: data.city ?? null,
              defenseAttorneyStateId: data.stateId ?? null,
              defenseAttorneyZipCode: data.zipCode ?? null,
            });
          }
        },
      });
  }

  onDefenseAttorneySelected(identityUserId: string | null): void {
    if (!identityUserId) {
      this.form.patchValue({
        defenseAttorneyFirstName: null,
        defenseAttorneyLastName: null,
        defenseAttorneyEmail: null,
        defenseAttorneyFirmName: null,
        defenseAttorneyWebAddress: null,
        defenseAttorneyPhoneNumber: null,
        defenseAttorneyFaxNumber: null,
        defenseAttorneyStreet: null,
        defenseAttorneyCity: null,
        defenseAttorneyStateId: null,
        defenseAttorneyZipCode: null,
      });
      this.defenseAttorneyId = null;
      this.defenseAttorneyConcurrencyStamp = null;
      return;
    }
    this.isDefenseAttorneyLoading = true;
    this.restService
      .request<
        any,
        {
          defenseAttorneyId?: string;
          identityUserId: string;
          firstName: string;
          lastName: string;
          email: string;
          firmName?: string;
          webAddress?: string;
          phoneNumber?: string;
          faxNumber?: string;
          street?: string;
          city?: string;
          stateId?: string;
          zipCode?: string;
          concurrencyStamp?: string;
        } | null
      >(
        {
          method: 'GET',
          url: '/api/app/appointments/defense-attorney-details-for-booking',
          params: { identityUserId },
        },
        { apiName: 'Default' },
      )
      .pipe(finalize(() => (this.isDefenseAttorneyLoading = false)))
      .subscribe({
        next: (data) => {
          if (data) {
            this.defenseAttorneyId = data.defenseAttorneyId ?? null;
            this.defenseAttorneyConcurrencyStamp = data.concurrencyStamp ?? null;
            this.form.patchValue({
              defenseAttorneyIdentityUserId: data.identityUserId,
              defenseAttorneyFirstName: data.firstName ?? null,
              defenseAttorneyLastName: data.lastName ?? null,
              defenseAttorneyEmail: data.email ?? null,
              defenseAttorneyFirmName: data.firmName ?? null,
              defenseAttorneyWebAddress: data.webAddress ?? null,
              defenseAttorneyPhoneNumber: data.phoneNumber ?? null,
              defenseAttorneyFaxNumber: data.faxNumber ?? null,
              defenseAttorneyStreet: data.street ?? null,
              defenseAttorneyCity: data.city ?? null,
              defenseAttorneyStateId: data.stateId ?? null,
              defenseAttorneyZipCode: data.zipCode ?? null,
            });
          }
        },
      });
  }

  private async upsertDefenseAttorneyForAppointmentIfProvided(
    appointmentId?: string,
  ): Promise<void> {
    const raw = this.form.getRawValue();
    // Bonus issue (2026-05-07): mirror the AA upsert above. Submit whenever
    // the DA section is enabled AND the booker typed an email; backend
    // resolves IdentityUser by email or persists with null + linkback.
    if (!appointmentId || !raw.defenseAttorneyEnabled || !raw.defenseAttorneyEmail) {
      return;
    }
    const body = {
      defenseAttorneyId: this.defenseAttorneyId ?? undefined,
      identityUserId: raw.defenseAttorneyIdentityUserId ?? '00000000-0000-0000-0000-000000000000',
      firstName: raw.defenseAttorneyFirstName ?? '',
      lastName: raw.defenseAttorneyLastName ?? '',
      email: raw.defenseAttorneyEmail ?? '',
      firmName: raw.defenseAttorneyFirmName ?? undefined,
      webAddress: raw.defenseAttorneyWebAddress ?? undefined,
      phoneNumber: raw.defenseAttorneyPhoneNumber ?? undefined,
      faxNumber: raw.defenseAttorneyFaxNumber ?? undefined,
      street: raw.defenseAttorneyStreet ?? undefined,
      city: raw.defenseAttorneyCity ?? undefined,
      stateId: raw.defenseAttorneyStateId ?? undefined,
      zipCode: raw.defenseAttorneyZipCode ?? undefined,
      concurrencyStamp: this.defenseAttorneyConcurrencyStamp ?? undefined,
    };
    await firstValueFrom(
      this.restService.request<any, any>(
        {
          method: 'POST',
          url: `/api/app/appointments/${appointmentId}/defense-attorney`,
          body,
        },
        { apiName: 'Default' },
      ),
    );
  }

  private async createAppointmentAccessorsIfProvided(appointmentId?: string): Promise<void> {
    if (!appointmentId || this.appointmentAuthorizedUsers.length === 0) {
      return;
    }

    for (const item of this.appointmentAuthorizedUsers) {
      await firstValueFrom(
        this.restService.request<any, any>(
          {
            method: 'POST',
            url: '/api/app/appointment-accessors',
            body: {
              appointmentId,
              identityUserId: item.identityUserId,
              accessTypeId: item.accessTypeId,
            },
          },
          { apiName: 'Default' },
        ),
      );
    }
  }

  private async updatePatientProfile(): Promise<void> {
    const raw = this.form.getRawValue();
    const existing = this.currentPatientProfile?.patient;
    if (!existing?.id) {
      return;
    }
    const needsInterpreter = raw.needsInterpreter === true || `${raw.needsInterpreter}` === 'true';

    const payload: PatientUpdateDto = {
      firstName: raw.firstName || '',
      lastName: raw.lastName || '',
      middleName: raw.middleName ?? undefined,
      email: raw.email || '',
      genderId: (raw.genderId as any) ?? undefined,
      dateOfBirth: raw.dateOfBirth ?? undefined,
      phoneNumber: raw.phoneNumber ?? undefined,
      socialSecurityNumber: raw.socialSecurityNumber ?? undefined,
      address: raw.address ?? undefined,
      city: raw.city ?? undefined,
      zipCode: raw.zipCode ?? undefined,
      refferedBy: raw.refferedBy ?? undefined,
      cellPhoneNumber: raw.cellPhoneNumber ?? undefined,
      phoneNumberTypeId: (raw.phoneNumberTypeId as any) ?? undefined,
      street: raw.street ?? undefined,
      interpreterVendorName: needsInterpreter
        ? (raw.interpreterVendorName ?? undefined)
        : undefined,
      apptNumber: existing.apptNumber ?? undefined,
      othersLanguageName: existing.othersLanguageName ?? undefined,
      stateId: raw.stateId ?? undefined,
      appointmentLanguageId: raw.appointmentLanguageId ?? undefined,
      identityUserId: raw.identityUserId ?? existing.identityUserId ?? undefined,
      tenantId: existing.tenantId ?? undefined,
      concurrencyStamp: existing.concurrencyStamp,
    };

    const updateUrl = this.isExternalUserNonPatient
      ? `/api/app/patients/for-appointment-booking/${existing.id}`
      : '/api/app/patients/me';
    const updated = await firstValueFrom(
      this.restService.request<any, any>(
        {
          method: 'PUT',
          url: updateUrl,
          body: payload,
        },
        { apiName: 'Default' },
      ),
    );

    if (this.currentPatientProfile?.patient) {
      this.currentPatientProfile.patient = {
        ...this.currentPatientProfile.patient,
        ...updated,
      };
    }
  }

  private get currentUser(): {
    id?: string;
    userName?: string;
    name?: string;
    surname?: string;
    email?: string;
    roles?: string[];
  } | null {
    return (this.configState.getOne('currentUser') as any) ?? null;
  }

  private get currentTenant(): {
    name?: string;
    tenantName?: string;
  } | null {
    return (this.configState.getOne('currentTenant') as any) ?? null;
  }

  onLocationSelected(locationId: string): void {
    this.updateLocationSelection(locationId);
  }

  private updateLocationSelection(locationId: string | null): void {
    this.isLocationSelected = !!locationId;

    if (this.isLocationSelected) {
      this.form.get('appointmentDate')?.setValidators([Validators.required]);
    } else {
      this.form.patchValue({
        appointmentDate: null,
        appointmentTime: null,
        doctorAvailabilityId: null,
      });
      this.form.get('appointmentDate')?.clearValidators();
    }

    this.form.get('appointmentDate')?.updateValueAndValidity({ emitEvent: false });
    if (!this.isLocationSelected) {
      this.clearTimeSlots();
    }
  }

  private loadAvailableDatesBySelection(): void {
    const locationId = this.form.get('locationId')?.value;
    const appointmentTypeId = this.form.get('appointmentTypeId')?.value;
    this.checkForAppointmentTypeSelected = !!locationId && !!appointmentTypeId;

    if (!this.checkForAppointmentTypeSelected) {
      this.availableDateKeys.clear();
      this.availableSlotsByDate.clear();
      this.form.patchValue(
        { appointmentDate: null, appointmentTime: null, doctorAvailabilityId: null },
        { emitEvent: false },
      );
      this.clearTimeSlots();
      return;
    }

    const requestVersion = ++this.availableSlotsRequestVersion;
    this.isAvailableDatesLoading = true;

    this.fetchAllAvailableSlots(locationId as string, appointmentTypeId as string)
      .then((items) => {
        if (requestVersion !== this.availableSlotsRequestVersion) {
          return;
        }

        this.availableDateKeys.clear();
        this.availableSlotsByDate.clear();
        (items ?? []).forEach((item) => {
          const availability = item?.doctorAvailability;
          const rawDate = availability?.availableDate as string | undefined;
          const dateKey = this.toDateKeyFromApi(rawDate);
          if (dateKey) {
            if (this.isBeforeMinimumBookingDateKey(dateKey)) {
              return;
            }
            this.availableDateKeys.add(dateKey);
            const fromTime = (availability?.fromTime as string | undefined) ?? '';
            const availabilityId = (availability?.id as string | undefined) ?? '';
            if (fromTime) {
              const existingSlots = this.availableSlotsByDate.get(dateKey) ?? [];
              const exists = existingSlots.some(
                (slot) => slot.time === fromTime && slot.doctorAvailabilityId === availabilityId,
              );
              if (!exists) {
                existingSlots.push({ time: fromTime, doctorAvailabilityId: availabilityId });
                this.availableSlotsByDate.set(dateKey, existingSlots);
              }
            }
          }
        });

        const selectedDate = this.toDateKeyFromControl(
          this.form.get('appointmentDate')?.value ?? null,
        );
        if (selectedDate && !this.availableDateKeys.has(selectedDate)) {
          this.form.patchValue(
            { appointmentDate: null, appointmentTime: null, doctorAvailabilityId: null },
            { emitEvent: false },
          );
          this.clearTimeSlots();
          return;
        }

        if (selectedDate) {
          this.populateTimeSlotsForDate(selectedDate);
        }
      })
      .finally(() => {
        if (requestVersion === this.availableSlotsRequestVersion) {
          this.isAvailableDatesLoading = false;
        }
      });
  }

  private toDateKey(year: number, month: number, day: number): string {
    return `${year.toString().padStart(4, '0')}-${month.toString().padStart(2, '0')}-${day
      .toString()
      .padStart(2, '0')}`;
  }

  private toDateKeyFromApi(value?: string | null): string | null {
    if (!value) return null;
    const parsed = value.includes('T') ? value.split('T')[0] : value;
    if (parsed.length < 10) return null;
    return parsed.slice(0, 10);
  }

  private toDateKeyFromControl(value?: string | null): string | null {
    if (!value) return null;
    if (value.includes('-') && value.length >= 10) {
      return value.slice(0, 10);
    }

    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) {
      return null;
    }

    return this.toDateKey(parsed.getFullYear(), parsed.getMonth() + 1, parsed.getDate());
  }

  private onAppointmentDateChanged(value: string | null): void {
    const dateKey = this.toDateKeyFromControl(value);
    if (!dateKey || !this.availableDateKeys.has(dateKey)) {
      this.form.patchValue(
        { appointmentTime: null, doctorAvailabilityId: null },
        { emitEvent: false },
      );
      this.clearTimeSlots();
      return;
    }

    this.populateTimeSlotsForDate(dateKey);
  }

  private populateTimeSlotsForDate(dateKey: string): void {
    const slots = (this.availableSlotsByDate.get(dateKey) ?? []).sort((a, b) =>
      a.time.localeCompare(b.time),
    );
    this.appointmentTimeOptions = slots.map((slot) => ({
      value: slot.time,
      label: this.toTimeLabel(slot.time),
      doctorAvailabilityId: slot.doctorAvailabilityId,
    }));

    const selected = this.form.get('appointmentTime')?.value;
    if (!selected || !slots.some((slot) => slot.time === selected)) {
      this.form.patchValue(
        { appointmentTime: null, doctorAvailabilityId: null },
        { emitEvent: false },
      );
      return;
    }

    this.onAppointmentTimeChanged(selected);
  }

  private clearTimeSlots(): void {
    this.appointmentTimeOptions = [];
  }

  private onAppointmentTimeChanged(value: string | null): void {
    if (!value) {
      this.form.patchValue({ doctorAvailabilityId: null }, { emitEvent: false });
      return;
    }

    const selectedOption = this.appointmentTimeOptions.find((option) => option.value === value);
    this.form.patchValue(
      { doctorAvailabilityId: selectedOption?.doctorAvailabilityId ?? null },
      { emitEvent: false },
    );
  }

  private toTimeLabel(time: string): string {
    const [h = '0', m = '0'] = time.split(':');
    const hour = Number(h);
    const minute = Number(m);
    const normalizedHour = Number.isNaN(hour) ? 0 : hour;
    const normalizedMinute = Number.isNaN(minute) ? 0 : minute;
    const suffix = normalizedHour >= 12 ? 'PM' : 'AM';
    const displayHour = normalizedHour % 12 || 12;
    return `${displayHour.toString().padStart(2, '0')}:${normalizedMinute
      .toString()
      .padStart(2, '0')} ${suffix}`;
  }

  private combineAppointmentDateAndTime(
    dateValue?: string | null,
    timeValue?: string | null,
  ): string | undefined {
    const dateKey = this.toDateKeyFromControl(dateValue ?? null);
    if (!dateKey) {
      return undefined;
    }

    if (!timeValue) {
      return `${dateKey}T00:00:00`;
    }

    return `${dateKey}T${timeValue}`;
  }

  private isBeforeMinimumBookingDate(date: NgbDateStruct): boolean {
    if (!date) return false;
    const key = this.toDateKey(date.year, date.month, date.day);
    return this.isBeforeMinimumBookingDateKey(key);
  }

  private isBeforeMinimumBookingDateKey(dateKey: string): boolean {
    // Parse the date parts from the key (format: YYYY-MM-DD)
    const [year, month, day] = dateKey.split('-').map(Number);

    // Create dates in local timezone (not UTC)
    const selected = new Date(year, month - 1, day); // month is 0-indexed in JS
    selected.setHours(0, 0, 0, 0);

    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const threshold = new Date(today);
    threshold.setDate(threshold.getDate() + this.minimumBookingDays);

    // Phase 11d (2026-05-04): client-side guard is informational only.
    // The server-side BookingPolicyValidator (Phase 11b) is authoritative
    // and reads SystemParameter.AppointmentLeadTime per-tenant. UI used
    // to log a debug `console.log('Date check:', ...)` here; removed.
    return selected < threshold;
  }

  private async fetchAllAvailableSlots(
    locationId: string,
    appointmentTypeId: string,
  ): Promise<any[]> {
    const allItems: any[] = [];
    let skipCount = 0;
    const pageSize = 1000;
    let totalCount = Number.MAX_SAFE_INTEGER;

    while (skipCount < totalCount) {
      const response = await firstValueFrom(
        this.restService.request<any, PagedResultDto<any>>(
          {
            method: 'GET',
            url: '/api/app/doctor-availabilities',
            params: {
              locationId,
              appointmentTypeId,
              bookingStatusId: BookingStatus.Available,
              skipCount,
              maxResultCount: pageSize,
            },
          },
          { apiName: 'Default' },
        ),
      );

      const items = response?.items ?? [];
      totalCount = response?.totalCount ?? items.length;
      allItems.push(...items);

      if (items.length < pageSize) {
        break;
      }

      skipCount += pageSize;
    }

    return allItems;
  }

  // -------- W2-8: Claim Information modal + multi-injury workflow --------

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
        insuranceNumber: null,
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
        claimExaminerNumber: null,
        city: null,
        stateId: null,
        zip: null,
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
    this.injuryEditing = this.makeEmptyInjuryDraft();
    this.applyClaimExaminerRolePrefill();
    this.loadInjuryLookups();
    this.isInjuryModalOpen = true;
  }

  /**
   * OLD parity (appointment-add.component.ts:145-149): when the booker is
   * an Adjuster (NEW = Claim Examiner role) on a fresh appointment (not
   * re-evaluation), pre-fill the per-injury claim examiner row with the
   * logged-in user's name + email so they don't re-type their own info.
   * The HTML pairs this with `[readonly]` on those fields when the booker
   * holds the role and is not IT Admin.
   */
  private applyClaimExaminerRolePrefill(): void {
    if (!this.isClaimExaminerRole || this.isItAdmin) return;
    const user = this.currentUser;
    if (!user) return;
    const fullName = [user.name, user.surname].filter(Boolean).join(' ').trim();
    this.injuryEditing.claimExaminer.name =
      fullName || user.userName || this.injuryEditing.claimExaminer.name;
    this.injuryEditing.claimExaminer.email = user.email || this.injuryEditing.claimExaminer.email;
    this.injuryEditing.claimExaminer.isActive = true;
  }

  openEditInjuryModal(index: number): void {
    const existing = this.injuryDrafts[index];
    if (!existing) return;
    // Deep clone so cancel discards in-modal edits.
    this.injuryEditing = JSON.parse(JSON.stringify(existing));
    this.injuryEditingIndex = index;
    this.loadInjuryLookups();
    this.isInjuryModalOpen = true;
  }

  closeInjuryModal(): void {
    this.isInjuryModalOpen = false;
    this.injuryEditingIndex = -1;
    this.injuryEditing = this.makeEmptyInjuryDraft();
    this.injuryModalError = null;
  }

  saveInjuryModal(): void {
    const missing: string[] = [];
    if (!this.injuryEditing.dateOfInjury) missing.push('Date of Injury');
    if (!this.injuryEditing.claimNumber) missing.push('Claim Number');
    if (!this.injuryEditing.bodyPartsSummary) missing.push('Body Parts');
    if (missing.length > 0) {
      this.injuryModalError =
        'Please fill the required field' +
        (missing.length > 1 ? 's' : '') +
        ': ' +
        missing.join(', ') +
        '.';
      return;
    }
    this.injuryModalError = null;
    if (this.injuryEditingIndex >= 0) {
      this.injuryDrafts[this.injuryEditingIndex] = this.injuryEditing;
    } else {
      this.injuryDrafts.push(this.injuryEditing);
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

  private async persistInjuryDraftsIfProvided(appointmentId?: string): Promise<void> {
    if (!appointmentId || this.injuryDrafts.length === 0) {
      return;
    }
    for (const draft of this.injuryDrafts) {
      const created = await firstValueFrom(
        this.restService.request<any, { id: string }>(
          {
            method: 'POST',
            url: '/api/app/appointment-injury-details',
            body: {
              appointmentId,
              dateOfInjury: draft.dateOfInjury,
              toDateOfInjury: draft.toDateOfInjury,
              claimNumber: draft.claimNumber,
              isCumulativeInjury: draft.isCumulativeInjury,
              wcabAdj: draft.wcabAdj,
              bodyPartsSummary: draft.bodyPartsSummary,
              wcabOfficeId: draft.wcabOfficeId,
            },
          },
          { apiName: 'Default' },
        ),
      );
      const injuryId = created?.id;
      if (!injuryId) continue;

      // Insurance: only persist if booker enabled the section.
      if (draft.primaryInsurance.isActive) {
        await firstValueFrom(
          this.restService.request<any, any>(
            {
              method: 'POST',
              url: '/api/app/appointment-primary-insurances',
              body: {
                appointmentInjuryDetailId: injuryId,
                isActive: true,
                name: draft.primaryInsurance.name,
                insuranceNumber: draft.primaryInsurance.insuranceNumber,
                attention: draft.primaryInsurance.attention,
                phoneNumber: draft.primaryInsurance.phoneNumber,
                faxNumber: draft.primaryInsurance.faxNumber,
                street: draft.primaryInsurance.street,
                city: draft.primaryInsurance.city,
                zip: draft.primaryInsurance.zip,
                stateId: draft.primaryInsurance.stateId,
              },
            },
            { apiName: 'Default' },
          ),
        );
      }

      // Claim Examiner: only persist if booker enabled the section.
      if (draft.claimExaminer.isActive) {
        await firstValueFrom(
          this.restService.request<any, any>(
            {
              method: 'POST',
              url: '/api/app/appointment-claim-examiners',
              body: {
                appointmentInjuryDetailId: injuryId,
                isActive: true,
                name: draft.claimExaminer.name,
                claimExaminerNumber: draft.claimExaminer.claimExaminerNumber,
                email: draft.claimExaminer.email,
                phoneNumber: draft.claimExaminer.phoneNumber,
                fax: draft.claimExaminer.fax,
                street: draft.claimExaminer.street,
                city: draft.claimExaminer.city,
                zip: draft.claimExaminer.zip,
                stateId: draft.claimExaminer.stateId,
              },
            },
            { apiName: 'Default' },
          ),
        );
      }
    }
  }
}
