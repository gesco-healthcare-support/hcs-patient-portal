import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  ConfigStateService,
  ListService,
  LocalizationPipe,
  PagedResultDto,
  RestService,
} from '@abp/ng.core';
import {
  Confirmation,
  ConfirmationService,
  DateAdapter,
  TimeAdapter,
  ToasterService,
} from '@abp/ng.theme.shared';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  finalize,
  map,
  switchMap,
} from 'rxjs/operators';
import { firstValueFrom, Observable, of } from 'rxjs';
import { TopHeaderNavbarComponent } from '../shared/components/top-header-navbar/top-header-navbar.component';
import { applyAttorneySectionValidators } from './shared/attorney-section-validators';
import { buildRevalPrefill } from './shared/reval-prefill.mapper';
import {
  AddressValidationProvider,
  AddressInput,
  StandardizedAddress,
} from '../shared/address/address-validation.provider';
import { AddressFieldMap } from '../shared/address/address-autocomplete.component';
import { resolveStateId, StateLookupOption } from '../shared/address/state-resolver';
import {
  ConfirmAddressDialogComponent,
  AddressChoice,
  AddressDiffItem,
} from '../shared/address/confirm-address-dialog.component';
import { NgbDateAdapter, NgbDateStruct, NgbTimeAdapter } from '@ng-bootstrap/ng-bootstrap';
import type { AppointmentCreateDto, AppointmentDto } from '../proxy/appointments/models';
import type { AppointmentClaimExaminerDto } from '../proxy/appointment-claim-examiners/models';
import type { AppointmentPrimaryInsuranceDto } from '../proxy/appointment-primary-insurances/models';
import { AppointmentService } from '../proxy/appointments/appointment.service';
import { AppointmentApprovalService } from '../proxy/appointments/appointment-approval.service';
import { AppointmentStatusType } from '../proxy/enums/appointment-status-type.enum';
import type {
  PatientDto,
  PatientUpdateDto,
  PatientWithNavigationPropertiesDto,
} from '../proxy/patients/models';
import type { LookupDto, LookupRequestDto } from '../proxy/shared/models';
import { AppointmentViewService } from './appointment/services/appointment.service';
import { DoctorAvailabilityService } from '../proxy/doctor-availabilities/doctor-availability.service';
import type { DoctorAvailabilityDto } from '../proxy/doctor-availabilities/models';
import { CustomFieldsService } from '../proxy/custom-fields-controllers/custom-fields.service';
import type { CustomFieldDto, CustomFieldValueInputDto } from '../proxy/custom-fields/models';
import { CustomFieldType } from '../proxy/enums/custom-field-type.enum';
// #121 (2026-05-13) -- 7 section components extracted from the
// monolithic booker template (T1-T7). The parent retains the 55-field
// reactive `form` FormGroup, every cascade subscription, every
// lookup/HTTP roundtrip, and every role-based visibility gate; each
// child binds [formGroup]="form" and renders template-only. Types
// surfaced here (AppointmentAuthorizedUserDraft, ExternalAuthorizedUserOption,
// AppointmentInjuryDraft, ClaimExaminerPrefill) live in the section
// files because they describe section-owned data; the parent imports
// them so submit-time + draft-list reads keep type-checking.
import { AppointmentAddCustomFieldsComponent } from './sections/appointment-add-custom-fields.component';
import {
  AppointmentAddAuthorizedUsersComponent,
  type AppointmentAuthorizedUserDraft,
  type ExternalAuthorizedUserOption,
} from './sections/appointment-add-authorized-users.component';
import { AppointmentAddEmployerDetailsComponent } from './sections/appointment-add-employer-details.component';
import {
  AppointmentAddClaimInformationComponent,
  type AppointmentInjuryDraft,
} from './sections/appointment-add-claim-information.component';
import { AppointmentAddAttorneySectionComponent } from './sections/appointment-add-attorney-section.component';
import { AppointmentAddClaimPartiesSectionComponent } from './sections/appointment-add-claim-parties-section.component';
import { AppointmentAddPatientDemographicsComponent } from './sections/appointment-add-patient-demographics.component';
import { AppointmentAddScheduleComponent } from './sections/appointment-add-schedule.component';
import {
  AppointmentAddDocumentsComponent,
  OTHER_DOCUMENT_TYPE_VALUE,
  type StagedDocumentUpload,
} from './sections/appointment-add-documents.component';
import { validateDocumentFile } from '../appointment-documents/document-upload.validation';
import { isStrikeListGateBlocked } from '../appointment-documents/strike-list-gate';

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

@Component({
  selector: 'app-appointment-add',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    LocalizationPipe,
    TopHeaderNavbarComponent,
    NgxValidateCoreModule,
    AppointmentAddCustomFieldsComponent,
    AppointmentAddAuthorizedUsersComponent,
    AppointmentAddEmployerDetailsComponent,
    AppointmentAddClaimInformationComponent,
    AppointmentAddAttorneySectionComponent,
    AppointmentAddClaimPartiesSectionComponent,
    AppointmentAddPatientDemographicsComponent,
    AppointmentAddScheduleComponent,
    AppointmentAddDocumentsComponent,
    ConfirmAddressDialogComponent,
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
  // G-01-07: proxy service for the reval/re-request endpoints
  // (getByConfirmationNumber + createReval + reSubmit).
  private readonly appointmentProxyService = inject(AppointmentService);
  private readonly appointmentApprovalService = inject(AppointmentApprovalService);
  // B1 (2026-05-05): per-AppointmentType custom-field catalog fetcher.
  private readonly customFieldsService = inject(CustomFieldsService);
  // Slot rework plan 5 (2026-05-15) -- the booking picker now reads from
  // GetDoctorAvailabilityLookupAsync, which already filters full +
  // reserved/booked slots server-side. Binary availability per locked
  // decision 2026-05-27: clients never see remaining/capacity numbers.
  private readonly doctorAvailabilityService = inject(DoctorAvailabilityService);
  // Picker refetch + booking error feedback (plan 5). Three new booking
  // codes (BookingSlotFull / BookingSlotClosed / BookingSlotTypeMismatch)
  // from plan 2 surface inline via this toaster; matching codes also
  // trigger a refetch of the lookup so the dropdown reflects current
  // server state before the next attempt.
  private readonly toaster = inject(ToasterService);
  // 2026-05-28 -- self-represented confirmation modal on AA toggle-off.
  private readonly confirmationService = inject(ConfirmationService);
  private readonly addressProvider = inject(AddressValidationProvider);

  // F2 (2026-05-29): pre-submit address standardization. When the provider
  // returns a USPS-standardized form that differs from what was typed, this
  // holds the diff items for the inline confirm dialog; `addressDialogResolve`
  // bridges the user's choice back to the awaiting submit flow. Covers the four
  // main-form address groups; the per-injury insurance + claim-examiner
  // addresses are standardized at autocomplete-pick time (see plan T3 note).
  addressDialogItems: AddressDiffItem[] | null = null;
  private addressDialogResolve?: (choices: Record<string, AddressChoice>) => void;
  private readonly addressGroupsForStandardization: {
    key: string;
    label: string;
    fields: AddressFieldMap;
    isEnabled: () => boolean;
  }[] = [
    {
      key: 'patient',
      label: 'Patient address',
      fields: {
        street: 'street',
        suite: 'address',
        city: 'city',
        state: 'stateId',
        zip: 'zipCode',
      },
      isEnabled: () => true,
    },
    {
      key: 'employer',
      label: 'Employer address',
      fields: {
        street: 'employerStreet',
        city: 'employerCity',
        state: 'employerStateId',
        zip: 'employerZipCode',
      },
      isEnabled: () => true,
    },
    {
      key: 'applicantAttorney',
      label: 'Applicant attorney address',
      fields: {
        street: 'applicantAttorneyStreet',
        city: 'applicantAttorneyCity',
        state: 'applicantAttorneyStateId',
        zip: 'applicantAttorneyZipCode',
      },
      isEnabled: () => !!this.form.get('applicantAttorneyEnabled')?.value,
    },
    {
      key: 'defenseAttorney',
      label: 'Defense attorney address',
      fields: {
        street: 'defenseAttorneyStreet',
        city: 'defenseAttorneyCity',
        state: 'defenseAttorneyStateId',
        zip: 'defenseAttorneyZipCode',
      },
      isEnabled: () => !!this.form.get('defenseAttorneyEnabled')?.value,
    },
  ];

  // B8 (2026-05-06): NgbDatepicker defaults to a +/-10-year navigation
  // window. For DOB we want the full century. Setting [minDate]/[maxDate]
  // and `navigation="select"` switches the header to month + year selects
  // that span the full configured range. Passed into the demographics
  // section as Inputs since the datepicker template lives there.
  readonly dobMinDate: NgbDateStruct = { year: 1920, month: 1, day: 1 };
  readonly dobMaxDate: NgbDateStruct = (() => {
    const today = new Date();
    return { year: today.getFullYear(), month: today.getMonth() + 1, day: today.getDate() };
  })();

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
  // 2026-06-11: role-based booking horizon. External users may book at most 60
  // days out online; internal staff up to 90 (the absolute ceiling -- the
  // state does not allow scheduling further out). Hardcoded client-side,
  // mirroring minimumBookingDays; the server-side BookingPolicyValidator
  // (role-aware AppointmentMaxTimeInternal) stays authoritative.
  private readonly externalMaxBookingDays = 60;
  private readonly internalMaxBookingDays = 90;

  /** Per-role online-booking horizon: 90 days for internal staff, 60 for external users. */
  get maxBookingDays(): number {
    return this.isInternalBooker ? this.internalMaxBookingDays : this.externalMaxBookingDays;
  }
  appointmentTimeOptions: Array<{ value: string; label: string; doctorAvailabilityId: string }> =
    [];
  private currentPatientProfile?: PatientWithNavigationPropertiesDto;
  // 2026-06-11: drives the patient Email label asterisk in the demographics
  // section. Recomputed by applyConditionalPatientEmailValidator() alongside the
  // control validator so the "*" reflects the actual (conditional) requirement.
  patientEmailRequired = true;
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
  // G-01-07 (2026-06-02): the booking form serves three modes, decided from
  // the route query params in the constructor:
  //   'new'       -- `?type=1` or no type. Plain create.
  //   'reval'     -- `?type=2` (Re-evaluation, OLD IsRevolutionForm). The
  //                  booker enters a prior APPROVED confirmation number, loads
  //                  it, and the form prefills from that source. Submits via
  //                  createReval (server generates a fresh confirmation #).
  //   'reRequest' -- `?mode=rerequest&source=<conf#>` (OLD IsReRequestForm),
  //                  launched from a REJECTED appointment's view page. Auto-
  //                  loads + prefills from the source. Submits via reSubmit
  //                  (server reuses the source confirmation #).
  bookingMode: 'new' | 'reval' | 'reRequest' = 'new';
  // The source confirmation number once a prior appointment has been loaded
  // for prefill. Null until loaded; gates the reval/re-request submit path.
  sourceConfirmationNumber: string | null = null;
  isLoadingSource = false;
  sourceLoadMessage = '';

  /** Reval mode (`?type=2`). Keeps the existing lookup-filter + heading wiring. */
  get isReevaluation(): boolean {
    return this.bookingMode === 'reval';
  }

  /** Re-request mode (launched from a rejected appointment's view page). */
  get isReRequest(): boolean {
    return this.bookingMode === 'reRequest';
  }

  /** Localization key for the page heading, by mode. */
  get headingKey(): string {
    if (this.bookingMode === 'reval') return '::ReEvaluationAppointment';
    if (this.bookingMode === 'reRequest') return '::ReRequestAppointment';
    return '::NewAppointment';
  }

  /**
   * EvaluationType filter for the appointment-type dropdown (server enum:
   * 0 = Normal, 1 = Re). Initial booking shows Normal+Both; reval shows Re+Both;
   * re-request resubmits the SAME appointment, so its source type (any
   * classification, prefilled) must stay selectable -> no filter (all types).
   */
  get appointmentTypeEvaluationContext(): 0 | 1 | undefined {
    if (this.bookingMode === 'reval') return 1;
    if (this.bookingMode === 'reRequest') return undefined;
    return 0;
  }

  // #121 phase T4 (2026-05-13) -- injury list stays at parent because
  // submit consumes it (persistInjuryDraftsIfProvided + Bug C email
  // fan-out resolver). Passed into the section by reference; the
  // section mutates via push / splice. Modal state, the per-injury
  // FormGroup, lookup arrays, and the cumulative-/Insurance-/CE-toggle
  // wiring all moved to AppointmentAddClaimInformationComponent.
  injuryDrafts: AppointmentInjuryDraft[] = [];

  // BUG-043: set true when submit is attempted with no Claim Information,
  // to drive the inline message on the Claim Information card.
  claimInformationMissing = false;

  /**
   * CI1/CI2 (2026-06-05): when a Claim Examiner books, pre-fill the
   * appointment-level Claim Examiner section with their own name + email.
   * OLD parity (appointment-add.component.ts:145-149) prefilled the per-injury
   * modal; CI1 moved CE to the appointment level, so the prefill moves with it.
   * Called once from the constructor.
   */
  private prefillAppointmentClaimExaminerForRole(): void {
    if (!this.isClaimExaminerRole || this.isItAdmin) return;
    const user = this.currentUser;
    if (!user) return;
    const fullName = [user.name, user.surname].filter(Boolean).join(' ').trim();
    this.form.patchValue({
      appointmentClaimExaminerName: fullName || user.userName || null,
      appointmentClaimExaminerEmail: user.email ?? null,
    });
  }
  // #121 phase T2 (2026-05-13) -- the AppointmentAuthorizedUserDraft[]
  // array stays at parent because it carries the data the submit flow
  // consumes; passed into the section by reference so the modal can
  // push / splice in place.
  appointmentAuthorizedUsers: AppointmentAuthorizedUserDraft[] = [];

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
          evaluationContext: this.appointmentTypeEvaluationContext,
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

  // 2026-06-11 (PII): NgbTypeahead search fn for the "find existing patient"
  // email search box. Debounced server-side email lookup; the server returns
  // nothing until 2+ characters and scopes results to patients the booker has
  // already worked with (AppointmentsAppService.GetPatientLookupAsync), so no
  // default list of every patient's email is ever exposed. Short terms
  // short-circuit to no request. Errors collapse to an empty result list so a
  // transient lookup failure never breaks the search box.
  // (ng-bootstrap typeahead docs: https://ng-bootstrap.github.io/#/components/typeahead/api)
  readonly searchPatientByEmail = (text$: Observable<string>): Observable<LookupDto<string>[]> =>
    text$.pipe(
      debounceTime(250),
      distinctUntilChanged(),
      switchMap((term) => {
        const trimmed = (term ?? '').trim();
        if (trimmed.length < 2) {
          return of<LookupDto<string>[]>([]);
        }
        return this.getPatientLookup({ filter: trimmed, skipCount: 0, maxResultCount: 20 }).pipe(
          map((res) => res.items ?? []),
          catchError(() => of<LookupDto<string>[]>([])),
        );
      }),
    );

  // AF3 + AF4 (2026-06-04): mirrors CaseEvaluationSeedIds.AppointmentTypes.PanelQme.
  // The appointmentTypeId valueChanges arg IS the type GUID string; there is no
  // generated proxy enum for seed-data GUIDs, so the canonical PQME id is mirrored
  // here as a local constant (kept in sync with the C# seed id).
  private readonly PQME_TYPE_ID = 'a0a00002-0000-4000-9000-000000000002';
  // Drives the Panel Number required-star affordance in the schedule section.
  isPqmeType = false;

  // AF7 (2026-06-05): documents staged in the booking form, uploaded after the
  // appointment is created (two-phase). Mutated in place; the documents section
  // uses default change detection so it renders these updates.
  stagedDocuments: StagedDocumentUpload[] = [];
  // I15 (2026-06-08): document-category labels for the booking-form picker,
  // loaded per appointment type. panelStrikeListTypeId is the option id whose
  // name is "Panel Strike List" (drives the strike-list flag + checkbox auto-tick).
  documentTypeOptions: { id: string; displayName: string }[] = [];
  panelStrikeListTypeId: string | null = null;
  // Set to the created appointment id once it exists, so an upload retry
  // re-POSTs to the SAME appointment instead of creating a duplicate.
  private createdAppointmentIdForRetry?: string;

  // AF6 (2026-06-05): PQME-only opt-in. When checked, the booker must mark one
  // staged document as the panel strike list before submit; unchecked, a PQME
  // submits with no strike-list requirement.
  hasPanelStrikeList = false;
  panelStrikeListMissing = false;
  // 2026-06-09: set when submit is blocked because a doc labeled "Other" has no
  // free-text name yet (a blank custom label is not a usable category).
  otherLabelMissing = false;

  readonly form = this.fb.group({
    panelNumber: [null as string | null, [Validators.maxLength(50)]],
    appointmentDate: [null as string | null, [Validators.required]],
    requestConfirmationNumber: ['A' as string | null, [Validators.maxLength(50)]],
    dueDate: [null as string | null],
    patientId: [null as string | null, [Validators.required]],
    // IP6 (2026-06-05): optional -- a record-only patient has no login, so the
    // appointment is booked with a null identity (claimed later on self-register).
    identityUserId: [null as string | null],
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
    // OLD parity (Wave 2 #9, 2026-05-07): patient.ts in OLD does NOT carry
    // @required() on AppointmentLanguageId, and customValdiationFor* never
    // applies a required validator to it. The earlier audit's "Language is
    // required" reading was a NEW deviation; dropping to match OLD verbatim.
    appointmentLanguageId: [null as string | null],
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
    // CI1 (2026-06-05): appointment-level Primary Insurance (optional) + Claim
    // Examiner (REQUIRED -- Name + Email) -- one each per appointment, replacing
    // the per-injury insurance/CE captured in the claim-information modal. CI2
    // removes the now-orphaned per-injury controls. Posted once after create.
    // I12 (2026-06-08): Insurance Name required client-side per
    // docs/appointment-required-fields.md (OLD-parity required-fields spec).
    appointmentInsuranceName: [
      null as string | null,
      [Validators.required, Validators.maxLength(50)],
    ],
    appointmentInsuranceSuite: [null as string | null, [Validators.maxLength(255)]],
    appointmentInsurancePhoneNumber: [null as string | null, [Validators.maxLength(12)]],
    appointmentInsuranceFaxNumber: [null as string | null, [Validators.maxLength(20)]],
    appointmentInsuranceStreet: [null as string | null, [Validators.maxLength(255)]],
    appointmentInsuranceCity: [null as string | null, [Validators.maxLength(50)]],
    appointmentInsuranceStateId: [null as string | null],
    appointmentInsuranceZip: [null as string | null, [Validators.maxLength(10)]],
    appointmentClaimExaminerName: [
      null as string | null,
      [Validators.required, Validators.maxLength(50)],
    ],
    appointmentClaimExaminerEmail: [
      null as string | null,
      [Validators.required, Validators.maxLength(50), Validators.email],
    ],
    appointmentClaimExaminerSuite: [null as string | null, [Validators.maxLength(255)]],
    // I12 (2026-06-08): Phone / Street / City / State / Zip required client-side
    // per docs/appointment-required-fields.md (OLD-parity required-fields spec).
    appointmentClaimExaminerPhoneNumber: [
      null as string | null,
      [Validators.required, Validators.maxLength(12)],
    ],
    appointmentClaimExaminerFax: [null as string | null, [Validators.maxLength(20)]],
    appointmentClaimExaminerStreet: [
      null as string | null,
      [Validators.required, Validators.maxLength(255)],
    ],
    appointmentClaimExaminerCity: [
      null as string | null,
      [Validators.required, Validators.maxLength(50)],
    ],
    appointmentClaimExaminerStateId: [null as string | null, [Validators.required]],
    appointmentClaimExaminerZip: [
      null as string | null,
      [Validators.required, Validators.maxLength(10)],
    ],
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

  // Wave 4 / #15 (2026-05-07, NEW-only enhancement -- no OLD parity):
  // when patient language = English, interpreter is irrelevant so the
  // radio is forced to No and locked. Cached on first lookup so the
  // valueChanges subscriber runs as a pure compare.
  private englishLanguageId: string | null = null;
  private englishLanguageLookupComplete = false;

  get customFieldsArray(): FormArray<FormGroup> {
    return this.form.get('customFieldsValues') as FormArray<FormGroup>;
  }

  constructor() {
    // G-01-07: resolve the booking mode from the route. `?type=2` is reval
    // (the booker enters a prior confirmation number + loads); `?mode=rerequest
    // &source=<conf#>` is re-request (launched from a rejected appointment and
    // auto-loaded). queryParamMap.subscribe so deep-links work.
    this.route.queryParamMap.subscribe((params) => {
      if (params.get('mode') === 'rerequest') {
        this.bookingMode = 'reRequest';
        // Re-request prefills from the source appointment, not the booker's own
        // profile, so the self-profile load is skipped (see the guard below).
        // Clear its loading flag here; loadSourceForPrefill owns the load state.
        this.isProfileLoading = false;
        const source = (params.get('source') ?? '').trim();
        if (source && this.sourceConfirmationNumber !== source) {
          void this.loadSourceForPrefill(source, 'reRequest');
        }
      } else {
        this.bookingMode = params.get('type') === '2' ? 'reval' : 'new';
      }
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
      // AF3 + AF4: toggle Panel Number enabled/required (PQME) vs cleared/
      // disabled (AME/IME) on every type change. Synchronous (no HTTP), so it
      // needs no request-version counter; it stays inside this subscriber to
      // remain ordered with the other per-type updates.
      this.applyPanelNumberStateForType(appointmentTypeId);
      // I15/I16 (2026-06-08): document-category labels are scoped to the
      // appointment type, so a type change invalidates any label / strike-list
      // designation on staged documents -- clear them so the booker re-labels for
      // the new type.
      this.stagedDocuments.forEach((doc) => {
        doc.documentTypeId = null;
        doc.isStrikeList = false;
        doc.isOtherType = false;
        doc.otherDocumentTypeName = null;
      });
      // The panel-strike-list opt-in is PQME-only; leaving PQME also clears it.
      if (!this.isPqmeType) {
        this.hasPanelStrikeList = false;
        this.panelStrikeListMissing = false;
      }
    });
    this.form
      .get('appointmentDate')
      ?.valueChanges.subscribe((value) => this.onAppointmentDateChanged(value));
    this.form
      .get('appointmentTime')
      ?.valueChanges.subscribe((value) => this.onAppointmentTimeChanged(value));
    this.updateLocationSelection(this.form.get('locationId')?.value ?? null);
    // In re-request mode the form prefills from the SOURCE appointment, so
    // loading the booker's own profile here would race the prefill and null
    // out the patient + employer controls. Skip it -- the prefill sets the
    // (reused) source patient instead. Reval is button-triggered after
    // construction, so its profile load has already settled.
    if (this.bookingMode !== 'reRequest') {
      this.loadCurrentPatientProfile();
    }
    this.prefillAppointmentClaimExaminerForRole();
    this.loadExternalAuthorizedUsers();

    // Wave 4 / #15 (2026-05-07): cache the English language GUID and
    // wire a valueChanges subscriber on appointmentLanguageId. When the
    // patient language is English, interpreter is irrelevant -- force
    // needsInterpreter to No and disable the radio. NEW-only enhancement
    // with no OLD parity (OLD html:199 has no English coupling); see
    // PARITY-FLAG-NEW-004 in docs/parity/_parity-flags.md.
    this.loadEnglishLanguageId();
    this.form
      .get('appointmentLanguageId')
      ?.valueChanges.subscribe((value) => this.applyEnglishInterpreterLock(value));
    // #121 phase T2 (2026-05-13) -- authorized-user identityUserId
    // change handler moved to the section component along with the
    // modal FormGroup it watches.

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
    // 2026-05-28 -- AA toggle-off requires explicit confirmation that the
    // applicant is self-represented. OLD did not have this gate; NEW adds
    // it so a booker (patient / intake staff / IT admin) cannot silently
    // omit the applicant attorney section without acknowledging it. When
    // the user clicks "No" on the modal we revert the toggle back to ON
    // via setValue(true, { emitEvent: false }) so this subscriber does
    // not re-fire. Toggling ON has no modal -- the section just opens.
    this.form.get('applicantAttorneyEnabled')?.valueChanges.subscribe((enabled) => {
      if (!enabled) {
        this.confirmAaToggleOff();
        return;
      }
      this.applyConditionalEmailValidator('applicantAttorneyEmail', true);
      applyAttorneySectionValidators(this.form, 'applicantAttorney', true);
      // AA present -> patient email becomes optional for on-behalf bookers.
      this.applyConditionalPatientEmailValidator();
    });
    // F4 (2026-05-29): DA toggle-off mirrors AA -- pop the confirmation modal
    // instead of silently dropping the section. Toggling ON has no modal.
    this.form.get('defenseAttorneyEnabled')?.valueChanges.subscribe((enabled) => {
      if (!enabled) {
        this.confirmDaToggleOff();
        return;
      }
      this.applyConditionalEmailValidator('defenseAttorneyEmail', true);
      applyAttorneySectionValidators(this.form, 'defenseAttorney', true);
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
    applyAttorneySectionValidators(this.form, 'applicantAttorney', aaInitialEnabled);
    this.applyConditionalEmailValidator('defenseAttorneyEmail', daInitialEnabled);
    applyAttorneySectionValidators(this.form, 'defenseAttorney', daInitialEnabled);
    this.applyConditionalEmailValidator('claimExaminerEmail', ceInitialEnabled);
    // 2026-06-11: set the patient-email required state for the initial booker
    // role + AA-enabled combination (recomputed on every AA toggle above).
    this.applyConditionalPatientEmailValidator();
    // AF3 + AF4: apply the Panel Number state once for the initial type. The
    // type starts null (no selection), so Panel Number begins cleared + disabled
    // until a PQME type is chosen.
    this.applyPanelNumberStateForType(this.form.get('appointmentTypeId')?.value ?? null);

    // B11-followup (2026-05-07): the earlier "hide AA/DA for CE" auto-
    // flip-off is no longer needed -- shouldShowApplicantAttorneySection
    // / shouldShowDefenseAttorneySection now always return true to match
    // OLD's behavior (see the comment on those methods).
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
   * 2026-06-11 -- patient email is mandatory ONLY when the patient is the one
   * requesting (the booker IS the patient) OR the applicant is self-represented
   * (no applicant attorney). For an on-behalf booker (AA / DA / CE / staff) with
   * an applicant attorney present, patient email is optional: if left blank, the
   * server routes patient-targeted mail to the applicant attorney
   * (PatientPacketEmailHandler AA fallback; the other handlers already CC the
   * AA as a party). Re-evaluated at construction and whenever the AA toggle
   * flips. Mirrors applyConditionalEmailValidator's emitEvent: false discipline
   * so it never re-enters a valueChanges subscriber.
   */
  private applyConditionalPatientEmailValidator(): void {
    const control = this.form.get('email');
    if (!control) return;
    const required =
      !this.isExternalUserNonPatient || !this.form.get('applicantAttorneyEnabled')?.value;
    // Drives the label asterisk in the demographics section (passed down as an
    // Input) so the "*" mirrors the actual requirement instead of always showing.
    this.patientEmailRequired = required;
    const validators = required
      ? [Validators.required, Validators.maxLength(50), Validators.email]
      : [Validators.maxLength(50), Validators.email];
    control.setValidators(validators);
    control.updateValueAndValidity({ emitEvent: false });
  }

  /**
   * AF3 + AF4 (2026-06-04): Panel Number state machine keyed off the PQME type.
   * PQME -> the field is enabled + required (a PQME carries a state-issued panel
   * number). Any other type (AME / IME) -> the value is cleared, validators drop
   * to length-only, and the control is disabled, so a legitimate submission never
   * carries a panel number for a non-PQME type. AppointmentManager enforces the
   * same rule server-side as the authoritative guard; this is the primary UX.
   * Mirrors applyConditionalEmailValidator: setValidators + value reset +
   * updateValueAndValidity with { emitEvent: false } so it does not re-enter the
   * appointmentTypeId valueChanges subscriber that calls it.
   */
  private applyPanelNumberStateForType(typeId: string | null): void {
    const control = this.form.get('panelNumber');
    if (!control) return;
    this.isPqmeType = typeId === this.PQME_TYPE_ID;
    // I15: refresh the document-category labels for the newly-selected type.
    this.loadDocumentTypeOptions(typeId);
    if (this.isPqmeType) {
      control.enable({ emitEvent: false });
      control.setValidators([Validators.required, Validators.maxLength(50)]);
    } else {
      control.setValue(null, { emitEvent: false });
      control.setValidators([Validators.maxLength(50)]);
      control.disable({ emitEvent: false });
    }
    control.updateValueAndValidity({ emitEvent: false });
  }

  /**
   * 2026-05-28 -- AA toggle-off confirmation. Pops the ABP confirmation
   * modal asking whether the applicant is self-represented. On Yes the
   * toggle stays off and the section's required-validators + email value
   * are cleared. On No (or dismissal) the toggle reverts to ON with
   * { emitEvent: false } so the valueChanges subscriber that opened this
   * modal does not re-fire.
   */
  private confirmAaToggleOff(): void {
    const enabledControl = this.form.get('applicantAttorneyEnabled');
    if (!enabledControl) return;
    this.confirmationService
      .warn(
        '::Appointment:ApplicantAttorneySelfRepresentedMessage',
        '::Appointment:ApplicantAttorneySelfRepresentedTitle',
        { yesText: 'AbpUi::Yes', cancelText: 'AbpUi::No' },
      )
      .subscribe((status) => {
        if (status !== Confirmation.Status.confirm) {
          // Revert with emitEvent: true so valueChanges re-fires and the
          // section's OnPush markForCheck hook re-renders the body. The
          // outer valueChanges subscriber's "enabled=true" branch only
          // re-applies the (already-required) validators -- idempotent.
          enabledControl.setValue(true);
          return;
        }
        this.applyConditionalEmailValidator('applicantAttorneyEmail', false);
        applyAttorneySectionValidators(this.form, 'applicantAttorney', false);
        this.form.get('applicantAttorneyEmail')?.setValue(null, { emitEvent: false });
        // Self-represented (no AA) -> patient email becomes required.
        this.applyConditionalPatientEmailValidator();
      });
  }

  /**
   * F4 (2026-05-29) -- DA toggle-off confirmation. Mirrors confirmAaToggleOff
   * but with INVERTED Yes/No, because the question polarity is opposite. The
   * modal asks "Is a Defense Attorney assigned to this case?":
   *   - Yes = one IS assigned -> keep the section required (revert the toggle
   *     to ON with emitEvent so the OnPush section re-renders; the enabled=true
   *     branch re-applies the already-required validators, idempotent).
   *   - No / dismiss = none assigned -> confirm removal: clear the DA
   *     required-validators and the DA email value.
   */
  private confirmDaToggleOff(): void {
    const enabledControl = this.form.get('defenseAttorneyEnabled');
    if (!enabledControl) return;
    this.confirmationService
      .warn(
        '::Appointment:DefenseAttorneyAssignedMessage',
        '::Appointment:DefenseAttorneyAssignedTitle',
        { yesText: 'AbpUi::Yes', cancelText: 'AbpUi::No' },
      )
      .subscribe((status) => {
        if (status === Confirmation.Status.confirm) {
          enabledControl.setValue(true);
          return;
        }
        this.applyConditionalEmailValidator('defenseAttorneyEmail', false);
        applyAttorneySectionValidators(this.form, 'defenseAttorney', false);
        this.form.get('defenseAttorneyEmail')?.setValue(null, { emitEvent: false });
      });
  }

  // BUG-012 Sub-bug 2 (2026-05-22) -- the OLD-parity "Mandatory Fields"
  // section-validator helper moved to ./shared/attorney-section-validators.ts
  // so appointment-view.component.ts can share the implementation. See the
  // exported `applyAttorneySectionValidators` + `ATTORNEY_SECTION_SUFFIXES`
  // in that file. The call sites above retain their context-specific
  // behavior (email-validator toggle + email-clear on disable).

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
   * users (admin, Intake Staff, Staff Supervisor, Doctor) booking on
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

  /**
   * True when the booker is an internal staff user (anyone whose roles are
   * none of the four external party roles). F1/F2 fix (2026-06-07): internal
   * bookings now create as Pending and are auto-approved by the client once
   * the party/injury attach sequence has persisted, so the approval gates run
   * against the complete appointment and the attach calls no longer race the
   * approval side-effects. Mirrors the backend BookingFlowRoles external set.
   */
  private static readonly externalPartyRoles = [
    'patient',
    'applicant attorney',
    'defense attorney',
    'claim examiner',
  ];

  get isInternalBooker(): boolean {
    const roles = this.currentUser?.roles ?? [];
    if (roles.length === 0) {
      return false;
    }
    return !roles.some((r: string) =>
      AppointmentAddComponent.externalPartyRoles.includes(r?.toLowerCase()),
    );
  }

  // B11 reversed (2026-05-07): the earlier interpretation hid the
  // Applicant Attorney / Defense Attorney / Additional Authorized User
  // cards for the Claim Examiner (= OLD's Adjuster) booker. A live
  // walkthrough of the OLD app under `adjuster@local.test` showed that
  // OLD shows ALL three sections to the Adjuster; only the Insurance
  // fieldset is `[disabled]` and the Claim Examiner Name + Email fields
  // auto-fill from the booker identity and become readonly (OLD
  // appointment-add.component.html:378 + :461). The two attorney-section
  // methods below stay for future role-specific gating but currently always
  // return true for parity; shouldShowAuthorizedUserSection is gated as of
  // Workstream B (see its own doc).
  shouldShowApplicantAttorneySection(): boolean {
    return true;
  }

  shouldShowDefenseAttorneySection(): boolean {
    return true;
  }

  /**
   * B (2026-06-10): the booking-time "Additional Authorized User" section is
   * only offered to callers who may manage accessors -- internal staff or an
   * Applicant/Defense Attorney booker (the booker is the creator-to-be, so the
   * creator condition is implicit during booking). Cosmetic only; the server's
   * EnsureCanManageAccessorsAsync gate stays authoritative. Paralegal-ready: the
   * paralegal feature adds `|| this.isParalegal` here (additive).
   */
  shouldShowAuthorizedUserSection(): boolean {
    return this.isInternalBooker || this.isApplicantAttorney || this.isDefenseAttorney;
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

  /**
   * #121 phase T5 (2026-05-13) -- pre-bound reference passed to
   * <app-appointment-add-attorney-section> as Input. Angular's change
   * detection compares Input identities, so a freshly-bound arrow on
   * every CD pass would force unnecessary child re-evaluation. Caching
   * the bound reference keeps the section's OnPush change detection
   * stable.
   */
  readonly isFieldInvalidBound = (fieldName: string): boolean => this.isFieldInvalid(fieldName);

  // ===== G-01-07: reval / re-request source lookup + prefill =====

  /**
   * Reval entry (OLD getRevelAppointmentForm): the booker types a prior
   * APPROVED confirmation number and loads it. Bound to the "Load prior
   * appointment" button shown on the booking form in reval mode.
   */
  loadRevalSource(confirmationNumber: string): void {
    const conf = (confirmationNumber ?? '').trim();
    if (!conf) {
      this.sourceLoadMessage = 'Enter the prior appointment confirmation number.';
      return;
    }
    void this.loadSourceForPrefill(conf, 'reval');
  }

  /**
   * Look up a source appointment by confirmation number and prefill the form
   * from its full intake. Mirrors OLD bindFormGroup(Revel/ReRequest): copy
   * everything forward, child PKs reset (implicit -- the produced drafts carry
   * no ids). Slot/date/time are NOT prefilled -- the booker picks a fresh,
   * currently-available slot. The status gate is client-side defensive; the
   * server re-validates on submit via LoadReval/ResubmitSourceAsync.
   */
  private async loadSourceForPrefill(
    confirmationNumber: string,
    flow: 'reval' | 'reRequest',
  ): Promise<void> {
    if (this.isLoadingSource) return;
    this.isLoadingSource = true;
    this.sourceLoadMessage = '';
    try {
      const source = await firstValueFrom(
        this.appointmentProxyService.getByConfirmationNumber(confirmationNumber),
      );
      const appt = source?.appointment;
      if (!appt?.id) {
        this.sourceLoadMessage = 'No appointment was found for that confirmation number.';
        return;
      }
      const gate = this.checkSourceStatusForFlow(appt.appointmentStatus, flow);
      if (gate) {
        this.sourceLoadMessage = gate;
        return;
      }
      await this.applySourceToForm(
        appt,
        source.patient ?? null,
        source.claimExaminer ?? null,
        source.primaryInsurance ?? null,
      );
      this.sourceConfirmationNumber = confirmationNumber;
      this.sourceLoadMessage =
        'Prior appointment loaded. Review the details, choose a new date and time, then submit.';
    } catch {
      this.sourceLoadMessage =
        'Unable to load that appointment. Check the confirmation number and try again.';
    } finally {
      this.isLoadingSource = false;
    }
  }

  /** Defensive status gate: reval needs Approved, re-request needs Rejected. */
  private checkSourceStatusForFlow(
    status: AppointmentStatusType | undefined,
    flow: 'reval' | 'reRequest',
  ): string | null {
    if (flow === 'reval' && status !== AppointmentStatusType.Approved) {
      return 'You can re-evaluate only an approved appointment.';
    }
    if (flow === 'reRequest' && status !== AppointmentStatusType.Rejected) {
      return 'You can re-request only a rejected appointment.';
    }
    return null;
  }

  /** Fetch the source child intake (same endpoints the view page uses) and
   * apply it to the form + drafts. */
  private async applySourceToForm(
    appt: AppointmentDto,
    patient: PatientDto | null,
    claimExaminer: AppointmentClaimExaminerDto | null,
    primaryInsurance: AppointmentPrimaryInsuranceDto | null,
  ): Promise<void> {
    const id = appt.id!;
    const [employer, applicantAttorney, defenseAttorney, injuries, accessors] = await Promise.all([
      this.fetchSourceEmployer(id),
      this.fetchSourceAppointmentResource(`/api/app/appointments/${id}/applicant-attorney`),
      this.fetchSourceAppointmentResource(`/api/app/appointments/${id}/defense-attorney`),
      this.fetchSourceInjuries(id),
      this.fetchSourceAccessors(id),
    ]);

    this.applySourcePatient(patient);

    const prefill = buildRevalPrefill({
      appointment: appt,
      employer,
      applicantAttorney,
      defenseAttorney,
      injuries,
      accessors,
      authorizedUserOptions: this.externalAuthorizedUserOptions,
      claimExaminer,
      primaryInsurance,
    });
    // formPatch is a dynamic key/value bag (employer + attorney + panel/due);
    // cast to bypass the strongly-typed FormGroup value shape at this one site.
    this.form.patchValue(prefill.formPatch as any, { emitEvent: false });
    this.injuryDrafts = prefill.injuryDrafts;
    this.appointmentAuthorizedUsers = prefill.authorizedUsers;
    this.applicantAttorneyId = prefill.applicantAttorneyId;
    this.applicantAttorneyConcurrencyStamp = prefill.applicantAttorneyConcurrencyStamp;
    this.defenseAttorneyId = prefill.defenseAttorneyId;
    this.defenseAttorneyConcurrencyStamp = prefill.defenseAttorneyConcurrencyStamp;
    this.applyAttorneyEnabledFromSource(!!applicantAttorney, 'applicantAttorney');
    this.applyAttorneyEnabledFromSource(!!defenseAttorney, 'defenseAttorney');

    // Type + location LAST, WITH events, so the slot picker + field-config +
    // custom-field cascades run for the prefilled type/location. Date/time stay
    // null so the booker selects a fresh, currently-available slot. ORDER IS
    // LOAD-BEARING: patch locationId first (its valueChanges early-returns while
    // appointmentTypeId is still null), then appointmentTypeId (which runs the
    // slot fetch with both present). Two calls keep the ordering explicit.
    this.form.patchValue({ locationId: appt.locationId ?? null });
    this.form.patchValue({ appointmentTypeId: appt.appointmentTypeId ?? null });
  }

  private async fetchSourceEmployer(appointmentId: string): Promise<any> {
    try {
      const res = await firstValueFrom(
        this.restService.request<any, PagedResultDto<any>>(
          {
            method: 'GET',
            url: '/api/app/appointment-employer-details',
            params: { appointmentId, skipCount: 0, maxResultCount: 1 },
          },
          { apiName: 'Default' },
        ),
      );
      return res?.items?.[0]?.appointmentEmployerDetail ?? null;
    } catch {
      return null;
    }
  }

  /** GET a nullable per-appointment sub-resource, swallowing errors to null. */
  private async fetchSourceAppointmentResource(url: string): Promise<any> {
    try {
      return await firstValueFrom(
        this.restService.request<any, any>({ method: 'GET', url }, { apiName: 'Default' }),
      );
    } catch {
      return null;
    }
  }

  private async fetchSourceInjuries(appointmentId: string): Promise<any[]> {
    try {
      const items = await firstValueFrom(
        this.restService.request<any, any[]>(
          {
            method: 'GET',
            url: `/api/app/appointment-injury-details/by-appointment/${appointmentId}`,
          },
          { apiName: 'Default' },
        ),
      );
      return items ?? [];
    } catch {
      return [];
    }
  }

  private async fetchSourceAccessors(appointmentId: string): Promise<any[]> {
    try {
      const res = await firstValueFrom(
        this.restService.request<any, PagedResultDto<any>>(
          {
            method: 'GET',
            url: '/api/app/appointment-accessors',
            params: { appointmentId, skipCount: 0, maxResultCount: 100 },
          },
          { apiName: 'Default' },
        ),
      );
      return res?.items ?? [];
    } catch {
      return [];
    }
  }

  // ----- AF7 (2026-06-05): pre-submit document staging + post-create upload -----

  onDocumentsSelected(files: File[]): void {
    for (const file of files) {
      const error = validateDocumentFile(file);
      if (error) {
        this.toaster.error(`${file.name}: ${error}`);
        continue;
      }
      this.stagedDocuments.push({
        file,
        status: 'staged',
        isStrikeList: false,
        documentTypeId: null,
      });
    }
  }

  // ----- AF6 (2026-06-05): PQME panel-strike-list opt-in + designation -----

  onHasPanelStrikeListChange(checked: boolean): void {
    // I16 (2026-06-08): the checkbox is the "providing the strike list now"
    // toggle. The strike list itself is identified by the document labeled
    // "Panel Strike List" (see onDocumentTypeChange), not a radio designation.
    this.hasPanelStrikeList = checked;
    this.panelStrikeListMissing = false;
  }

  /**
   * I16 (2026-06-08): a staged document's type-label was chosen. The strike list
   * is whichever document is labeled "Panel Strike List"; choosing that label
   * also auto-ticks the "panel strike list" checkbox (D2).
   */
  onDocumentTypeChange(event: { index: number; typeId: string | null }): void {
    const doc = this.stagedDocuments[event.index];
    if (!doc) {
      return;
    }
    if (event.typeId === OTHER_DOCUMENT_TYPE_VALUE) {
      // "Other": no listed category -- the booker types a free-text label.
      // Clear the type id (mutually exclusive with otherDocumentTypeName on the
      // backend) and the strike-list flag (a custom label is never the
      // recognized "Panel Strike List").
      doc.isOtherType = true;
      doc.documentTypeId = null;
      doc.isStrikeList = false;
    } else {
      doc.isOtherType = false;
      doc.otherDocumentTypeName = null;
      doc.documentTypeId = event.typeId;
      doc.isStrikeList = !!event.typeId && event.typeId === this.panelStrikeListTypeId;
    }
    if (this.stagedDocuments.some((d) => d.isStrikeList)) {
      this.hasPanelStrikeList = true;
    }
    this.panelStrikeListMissing = false;
    this.otherLabelMissing = false;
  }

  /** I (2026-06-09): the free-text "Other" label was edited for a staged doc. */
  onOtherDocumentTypeNameChange(event: { index: number; value: string }): void {
    const doc = this.stagedDocuments[event.index];
    if (!doc) {
      return;
    }
    doc.otherDocumentTypeName = event.value;
    if (event.value.trim()) {
      this.otherLabelMissing = false;
    }
  }

  /**
   * I15 (2026-06-08): load the document-category labels for the chosen appointment
   * type so the booking-form picker can show them, and cache the "Panel Strike
   * List" option id for strike-list detection. Best-effort: a failure leaves the
   * picker empty rather than blocking booking.
   */
  private loadDocumentTypeOptions(appointmentTypeId: string | null): void {
    this.documentTypeOptions = [];
    this.panelStrikeListTypeId = null;
    if (!appointmentTypeId) {
      return;
    }
    this.restService
      .request<
        unknown,
        { id: string; displayName: string }[]
      >({ method: 'GET', url: `/api/app/appointment-documents/options-by-type/${appointmentTypeId}` }, { apiName: 'Default' })
      .subscribe({
        next: (options) => {
          this.documentTypeOptions = options ?? [];
          const strike = this.documentTypeOptions.find(
            (o) => (o.displayName ?? '').trim().toLowerCase() === 'panel strike list',
          );
          this.panelStrikeListTypeId = strike?.id ?? null;
        },
        error: () => {
          this.documentTypeOptions = [];
          this.panelStrikeListTypeId = null;
        },
      });
  }

  removeStagedDocument(index: number): void {
    const doc = this.stagedDocuments[index];
    // Do not yank a file mid-upload or after it has already uploaded.
    if (!doc || doc.status === 'uploading' || doc.status === 'uploaded') {
      return;
    }
    this.stagedDocuments.splice(index, 1);
  }

  /**
   * Re-POST any not-yet-uploaded staged documents to the already-created
   * appointment. Surfaced by the documents section's Retry button after a
   * partial failure; navigates home once every file is uploaded.
   */
  async retryStagedUploads(): Promise<void> {
    if (this.isSaving) {
      return;
    }
    this.isSaving = true;
    try {
      if (await this.uploadStagedDocuments(this.createdAppointmentIdForRetry)) {
        this.router.navigateByUrl('/');
      }
    } finally {
      this.isSaving = false;
    }
  }

  /**
   * Reuse the source patient (a standalone entity, not a child of the
   * appointment) -- set patientId + currentPatientProfile so submit updates
   * that patient and stamps isPatientAlreadyExist. SSN is never pre-filled.
   */
  private applySourcePatient(patient: PatientDto | null): void {
    if (!patient?.id) return;
    this.currentPatientProfile = {
      patient,
      isExisting: true,
    } as PatientWithNavigationPropertiesDto;
    this.patientLabel = [patient.firstName, patient.lastName].filter(Boolean).join(' ').trim();
    this.form.patchValue(
      {
        patientId: patient.id,
        identityUserId: patient.identityUserId ?? this.currentUser?.id ?? null,
        firstName: patient.firstName ?? null,
        lastName: patient.lastName ?? null,
        middleName: patient.middleName ?? null,
        email: patient.email ?? null,
        genderId: this.normalizePatientGender(patient.genderId),
        dateOfBirth: this.normalizePatientDateOfBirth(patient.dateOfBirth as string | null),
        cellPhoneNumber: patient.cellPhoneNumber ?? null,
        phoneNumber: patient.phoneNumber ?? null,
        phoneNumberTypeId: (patient.phoneNumberTypeId as number | undefined) ?? null,
        socialSecurityNumber: null, // F1 / Design B: SSN is never pre-filled
        street: patient.street ?? null,
        address: patient.address ?? null,
        city: patient.city ?? null,
        stateId: patient.stateId ?? null,
        zipCode: patient.zipCode ?? null,
        appointmentLanguageId: patient.appointmentLanguageId ?? null,
        interpreterVendorName: patient.interpreterVendorName ?? null,
        needsInterpreter: !!patient.interpreterVendorName,
        refferedBy: null, // 2026-06-09: not prefilled -- per-booking optional field
      },
      { emitEvent: false },
    );
  }

  /**
   * Set an attorney section's Enabled flag from the source (present -> on),
   * keeping it on when the booker's own role makes the section mandatory.
   * Re-applies the conditional validators so an ABSENT source attorney does
   * not leave a required-but-empty section that would block submit.
   */
  private applyAttorneyEnabledFromSource(
    present: boolean,
    prefix: 'applicantAttorney' | 'defenseAttorney',
  ): void {
    const control = this.form.get(`${prefix}Enabled`);
    if (!control) return;
    const mandatory =
      prefix === 'applicantAttorney'
        ? this.isApplicantAttorney && !this.isItAdmin
        : this.isDefenseAttorney && !this.isItAdmin;
    const enabled = present || mandatory;
    control.setValue(enabled, { emitEvent: false });
    this.applyConditionalEmailValidator(`${prefix}Email`, enabled);
    applyAttorneySectionValidators(this.form, prefix, enabled);
  }

  /**
   * Route the create call by mode: reval -> createReval (server generates a
   * fresh confirmation #); re-request -> reSubmit (server reuses the source
   * confirmation #); otherwise the plain create. The post-create child
   * cascade is identical for all three, so prefilled drafts persist as fresh
   * rows on the new appointment.
   */
  private createAppointmentForCurrentMode(payload: AppointmentCreateDto): Promise<any> {
    if (this.bookingMode === 'reval' && this.sourceConfirmationNumber) {
      return firstValueFrom(
        this.appointmentProxyService.createReval(this.sourceConfirmationNumber, payload),
      );
    }
    if (this.bookingMode === 'reRequest' && this.sourceConfirmationNumber) {
      return firstValueFrom(
        this.appointmentProxyService.reSubmit(this.sourceConfirmationNumber, payload),
      );
    }
    return firstValueFrom(
      this.restService.request<any, any>(
        { method: 'POST', url: '/api/app/appointments', body: payload },
        { apiName: 'Default' },
      ),
    );
  }

  /**
   * Upload every not-yet-uploaded staged document to the created appointment via
   * the existing ad-hoc endpoint (reuses its 10 MB size + magic-byte format
   * validation; the booker is authorized as appointment Creator -- zero backend
   * change). Returns true only when all staged files are uploaded. On a per-file
   * failure the file is marked 'failed' and kept for retry -- no rollback, which
   * matches the existing non-atomic child-POST behavior.
   */
  private async uploadStagedDocuments(appointmentId?: string): Promise<boolean> {
    if (!appointmentId || this.stagedDocuments.length === 0) {
      return true;
    }
    let allUploaded = true;
    for (const staged of this.stagedDocuments) {
      if (staged.status === 'uploaded') {
        continue;
      }
      staged.status = 'uploading';
      staged.error = undefined;
      try {
        const form = new FormData();
        form.append('file', staged.file, staged.file.name);
        form.append('documentName', staged.file.name);
        // AF6: tag the marked strike-list file so the server sets IsPanelStrikeList.
        form.append('isPanelStrikeList', String(staged.isStrikeList));
        // I15: send the chosen document-type label so it is stored, and (for the
        // "Panel Strike List" label) the server also sets IsPanelStrikeList.
        // "Other" sends the free-text name instead -- the two are mutually
        // exclusive on the backend (ResolveDocumentTypeSelectionAsync).
        if (staged.isOtherType) {
          const otherName = staged.otherDocumentTypeName?.trim();
          if (otherName) {
            form.append('otherDocumentTypeName', otherName);
          }
        } else if (staged.documentTypeId) {
          form.append('appointmentDocumentTypeId', staged.documentTypeId);
        }
        await firstValueFrom(
          this.restService.request<FormData, unknown>(
            {
              method: 'POST',
              url: `/api/app/appointments/${appointmentId}/documents`,
              body: form,
            },
            { apiName: 'Default' },
          ),
        );
        staged.status = 'uploaded';
      } catch (err: unknown) {
        staged.status = 'failed';
        const httpErr = err as { error?: { error?: { message?: string } } };
        staged.error = httpErr?.error?.error?.message ?? 'Upload failed.';
        allUploaded = false;
      }
    }
    return allUploaded;
  }

  async onSubmit(): Promise<void> {
    const raw = this.form.getRawValue();
    // G-01-07: reval + re-request must be anchored to a loaded source (the
    // server endpoints take the source confirmation # in the route). Block
    // submit until one is loaded so we never silently fall through to a plain
    // create for a re-eval/re-request.
    if (this.bookingMode !== 'new' && !this.sourceConfirmationNumber) {
      if (this.bookingMode === 'reval') {
        this.sourceLoadMessage =
          'Look up the prior approved appointment by confirmation number before submitting.';
      } else {
        this.sourceLoadMessage =
          'The prior appointment could not be loaded, so this re-request cannot be submitted.';
      }
      return;
    }
    if (this.isExternalUserNonPatient && !raw.patientId) {
      // 2026-06-11: within this branch the booker is always a non-patient, so
      // patient email is required here ONLY when self-represented (no AA). With
      // an AA present, email is optional -- patient mail falls back to the AA
      // server-side. Mirrors applyConditionalPatientEmailValidator.
      const emailRequiredForNew = !raw.applicantAttorneyEnabled;
      const requiredForNew =
        raw.firstName && raw.lastName && raw.dateOfBirth && (!emailRequiredForNew || raw.email);
      if (!requiredForNew) {
        this.patientLoadMessage = emailRequiredForNew
          ? 'To create a new patient, First Name, Last Name, Email and Date of Birth are required.'
          : 'To create a new patient, First Name, Last Name and Date of Birth are required.';
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

    // BUG-043: Claim Information is required for all appointment types
    // (OLD parity -- OLD blocked submit when no injury detail existed).
    // The per-claim modal validates each entry; this guards that at least
    // one claim was added before the appointment can be booked.
    if (this.injuryDrafts.length === 0) {
      this.claimInformationMissing = true;
      this.patientLoadMessage = 'Please add at least one Claim Information entry before saving.';
      return;
    }
    this.claimInformationMissing = false;

    // AF6: PQME panel-strike-list gate. When the booker opted in
    // (hasPanelStrikeList), block submit until one staged document is marked as
    // the strike list. Client-side only (locked decision); mirrors the BUG-043
    // flag/message/return shape above so an invalid PQME booking never persists.
    if (isStrikeListGateBlocked(this.isPqmeType, this.hasPanelStrikeList, this.stagedDocuments)) {
      this.panelStrikeListMissing = true;
      this.patientLoadMessage =
        'Please mark which uploaded document is the panel strike list before saving.';
      return;
    }
    this.panelStrikeListMissing = false;

    // 2026-06-09: a document labeled "Other" needs its free-text name. A blank
    // custom label is not a usable category, so block submit (mirrors the
    // flag/message/return shape of the gates above).
    if (this.stagedDocuments.some((d) => d.isOtherType && !d.otherDocumentTypeName?.trim())) {
      this.otherLabelMissing = true;
      this.patientLoadMessage = 'Enter a name for each document labeled "Other" before saving.';
      return;
    }
    this.otherLabelMissing = false;

    // F2 (2026-05-29): prompt for USPS-standardized addresses before booking.
    // Runs on the mock until the Smarty adapter ships; degrades to a no-op on
    // any provider error so submission is never blocked.
    await this.standardizeAddressesBeforeSubmit();

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
        // G-01-03 / OLD parity (AppointmentDomain.cs:203-218): persist the
        // returning-patient result the dedup already computed. The server reads
        // this flag verbatim; without it every booking records a "new" patient.
        isPatientAlreadyExist: this.currentPatientProfile?.isExisting ?? false,
        identityUserId: rawAfter.identityUserId ?? null,
        appointmentTypeId: rawAfter.appointmentTypeId ?? '',
        locationId: rawAfter.locationId ?? '',
        doctorAvailabilityId: rawAfter.doctorAvailabilityId ?? '',
        // 2026-06-09: per-appointment Referred By (optional; blank by default, never
        // prefilled from the patient or prior appointments).
        refferedBy: rawAfter.refferedBy?.trim() || undefined,
        // S-5.1: party emails captured at booking time so email fan-out (step 6.1)
        // and auto-link on registration (step 5.2) have the addresses immediately.
        patientEmail: rawAfter.email ?? undefined,
        applicantAttorneyEmail: rawAfter.applicantAttorneyEnabled
          ? (rawAfter.applicantAttorneyEmail ?? undefined)
          : undefined,
        defenseAttorneyEmail: rawAfter.defenseAttorneyEnabled
          ? (rawAfter.defenseAttorneyEmail ?? undefined)
          : undefined,
        // 2026-05-11 (Bug C fix): the top-level claimExaminerEmail field is
        // vestigial (see comment on form definition); the real CE email is
        // typed into the per-injury modal (`injuryDrafts[i].claimExaminer.email`).
        // The first injury's CE email is the canonical AppointmentRequested fan-out
        // address -- mirrors how the resolver's `Appointment.ClaimExaminerEmail`
        // column gets read for the CE-email-col walk. Without this sync, the column
        // saves NULL for non-CE bookers and the CE leg of the fan-out silently drops.
        // CI1 (2026-06-05): the canonical CE email is now the appointment-level
        // Claim Examiner section (required), not the per-injury modal. The
        // resolver reads Appointment.ClaimExaminerEmail for the CE-email-col walk.
        claimExaminerEmail: (rawAfter.appointmentClaimExaminerEmail ?? '').trim() || undefined,
        // B1 (2026-05-05): map the FormArray into CustomFieldValueInputDto[].
        // Empty / whitespace values are dropped to match OLD's "no answer"
        // semantics; the backend AppService also drops them defensively.
        customFieldValues: this.serializeCustomFieldValues(),
      };

      const createdAppointment = await this.createAppointmentForCurrentMode(payload);

      await this.createEmployerDetailsIfProvided(createdAppointment?.id);
      await this.upsertApplicantAttorneyForAppointmentIfProvided(createdAppointment?.id);
      await this.upsertDefenseAttorneyForAppointmentIfProvided(createdAppointment?.id);
      await this.createAppointmentPrimaryInsuranceIfProvided(createdAppointment?.id);
      await this.createAppointmentClaimExaminerIfProvided(createdAppointment?.id);
      await this.persistInjuryDraftsIfProvided(createdAppointment?.id);
      await this.createAppointmentAccessorsIfProvided(createdAppointment?.id);

      // AF7: upload staged documents to the now-created appointment. On a
      // partial failure keep the booker on the page (the appointment stays
      // Pending) so they can retry against the existing id -- no rollback,
      // matching the non-atomic child-POST behavior above.
      this.createdAppointmentIdForRetry = createdAppointment?.id;
      if (!(await this.uploadStagedDocuments(createdAppointment?.id))) {
        this.toaster.warn(
          'Appointment created, but some documents failed to upload. Retry from the Documents section.',
        );
        return;
      }

      // F1/F2 fix (2026-06-07): internal bookings are created Pending so the
      // party/injury attach calls above do not race the approval side-effects
      // (which previously 409'd the attaches and bypassed the gates). Now that
      // the appointment is fully populated, auto-approve it for internal
      // bookers in a single transaction whose gates run on complete data.
      await this.autoApproveIfInternalBooker(createdAppointment?.id);

      this.router.navigateByUrl('/');
    } catch (err: unknown) {
      // Slot rework plan 5: surface the 3 new booking error codes inline
      // and refetch the picker so subsequent attempts see current state.
      // Other errors fall through to a generic toast. ABP screens only
      // [401,403,404,500] in withHttpErrorConfig, so a 400 reaches here.
      const httpErr = err as { error?: { error?: { code?: string; message?: string } } };
      const code = httpErr?.error?.error?.code;
      const message = httpErr?.error?.error?.message;
      if (
        code === 'CaseEvaluation:Appointment.BookingSlotFull' ||
        code === 'CaseEvaluation:Appointment.BookingSlotClosed' ||
        code === 'CaseEvaluation:Appointment.BookingSlotTypeMismatch'
      ) {
        this.toaster.warn(message ?? 'This slot is no longer available.');
        this.form.patchValue(
          { appointmentTime: null, doctorAvailabilityId: null },
          { emitEvent: false },
        );
        this.loadAvailableDatesBySelection();
        return;
      }
      this.toaster.error(message ?? 'Booking failed.');
    } finally {
      this.isSaving = false;
    }
  }

  save(): void {
    this.onSubmit();
  }

  /**
   * F1/F2 fix (2026-06-07): auto-approve a freshly booked appointment when the
   * booker is internal staff. Runs AFTER the party/injury attach sequence so
   * the appointment is fully populated and the server-side approval gates
   * (>=1 injury, >=1 active claim examiner) pass. A failed approve is
   * swallowed with a warning -- the booking already exists and can be approved
   * from the appointment view, so navigation is never blocked.
   */
  private async autoApproveIfInternalBooker(appointmentId: string | undefined): Promise<void> {
    if (!appointmentId || !this.isInternalBooker) {
      return;
    }
    const responsibleUserId = this.currentUser?.id;
    if (!responsibleUserId) {
      return;
    }
    try {
      await firstValueFrom(
        this.appointmentApprovalService.approveAppointment(appointmentId, {
          primaryResponsibleUserId: responsibleUserId,
        }),
      );
    } catch {
      this.toaster.warn(
        'Appointment booked. Auto-approval did not complete -- approve it from the appointment view.',
      );
    }
  }

  // F2 (2026-05-29): validate each enabled, non-empty address group, and where
  // the provider returns a differing standardized form, prompt the user (one
  // consolidated dialog) to use it or keep theirs, applying the choices before
  // the booking POSTs. Any failure (state lookup, provider) is swallowed so the
  // booking is never blocked by the address service.
  private async standardizeAddressesBeforeSubmit(): Promise<void> {
    let stateOptions: StateLookupOption[];
    try {
      const res = await firstValueFrom(
        this.getStateLookup({ maxResultCount: 1000, skipCount: 0, filter: '' }),
      );
      stateOptions = (res?.items ?? []).map((i) => ({
        id: String(i.id),
        name: i.displayName ?? '',
      }));
    } catch {
      return;
    }

    const stateName = (id: unknown): string =>
      stateOptions.find((o) => o.id === String(id ?? ''))?.name ?? '';

    const items: AddressDiffItem[] = [];
    const pending: { key: string; fields: AddressFieldMap; std: StandardizedAddress }[] = [];

    for (const grp of this.addressGroupsForStandardization) {
      if (!grp.isEnabled()) continue;
      const street = (this.form.get(grp.fields.street)?.value ?? '').toString().trim();
      if (!street) continue;

      const input: AddressInput = {
        street,
        suite: grp.fields.suite ? (this.form.get(grp.fields.suite)?.value ?? null) : null,
        city: this.form.get(grp.fields.city)?.value ?? null,
        state: stateName(this.form.get(grp.fields.state)?.value),
        zip: this.form.get(grp.fields.zip)?.value ?? null,
      };

      let result;
      try {
        result = await firstValueFrom(this.addressProvider.validate(input));
      } catch {
        continue;
      }
      if (result.status === 'error' || !result.standardized || result.matchesInput) continue;

      const std = result.standardized;
      const suggestedState = stateName(resolveStateId(std.state, stateOptions)) || std.state;
      items.push({
        key: grp.key,
        label: grp.label,
        enteredLines: this.formatAddressLines(
          input.street,
          input.suite,
          input.city,
          input.state,
          input.zip,
        ),
        suggestedLines: this.formatAddressLines(
          std.street,
          std.suite,
          std.city,
          suggestedState,
          std.zip,
        ),
      });
      pending.push({ key: grp.key, fields: grp.fields, std });
    }

    if (items.length === 0) return;

    const choices = await new Promise<Record<string, AddressChoice>>((resolve) => {
      this.addressDialogResolve = resolve;
      this.addressDialogItems = items;
    });
    this.addressDialogItems = null;
    this.addressDialogResolve = undefined;

    for (const p of pending) {
      if (choices[p.key] !== 'suggested') continue;
      const patch: Record<string, unknown> = {
        [p.fields.street]: p.std.street,
        [p.fields.city]: p.std.city,
        [p.fields.zip]: p.std.zip,
      };
      if (p.fields.suite && p.std.suite) patch[p.fields.suite] = p.std.suite;
      const stateId = resolveStateId(p.std.state, stateOptions);
      if (stateId) patch[p.fields.state] = stateId;
      this.form.patchValue(patch);
    }
  }

  /** Builds two display lines ("street suite" / "city, ST zip") for the dialog. */
  private formatAddressLines(
    street?: string | null,
    suite?: string | null,
    city?: string | null,
    state?: string | null,
    zip?: string | null,
  ): string[] {
    const line1 = [street, suite]
      .map((s) => (s ?? '').trim())
      .filter(Boolean)
      .join(' ');
    const cityState = [city, state]
      .map((s) => (s ?? '').trim())
      .filter(Boolean)
      .join(', ');
    const line2 = [cityState, (zip ?? '').trim()].filter(Boolean).join(' ');
    return [line1, line2].filter((s) => s.trim().length > 0);
  }

  onAddressDialogResolved(choices: Record<string, AddressChoice>): void {
    this.addressDialogResolve?.(choices);
  }

  reset(): void {
    this.form.reset();
    // BUG-044: both attorney sections are mandatory; form.reset() nulls the
    // Enabled flags, so re-assert them to keep the required validators on.
    this.form.patchValue({ applicantAttorneyEnabled: true, defenseAttorneyEnabled: true });
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
    this.router.navigateByUrl('/user-management/patients/my-profile');
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

    // Absolute 90-day ceiling: disabled for every role (the state does not
    // allow scheduling further out). External 60-90 dates stay enabled here
    // and are intercepted with the contact-staff notice on selection.
    if (this.isBeyondAbsoluteBookingCeiling(date)) {
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

  /**
   * Wave 4 / #15 (NEW-only enhancement, PARITY-FLAG-NEW-004): looks up the
   * English AppointmentLanguage row once to cache its GUID. The
   * `?filter=English&maxResultCount=1` shape matches the existing
   * `getAppointmentLanguageLookup` server contract. Re-runs
   * `applyEnglishInterpreterLock` after the cache is populated so a
   * pre-filled English value (e.g. patient profile prefill) locks the
   * interpreter immediately, even if the lookup completes after the
   * profile load fires its initial valueChanges event.
   */
  private loadEnglishLanguageId(): void {
    if (this.englishLanguageLookupComplete) {
      return;
    }
    this.englishLanguageLookupComplete = true;
    this.getAppointmentLanguageLookup({
      filter: 'English',
      skipCount: 0,
      maxResultCount: 1,
    }).subscribe({
      next: (response) => {
        const match = response?.items?.find(
          (item) => item.displayName?.trim().toLowerCase() === 'english',
        );
        this.englishLanguageId = match?.id ?? null;
        if (this.englishLanguageId) {
          this.applyEnglishInterpreterLock(this.form.get('appointmentLanguageId')?.value ?? null);
        }
      },
      error: () => {
        // If the lookup fails the lock simply stays disabled until next
        // language change retries -- the form remains usable.
        this.englishLanguageLookupComplete = false;
      },
    });
  }

  /**
   * Wave 4 / #15 (interpreter behavior changed by I7, 2026-06-08): when the
   * selected language is English, DEFAULT `needsInterpreter` to No -- but keep
   * the radio ENABLED so a booker can still request an interpreter (e.g. ASL)
   * for an English speaker. `emitEvent: false` on the setValue suppresses the
   * cascading valueChanges that would otherwise re-enter via the
   * `interpreterVendorName` validators or the @if-rendered vendor input.
   */
  private applyEnglishInterpreterLock(currentLanguageId: string | null): void {
    const interpreterCtrl = this.form.get('needsInterpreter');
    if (!interpreterCtrl) {
      return;
    }
    if (!this.englishLanguageId) {
      // Lookup not done yet -- do not touch the control. The lookup
      // success branch will re-call this method once the cache is set.
      return;
    }
    const isEnglish = !!currentLanguageId && currentLanguageId === this.englishLanguageId;
    if (isEnglish) {
      if (interpreterCtrl.value !== false) {
        interpreterCtrl.setValue(false, { emitEvent: false });
      }
      // Also clear the conditional vendor name so a pre-filled value
      // does not silently ride along on submit when the @if hides it.
      const vendorCtrl = this.form.get('interpreterVendorName');
      if (vendorCtrl?.value) {
        vendorCtrl.setValue(null, { emitEvent: false });
      }
    } else {
      // Defensive: re-enable if some earlier state had disabled it. The English
      // branch no longer disables (I7), so this is normally a no-op.
      if (interpreterCtrl.disabled) {
        interpreterCtrl.enable({ emitEvent: false });
      }
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
        // Firm-model (D7 / C4): never auto-seed the attorney sections from the
        // booker's own identity. A firm/paralegal AA or DA books on behalf of a
        // DISTINCT attorney, so both sections start blank + editable. The former
        // auto-load (loadApplicant/DefenseAttorneyForCurrentUser) was removed
        // because the *-details-for-booking endpoint returns the firm's OWN
        // email + registration firm name for a firm account, which would re-seed
        // the very identity we want kept out of the on-behalf section.
        // D7 / Q3 consequence: an AA/DA booker (incl. a solo attorney booking
        // for self) now TYPES the attorney details each booking -- the add form
        // has no AA lookup UI (the email-search box + picker live only on the
        // appointment VIEW page). Submit still persists what they type to a
        // master row keyed by the form email. "Solo attorney retypes" is the
        // accepted trade-off (see the plan's Risks note).
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
          defenseAttorneyIdentityUserId: null,
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
        this.applicantAttorneyId = null;
        this.applicantAttorneyConcurrencyStamp = null;
        this.defenseAttorneyId = null;
        this.defenseAttorneyConcurrencyStamp = null;
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
          identityUserId: patient.identityUserId ?? null,
          firstName: patient.firstName ?? null,
          lastName: patient.lastName ?? null,
          middleName: patient.middleName ?? null,
          email: patient.email ?? null,
          genderId: this.normalizePatientGender(patient.genderId),
          dateOfBirth: this.normalizePatientDateOfBirth(patient.dateOfBirth as string | null),
          cellPhoneNumber: patient.cellPhoneNumber ?? null,
          phoneNumber: patient.phoneNumber ?? null,
          phoneNumberTypeId: (patient.phoneNumberTypeId as number | undefined) ?? null,
          socialSecurityNumber: null, // F1 / Design B: SSN is never pre-filled
          street: patient.street ?? null,
          address: patient.address ?? null,
          city: patient.city ?? null,
          stateId: patient.stateId ?? null,
          zipCode: patient.zipCode ?? null,
          appointmentLanguageId: patient.appointmentLanguageId ?? null,
          interpreterVendorName: patient.interpreterVendorName ?? null,
          needsInterpreter: !!patient.interpreterVendorName,
          refferedBy: null, // 2026-06-09: not prefilled -- per-booking optional field
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
          genderId: this.normalizePatientGender(profile.patient.genderId),
          dateOfBirth: this.normalizePatientDateOfBirth(
            profile.patient.dateOfBirth as string | null,
          ),
          cellPhoneNumber: profile.patient.cellPhoneNumber ?? null,
          phoneNumber: profile.patient.phoneNumber ?? null,
          phoneNumberTypeId: (profile.patient.phoneNumberTypeId as number | undefined) ?? null,
          socialSecurityNumber: null, // F1 / Design B: SSN is never pre-filled
          street: profile.patient.street ?? null,
          address: profile.patient.address ?? null,
          city: profile.patient.city ?? null,
          stateId: profile.patient.stateId ?? null,
          zipCode: profile.patient.zipCode ?? null,
          appointmentLanguageId: profile.patient.appointmentLanguageId ?? null,
          interpreterVendorName: profile.patient.interpreterVendorName ?? null,
          needsInterpreter: !!profile.patient.interpreterVendorName,
          refferedBy: null, // 2026-06-09: not prefilled -- per-booking optional field
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

  /**
   * G-06-08 (2026-06-01): a registered-but-not-yet-booked patient carries
   * Gender.Unspecified (0) -- the registration sentinel, never a real choice.
   * Treat it (and any missing value) as "not provided" so the booking form's
   * gender dropdown is not pre-selected with a fabricated value. Mirrors the
   * normalizePatientDateOfBirth guard for the DOB sentinel.
   */
  private normalizePatientGender(value: unknown): number | null {
    const id = value as number | null | undefined;
    if (id === undefined || id === null || id === 0) {
      return null;
    }
    return id;
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
          genderId: this.normalizePatientGender(patient.genderId),
          dateOfBirth: this.normalizePatientDateOfBirth(patient.dateOfBirth as string | null),
          cellPhoneNumber: patient.cellPhoneNumber ?? null,
          phoneNumber: patient.phoneNumber ?? null,
          phoneNumberTypeId: (patient.phoneNumberTypeId as number | undefined) ?? null,
          socialSecurityNumber: null, // F1 / Design B: SSN is never pre-filled
          street: patient.street ?? null,
          address: patient.address ?? null,
          city: patient.city ?? null,
          stateId: patient.stateId ?? null,
          zipCode: patient.zipCode ?? null,
          appointmentLanguageId: patient.appointmentLanguageId ?? null,
          interpreterVendorName: patient.interpreterVendorName ?? null,
          needsInterpreter: !!patient.interpreterVendorName,
          refferedBy: null, // 2026-06-09: not prefilled -- per-booking optional field
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

  // #121 phase T2 (2026-05-13) -- modal + table helpers all moved to
  // AppointmentAddAuthorizedUsersComponent: openAdd / openEdit / close /
  // saveFromModal / remove / getAccessTypeLabel. Group J (2026-06-05)
  // dropped the user-picker, so the section no longer consumes this
  // lookup; the parent retains loadExternalAuthorizedUsers only because
  // the Applicant + Defense Attorney sections still use the result.

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
          // G-01-07: a re-request auto-prefill can build the authorized-user
          // drafts before this lookup resolves, leaving userRole blank. Backfill
          // the role once the options arrive (mirrors the view page).
          this.backfillAuthorizedUserRoles();
        },
      });
  }

  /** Re-resolve userRole on prefilled authorized users once the lookup loads. */
  private backfillAuthorizedUserRoles(): void {
    if (
      this.appointmentAuthorizedUsers.length === 0 ||
      this.externalAuthorizedUserOptions.length === 0
    ) {
      return;
    }
    this.appointmentAuthorizedUsers = this.appointmentAuthorizedUsers.map((user) => {
      const option = this.externalAuthorizedUserOptions.find(
        (x) => x.identityUserId === user.identityUserId,
      );
      return option ? { ...user, userRole: option.userRole || user.userRole } : user;
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
              email: item.email,
              firstName: item.firstName || undefined,
              lastName: item.lastName || undefined,
              role: item.userRole,
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
        (items ?? []).forEach((availability) => {
          // Slot rework plan 5: lookup returns the flat DoctorAvailabilityDto
          // shape (not the WithNavigationProperties envelope). The list-page
          // shape had item.doctorAvailability.{availableDate,fromTime,id};
          // the lookup shape exposes those fields directly.
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

    // 2026-06-11: external users may SEE slots beyond their 60-day online
    // horizon (the picker only caps the shared 90-day ceiling), but cannot
    // book them online. When the picked date exceeds the role horizon
    // (60 external / 90 internal) show the contact-staff notice and clear the
    // selection. The server BookingPolicyValidator is authoritative; this is
    // the friendly UX guard so the user is not silently rejected on submit.
    if (this.daysFromTodayKey(dateKey) > this.maxBookingDays) {
      this.showContactStaffForFurtherBooking();
      this.clearAppointmentDate();
      return;
    }

    this.populateTimeSlotsForDate(dateKey);
  }

  /**
   * 2026-06-11: informational modal shown when an external user selects a slot
   * beyond the 60-day online booking horizon. Single OK button (hideCancelBtn);
   * the message directs them to contact staff, who can schedule 60-90 days out.
   */
  private showContactStaffForFurtherBooking(): void {
    this.confirmationService
      .info(
        '::Appointment:ContactStaffForFurtherBookingMessage',
        '::Appointment:ContactStaffForFurtherBookingTitle',
        { hideCancelBtn: true, yesText: 'AbpUi::Ok' },
      )
      .subscribe();
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

  /** Whole days between today (local midnight) and the given YYYY-MM-DD key. */
  private daysFromTodayKey(dateKey: string): number {
    const [year, month, day] = dateKey.split('-').map(Number);
    const selected = new Date(year, month - 1, day); // month is 0-indexed in JS
    selected.setHours(0, 0, 0, 0);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const msPerDay = 24 * 60 * 60 * 1000;
    return Math.round((selected.getTime() - today.getTime()) / msPerDay);
  }

  /**
   * 2026-06-11: the absolute booking ceiling (internalMaxBookingDays = 90)
   * applies to every booker -- nobody schedules beyond it, so the picker
   * disables those dates for all roles. Between 60 and 90 days, external
   * users still SEE the dates but get the contact-staff notice on selection
   * (handled in onAppointmentDateChanged); internal staff book them directly.
   */
  private isBeyondAbsoluteBookingCeiling(date: NgbDateStruct): boolean {
    if (!date) return false;
    const key = this.toDateKey(date.year, date.month, date.day);
    return this.daysFromTodayKey(key) > this.internalMaxBookingDays;
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
    // and reads SystemParameter.AppointmentLeadTime per-tenant.
    return selected < threshold;
  }

  // Slot rework plan 5: read from /api/app/doctor-availabilities/lookup
  // instead of the paged list. The lookup applies tenant lead-time, hides
  // Reserved/Booked, and excludes slots with zero remaining capacity --
  // so the picker is binary-available by construction.
  private async fetchAllAvailableSlots(
    locationId: string,
    appointmentTypeId: string,
  ): Promise<DoctorAvailabilityDto[]> {
    return firstValueFrom(
      this.doctorAvailabilityService.getDoctorAvailabilityLookup({
        locationId,
        appointmentTypeId: appointmentTypeId || null,
      }),
    );
  }

  // #121 phase T4 (2026-05-13) -- 14 methods moved to
  // AppointmentAddClaimInformationComponent: buildInjuryForm,
  // makeEmptyInjuryDraft, applyInsuranceRequiredValidators,
  // applyClaimExaminerRequiredValidators, clearInjuryToggleSubscriptions,
  // serializeInjuryForm, loadInjuryLookups, openAddInjuryModal,
  // applyClaimExaminerRolePrefill, openEditInjuryModal,
  // closeInjuryModal, saveInjuryModal, removeInjury,
  // injuryWcabOfficeName. Parent keeps persistInjuryDraftsIfProvided
  // below because the POST cascade is part of the submit flow.

  // CI1 (2026-06-05): one Claim Examiner per appointment (required). Posted
  // after create; Name + Email are guaranteed present by the parent
  // Validators.required gate, so this always inserts when an appointment exists.
  private async createAppointmentClaimExaminerIfProvided(appointmentId?: string): Promise<void> {
    if (!appointmentId) {
      return;
    }
    const raw = this.form.getRawValue();
    await firstValueFrom(
      this.restService.request<any, any>(
        {
          method: 'POST',
          url: '/api/app/appointment-claim-examiners',
          body: {
            appointmentId,
            isActive: true,
            name: raw.appointmentClaimExaminerName,
            email: raw.appointmentClaimExaminerEmail,
            suite: raw.appointmentClaimExaminerSuite,
            phoneNumber: raw.appointmentClaimExaminerPhoneNumber,
            fax: raw.appointmentClaimExaminerFax,
            street: raw.appointmentClaimExaminerStreet,
            city: raw.appointmentClaimExaminerCity,
            zip: raw.appointmentClaimExaminerZip,
            stateId: raw.appointmentClaimExaminerStateId,
          },
        },
        { apiName: 'Default' },
      ),
    );
  }

  // CI1 (2026-06-05): one optional Primary Insurance per appointment. Posted
  // after create only when a company name was entered.
  private async createAppointmentPrimaryInsuranceIfProvided(appointmentId?: string): Promise<void> {
    if (!appointmentId) {
      return;
    }
    const raw = this.form.getRawValue();
    const name = (raw.appointmentInsuranceName ?? '').trim();
    if (!name) {
      return;
    }
    await firstValueFrom(
      this.restService.request<any, any>(
        {
          method: 'POST',
          url: '/api/app/appointment-primary-insurances',
          body: {
            appointmentId,
            isActive: true,
            name: raw.appointmentInsuranceName,
            suite: raw.appointmentInsuranceSuite,
            phoneNumber: raw.appointmentInsurancePhoneNumber,
            faxNumber: raw.appointmentInsuranceFaxNumber,
            street: raw.appointmentInsuranceStreet,
            city: raw.appointmentInsuranceCity,
            zip: raw.appointmentInsuranceZip,
            stateId: raw.appointmentInsuranceStateId,
          },
        },
        { apiName: 'Default' },
      ),
    );
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

      // OBS-41 (2026-05-27): persist structured body-part rows
      // (description-only) to the existing CRUD endpoint. The injury's
      // BodyPartsSummary (derived comma-join) was already sent above so
      // legacy readers (view fallback, repo filter-text) keep working.
      for (const description of draft.bodyParts ?? []) {
        const trimmed = (description ?? '').trim();
        if (!trimmed) continue;
        await firstValueFrom(
          this.restService.request<any, any>(
            {
              method: 'POST',
              url: '/api/app/appointment-body-parts',
              body: {
                appointmentInjuryDetailId: injuryId,
                bodyPartDescription: trimmed,
              },
            },
            { apiName: 'Default' },
          ),
        );
      }

      // CI1 (2026-06-05): per-injury insurance/CE POSTs removed -- insurance +
      // CE are now single appointment-level records posted once after create
      // (createAppointmentPrimaryInsuranceIfProvided / ...ClaimExaminer...).
    }
  }
}
