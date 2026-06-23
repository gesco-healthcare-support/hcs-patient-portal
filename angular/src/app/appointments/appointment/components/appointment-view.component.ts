import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { SsnInputComponent } from '../../../shared/components/ssn-input.component';
import {
  ConfigStateService,
  EnvironmentService,
  ListResultDto,
  LocalizationPipe,
  LocalizationService,
  PagedResultDto,
  PermissionDirective,
  RestService,
} from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import type {
  AppointmentDto,
  AppointmentUpdateDto,
  AppointmentWithNavigationPropertiesDto,
} from '../../../proxy/appointments/models';
import { AppointmentStatusType } from '../../../proxy/enums/appointment-status-type.enum';
import { Gender, genderOptions } from '../../../proxy/enums/gender.enum';
import { phoneNumberTypeOptions } from '../../../proxy/enums/phone-number-type.enum';
import type { PatientUpdateDto } from '../../../proxy/patients/models';
import type { LookupDto, LookupRequestDto } from '../../../proxy/shared/models';
import { AppointmentService } from '../../../proxy/appointments/appointment.service';
import { AppLookupSelectComponent } from '../../../shared/components/app-lookup-select.component';
import { catchError, debounceTime, distinctUntilChanged, map, switchMap } from 'rxjs/operators';
import { firstValueFrom, Observable, of } from 'rxjs';
import {
  NgbDatepickerModule,
  NgbDateStruct,
  NgbTypeaheadModule,
  NgbTypeaheadSelectItemEvent,
} from '@ng-bootstrap/ng-bootstrap';
import { ApproveConfirmationModalComponent } from './approve-confirmation-modal.component';
import { RejectAppointmentModalComponent } from './reject-appointment-modal.component';
import { CancelAppointmentModalComponent } from './cancel-appointment-modal.component';
import { RescheduleRequestModalComponent } from './reschedule-request-modal.component';
import { CancellationRequestModalComponent } from './cancellation-request-modal.component';
import { RequestInfoModalComponent } from './request-info-modal.component';
import type { AppointmentChangeRequestDto } from '../../../proxy/appointment-change-requests/models';
import { ChangeRequestType } from '../../../proxy/appointment-change-requests/change-request-type.enum';
import { AppointmentDocumentsComponent } from '../../../appointment-documents/appointment-documents.component';
import { AppointmentPacketComponent } from '../../../appointment-packet/appointment-packet.component';
import { wireAttorneySectionToggle } from '../../shared/attorney-section-validators';
import { resolveExternalUserDisplayName } from '../../../shared/auth/external-user-display-name';

type TransitionAction = 'approve' | 'reject' | 'cancel';

type ExternalAuthorizedUserOption = {
  identityUserId: string;
  firstName: string;
  lastName: string;
  email: string;
  userRole: string;
  // Phase 1 / C2 / D4 (2026-06-11): firm name from the external-user lookup so
  // the picker shows a firm account's firm name instead of a blank/raw email.
  firmName?: string;
};

type AppointmentAuthorizedUserRow = {
  accessorId: string;
  identityUserId: string;
  firstName: string;
  lastName: string;
  email: string;
  userRole: string;
  accessTypeId: number;
};

// S-5.4: Single-row read-model for the Claim Information table on the view
// page. Pre-flattens the WCAB office display name + insurance/CE summary so
// the template binds via dot-access without nested null guards.
type AppointmentInjuryDetailRow = {
  id: string;
  dateOfInjury: string | null;
  toDateOfInjury: string | null;
  isCumulativeInjury: boolean;
  claimNumber: string;
  wcabAdj: string;
  wcabOfficeName: string;
  // OBS-41 (2026-05-27): structured per-body-part descriptions from the
  // nav-properties response (item.bodyParts[]). bodyPartsSummary is kept as a
  // fallback for legacy injuries that have only the summary string (no rows).
  bodyParts: string[];
  bodyPartsSummary: string;
  insuranceCompanyName: string;
  claimExaminerName: string;
};

// S-5.4: shape returned by GET /defense-attorney-details-for-booking and
// GET /{appointmentId}/defense-attorney. Mirror of ApplicantAttorneyDetailsDto.
type DefenseAttorneyLookupResult = {
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
};

// #122 (2026-05-14): shape returned by AA load endpoints. Mirror of
// applicant-attorney-details-for-booking + /{appointmentId}/applicant-attorney.
type ApplicantAttorneyLookupResult = {
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
};

@Component({
  selector: 'app-appointment-view',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    LocalizationPipe,
    PermissionDirective,
    AppLookupSelectComponent,
    NgbDatepickerModule,
    NgbTypeaheadModule,
    ApproveConfirmationModalComponent,
    RejectAppointmentModalComponent,
    CancelAppointmentModalComponent,
    RescheduleRequestModalComponent,
    CancellationRequestModalComponent,
    RequestInfoModalComponent,
    AppointmentDocumentsComponent,
    AppointmentPacketComponent,
    SsnInputComponent,
  ],
  templateUrl: './appointment-view.component.html',
})
export class AppointmentViewComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly configState = inject(ConfigStateService);
  private readonly appointmentService = inject(AppointmentService);
  private readonly restService = inject(RestService);
  private readonly http = inject(HttpClient);
  private readonly environmentService = inject(EnvironmentService);
  private readonly toaster = inject(ToasterService);
  private readonly localization = inject(LocalizationService);

  // W1-1: state-machine transition UI
  readonly AppointmentStatusType = AppointmentStatusType;
  approveModalVisible = false;
  rejectModalVisible = false;
  cancelModalVisible = false;
  // AP1 (decision 4): external-initiated change-request entry on the read-only
  // Review page (Approved appointments only).
  rescheduleRequestVisible = false;
  cancelRequestVisible = false;
  // Send Back (2026-06-14): staff "Request info" modal visibility (Pending only).
  requestInfoModalVisible = false;

  // B8 (2026-05-06): widen the DOB datepicker year range. Default
  // ngbDatepicker only navigates +/-10 years; with [minDate]/[maxDate]
  // and `navigation="select"` the header shows year + month selects
  // spanning the full configured range.
  readonly dobMinDate: NgbDateStruct = { year: 1920, month: 1, day: 1 };
  readonly dobMaxDate: NgbDateStruct = (() => {
    const today = new Date();
    return { year: today.getFullYear(), month: today.getMonth() + 1, day: today.getDate() };
  })();

  appointment: AppointmentWithNavigationPropertiesDto | null = null;
  isLoading = true;
  isSaving = false;
  errorMessage = '';
  successMessage = '';
  // I6 (2026-06-08): drop the Gender.Unspecified (value 0) radio -- it has no
  // localized label (renders the raw "Enum:Gender.0" key) and is not a valid
  // selection.
  readonly genderOptions = genderOptions.filter((option) => option.value !== Gender.Unspecified);
  readonly phoneNumberTypeOptions = phoneNumberTypeOptions;
  readonly accessTypeOptions = [
    { value: 23, label: 'View' },
    { value: 24, label: 'Edit' },
  ];
  // Group J: valid accessor roles -- email is free-typed, role chosen from
  // the seeded external roles. Order mirrors the invite-user dropdown.
  readonly roleOptions = ['Patient', 'Applicant Attorney', 'Defense Attorney', 'Claim Examiner'];
  externalAuthorizedUserOptions: ExternalAuthorizedUserOption[] = [];
  appointmentAuthorizedUsers: AppointmentAuthorizedUserRow[] = [];
  isAuthorizedUserModalOpen = false;
  authorizedUserModalMode: 'create' | 'edit' = 'create';
  editingAuthorizedUserId: string | null = null;

  // #122 (2026-05-14): IDs + concurrency stamps that live OUTSIDE the
  // FormGroup -- they are not user-editable inputs, just metadata used at
  // save time to detect existing-vs-new and to send the right HTTP verb.
  employerDetailId: string | null = null;
  employerDetailConcurrencyStamp: string | null = null;
  applicantAttorneyId: string | null = null;
  defenseAttorneyId: string | null = null;

  isApplicantAttorneyLoading = false;
  applicantAttorneyOptions: ExternalAuthorizedUserOption[] = [];
  isDefenseAttorneyLoading = false;
  defenseAttorneyOptions: ExternalAuthorizedUserOption[] = [];

  // S-5.4 (W-A-7): Claim Information section on the view page. Read-only table
  // sourced from `/api/app/appointment-injury-details/by-appointment/<id>`. The
  // booking form (appointment-add) supports add/edit/delete on these rows; the
  // view page surfaces them as a table only at MVP.
  injuryDetails: AppointmentInjuryDetailRow[] = [];
  // CI1 (2026-06-05): single appointment-level Claim Examiner + Insurance,
  // read from the appointment nav-props (data.claimExaminer / data.primaryInsurance).
  appointmentClaimExaminerName = '';
  appointmentInsuranceCompanyName = '';

  // CE-VIEW / INS-VIEW (2026-06-09): the dedicated read-only Claim Examiner
  // and Insurance sections render straight from the appointment nav-props
  // (appointment.claimExaminer / appointment.primaryInsurance) in the
  // template. stateNamesById resolves the stored StateId GUID to a display
  // name for those sections; the editable address blocks elsewhere use the
  // app-lookup-select component, but these sections are plain read-only.
  private readonly stateNamesById = new Map<string, string>();

  stateName(id?: string | null): string {
    return id ? (this.stateNamesById.get(id) ?? '') : '';
  }

  private loadStateNames(): void {
    this.getStateLookup({
      filter: '',
      skipCount: 0,
      maxResultCount: 100,
    } as LookupRequestDto).subscribe({
      next: (res) => {
        (res.items ?? []).forEach((item) => {
          if (item.id) {
            this.stateNamesById.set(item.id, item.displayName ?? '');
          }
        });
      },
    });
  }

  // #122 (2026-05-14): flat + prefixed FormGroup mirrors booker (#121) shape
  // so future shared section components (e.g. <app-patient-demographics>) can
  // drop in once both pages expose the same control surface. Save reads via
  // `this.form.getRawValue()`; loads call `this.form.patchValue({...})`.
  //
  // 2026-05-14 (validator parity): per-field Validators copied from the
  // booker's `appointment-add.component.ts` form definition. Applied as
  // SOFT validators -- the template uses `[class.is-invalid]` decorations
  // on each input to surface invalidity visually, but save() does NOT gate
  // on form.invalid. The existing manual null-check on the appointment IDs
  // remains the only blocking guard; the server is authoritative on data
  // validity. External roles (Patient/AA/DA/CE) have the whole form
  // disabled in ngOnInit, so these validators only affect internal staff
  // editing existing records.
  // AF3 + AF4 (2026-06-04): mirrors CaseEvaluationSeedIds.AppointmentTypes.PanelQme.
  // The appointment type is fixed on this view/edit form (type changes are
  // cancel+rebook per AP1), so the Panel Number state is applied once from the
  // loaded appointmentTypeId in ngOnInit. No proxy enum exists for seed-data GUIDs.
  private readonly PQME_TYPE_ID = 'a0a00002-0000-4000-9000-000000000002';

  readonly form: FormGroup = this.fb.group({
    // top-level
    panelNumber: [null as string | null, [Validators.maxLength(50)]],
    // F4-02 (2026-05-26) -- read-only display of staff outcomes. RejectionNotes
    // surfaces the reason captured by the Reject modal so the patient (and
    // staff later opening the rejected appointment) can see WHY. InternalUserComments
    // is the approval-modal "Any comments?" textarea; server redacts it to null
    // for external roles, so external viewers see the row hidden via the
    // null-guard in the template.
    rejectionNotes: [null as string | null],
    internalUserComments: [null as string | null],
    // patient (19 controls)
    patientFirstName: [null as string | null, [Validators.required, Validators.maxLength(50)]],
    patientLastName: [null as string | null, [Validators.required, Validators.maxLength(50)]],
    patientMiddleName: [null as string | null, [Validators.maxLength(50)]],
    patientEmail: [
      null as string | null,
      [Validators.required, Validators.maxLength(50), Validators.email],
    ],
    patientGenderId: [null as number | null],
    // S-5.5: ngbDatepicker's CVA needs NgbDateStruct; load helper converts
    // ISO -> { year, month, day } on first patch.
    patientDateOfBirth: [null as NgbDateStruct | string | null, [Validators.required]],
    patientCellPhoneNumber: [null as string | null, [Validators.maxLength(12)]],
    patientPhoneNumber: [null as string | null, [Validators.maxLength(20)]],
    patientPhoneNumberTypeId: [null as number | null],
    patientSocialSecurityNumber: [null as string | null, [Validators.maxLength(20)]],
    patientStreet: [null as string | null, [Validators.maxLength(255)]],
    patientAddress: [null as string | null, [Validators.maxLength(100)]],
    patientApptNumber: [null as string | null, [Validators.maxLength(100)]], // "Unit #" -- view page only field
    patientCity: [null as string | null, [Validators.maxLength(50)]],
    patientStateId: [null as string | null],
    patientZipCode: [null as string | null, [Validators.maxLength(15)]],
    patientAppointmentLanguageId: [null as string | null],
    patientNeedsInterpreter: [false],
    patientInterpreterVendorName: [null as string | null, [Validators.maxLength(255)]],
    patientRefferedBy: [null as string | null, [Validators.maxLength(50)]],
    // employer (7 controls)
    employerName: [null as string | null, [Validators.required, Validators.maxLength(255)]],
    employerOccupation: [null as string | null, [Validators.required, Validators.maxLength(255)]],
    employerPhoneNumber: [null as string | null, [Validators.maxLength(12)]],
    employerStreet: [null as string | null, [Validators.maxLength(255)]],
    employerCity: [null as string | null, [Validators.maxLength(255)]],
    employerStateId: [null as string | null],
    employerZipCode: [null as string | null, [Validators.maxLength(10)]],
    // applicant attorney (14 controls + enabled toggle + email search)
    applicantAttorneyEnabled: [true],
    applicantAttorneyEmailSearch: [null as string | null],
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
    // defense attorney (mirror of AA)
    defenseAttorneyEnabled: [true],
    defenseAttorneyEmailSearch: [null as string | null],
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
  });

  /**
   * Soft-validator helper: returns true when the control is invalid AND
   * the user has interacted with it (touched). Template `[class.is-invalid]`
   * decorations use this so the red border only appears AFTER the user has
   * focused + blurred the field -- empty initial state on a new appointment
   * load does not show angry red borders on every required field.
   *
   * Save() does NOT consult this; the server remains authoritative on data
   * validity. This is purely a UX cue for internal staff editing existing
   * records.
   */
  isFieldInvalid(controlName: string): boolean {
    const control = this.form.get(controlName);
    return !!control && control.invalid && control.touched;
  }

  // BUG-012 Sub-bug 2 (2026-05-22) -- the previous private
  // applyConditionalAttorneySectionValidators method moved to
  // ../../shared/attorney-section-validators.ts so it can be shared
  // with appointment-add.component.ts. The ngOnInit block above now
  // uses the `wireAttorneySectionToggle` convenience wrapper.

  // #122 (2026-05-14): authorized-user modal sub-form. Kept separate from
  // `form` because it represents draft state for a per-row append/edit
  // operation that submits via its own POST/PUT, not via save().
  readonly authorizedUserForm: FormGroup = this.fb.group({
    // identityUserId is set only when EDITING a persisted accessor (the
    // update contract still keys by it); CREATE resolves the typed email.
    identityUserId: [null as string | null],
    firstName: ['', [Validators.maxLength(64)]],
    lastName: ['', [Validators.maxLength(64)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    userRole: ['', [Validators.required]],
    accessTypeId: [23 as number, [Validators.required]],
  });

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

  ngOnInit(): void {
    // BUG-012 (2026-05-22): conditional required-validator wiring on
    // AA/DA section fields. Mirror of appointment-add.component.ts:454-484.
    // The view/edit form previously declared FirmName + 7 other section
    // fields with maxLength validators only -- so saving an existing
    // appointment with empty Firm Name silently succeeded client-side,
    // and the backend's UpsertApplicantAttorneyForAppointmentAsync did
    // not enforce it either. The matching server-side guard is added in
    // AppointmentsAppService.UpsertApplicantAttorneyForAppointmentAsync.
    // Subscription + initial-apply in one call per section -- see
    // ./shared/attorney-section-validators.ts for the helper's contract.
    wireAttorneySectionToggle(this.form, 'applicantAttorney');
    wireAttorneySectionToggle(this.form, 'defenseAttorney');

    this.loadExternalAuthorizedUsers();
    // CE-VIEW / INS-VIEW (2026-06-09): preload state display names for the
    // read-only Claim Examiner + Insurance sections.
    this.loadStateNames();

    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.errorMessage = 'Appointment id is required.';
      this.isLoading = false;
      return;
    }

    this.appointmentService.getWithNavigationProperties(id).subscribe({
      next: (data) => {
        this.appointment = data;
        this.form.patchValue(
          {
            panelNumber: data.appointment?.panelNumber ?? '',
            // F4-02 (2026-05-26) -- surface server-side staff outcomes.
            rejectionNotes: data.appointment?.rejectionNotes ?? null,
            internalUserComments: data.appointment?.internalUserComments ?? null,
          },
          { emitEvent: false },
        );
        this.loadEmployerDetails(data.appointment?.id);
        this.bindApplicantAttorneyFromResponse(data);
        this.appointmentClaimExaminerName = data.claimExaminer?.name ?? '';
        this.appointmentInsuranceCompanyName = data.primaryInsurance?.name ?? '';
        // S-5.4 (W-A-7): the AppointmentWithNavigationPropertiesDto does not
        // include the DA join (only AA), so DA must be fetched via a dedicated
        // GET against /{id}/defense-attorney. Same for the Claim Information
        // injury list -- retrieved per-appointment from a separate endpoint.
        this.bindDefenseAttorneyForAppointment(data.appointment?.id);
        this.loadInjuryDetails(data.appointment?.id);
        this.loadAppointmentAccessors(data.appointment?.id);
        const patient = data.patient;
        this.form.patchValue(
          {
            patientFirstName: patient?.firstName ?? '',
            patientLastName: patient?.lastName ?? '',
            patientMiddleName: patient?.middleName ?? '',
            patientEmail: patient?.email ?? '',
            patientGenderId: (patient?.genderId as number | undefined) ?? null,
            // S-5.5: parse ISO date string into NgbDateStruct so the datepicker renders.
            patientDateOfBirth: this.parseDateOfBirthFromApi(patient?.dateOfBirth),
            patientCellPhoneNumber: patient?.cellPhoneNumber ?? '',
            patientPhoneNumber: patient?.phoneNumber ?? '',
            patientPhoneNumberTypeId: (patient?.phoneNumberTypeId as number | undefined) ?? null,
            // F1 / Design B (2026-05-29): SSN is never pre-filled. The field
            // starts empty; the stored value is viewed via the reveal endpoint,
            // and an empty submit leaves the stored SSN unchanged (backend rule).
            patientSocialSecurityNumber: '',
            patientStreet: patient?.street ?? '',
            patientAddress: patient?.address ?? '',
            patientApptNumber: patient?.apptNumber ?? '',
            patientCity: patient?.city ?? '',
            patientStateId: patient?.stateId ?? null,
            patientZipCode: patient?.zipCode ?? '',
            patientAppointmentLanguageId: patient?.appointmentLanguageId ?? null,
            patientNeedsInterpreter: !!patient?.interpreterVendorName,
            patientInterpreterVendorName: patient?.interpreterVendorName ?? '',
            // 2026-06-09: Referred By is now a per-appointment field; source it
            // from the appointment, not the patient.
            patientRefferedBy: data.appointment?.refferedBy ?? '',
          },
          { emitEvent: false },
        );
        // AF3 + AF4 (2026-06-04): apply the Panel Number state once from the
        // loaded (fixed) appointment type. PQME -> enabled + required; AME/IME ->
        // cleared + disabled (cleans up any legacy value on edit). Runs BEFORE the
        // read-only gate below so that, for a patient viewer, the subsequent global
        // form.disable() still wins and the field stays locked.
        this.applyPanelNumberStateForType(data.appointment?.appointmentTypeId ?? null);
        // #122 (2026-05-14): O5 strict-parity read-only gate. External roles
        // (Patient / AA / DA / CE) see the form fields visually locked via
        // form.disable(); internal staff edit freely. Server permission
        // attributes remain authoritative on save.
        if (this.isReadOnly) {
          this.form.disable({ emitEvent: false });
        }
        this.isLoading = false;
      },
      error: () => {
        this.errorMessage = 'Unable to load appointment details.';
        this.isLoading = false;
      },
    });
  }

  /**
   * AF3 + AF4 (2026-06-04): Panel Number state machine keyed off the PQME type,
   * mirroring AppointmentAddComponent.applyPanelNumberStateForType. PQME -> the
   * field is enabled + required; any other type (AME / IME) -> the value is
   * cleared, validators drop to length-only, and the control is disabled (cleans
   * up any legacy value on edit instead of blocking the save). The form saves via
   * getRawValue(), so a disabled control still serializes. AppointmentManager
   * enforces the same rule server-side as the authoritative guard.
   */
  private applyPanelNumberStateForType(typeId: string | null): void {
    const control = this.form.get('panelNumber');
    if (!control) return;
    if (typeId === this.PQME_TYPE_ID) {
      control.enable({ emitEvent: false });
      control.setValidators([Validators.required, Validators.maxLength(50)]);
    } else {
      control.setValue(null, { emitEvent: false });
      control.setValidators([Validators.maxLength(50)]);
      control.disable({ emitEvent: false });
    }
    control.updateValueAndValidity({ emitEvent: false });
  }

  goBack(): void {
    this.router.navigateByUrl('/');
  }

  // G-08-04: download the per-appointment Patient Demographics PDF. Internal
  // staff only -- the button is gated by *abpPermission="'CaseEvaluation.Reports'".
  downloadDemographics(): void {
    const appointmentId = this.appointment?.appointment?.id;
    if (appointmentId) {
      void this.downloadDemographicsInternal(appointmentId);
    }
  }

  // Authenticated blob download (HttpClient + anchor click); NEVER window.open
  // (a new tab carries no Bearer token). See angular/src/app/CLAUDE.md.
  private async downloadDemographicsInternal(appointmentId: string): Promise<void> {
    const base = this.environmentService.getApiUrl('Default') ?? '';
    try {
      const response = await firstValueFrom(
        this.http.get(`${base}/api/app/appointment-demographics/${appointmentId}`, {
          observe: 'response',
          responseType: 'blob',
        }),
      );

      const blob = response.body;
      if (!blob) {
        return;
      }

      const disposition = response.headers.get('content-disposition') || '';
      const match = /filename\*?=(?:UTF-8'')?"?([^";]+)/i.exec(disposition);
      const fileName = match ? decodeURIComponent(match[1]) : 'appointment-demographics.pdf';

      const objectUrl = URL.createObjectURL(blob);
      try {
        const anchor = document.createElement('a');
        anchor.href = objectUrl;
        anchor.download = fileName;
        anchor.style.display = 'none';
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
      } finally {
        setTimeout(() => URL.revokeObjectURL(objectUrl), 0);
      }
    } catch {
      this.errorMessage = 'Could not download the demographics PDF.';
    }
  }

  /**
   * True when the booker/viewer is anyone OTHER than the Patient role.
   * Covers AA, DA, CE, AND internal admins/staff. Used SOLELY for the
   * patient-update URL gate: non-Patient -> /patients/for-appointment-booking/<id>;
   * Patient -> /patients/me (W-B-2 fix, 2026-04-30: previously CE + internal
   * bookers fell through to /patients/me and got 404).
   *
   * S-5.3b (W-VIEW-10): do NOT use this getter for read-only gates. It
   * returns true for internal admins, which incorrectly classifies them as
   * external. Use `isPatientUser` (which actually checks any-of-the-4-external
   * -roles) and negate it to detect internal admins instead.
   */
  get isExternalUserNonPatient(): boolean {
    const roles = (this.configState.getOne('currentUser') as any)?.roles ?? [];
    if (!Array.isArray(roles) || roles.length === 0) {
      return true;
    }
    return !roles.some((r: string) => r?.toLowerCase() === 'patient');
  }

  get isApplicantAttorney(): boolean {
    const roles = (this.configState.getOne('currentUser') as any)?.roles ?? [];
    return roles.some((r: string) => r?.toLowerCase() === 'applicant attorney');
  }

  // S-5.4: mirror of isApplicantAttorney for the DA-self case (used to hide
  // the email-search row when a DA is viewing their own appointment).
  get isDefenseAttorney(): boolean {
    const roles = (this.configState.getOne('currentUser') as any)?.roles ?? [];
    return roles.some((r: string) => r?.toLowerCase() === 'defense attorney');
  }

  /**
   * Returns true if the current user holds any external role (Patient,
   * Applicant Attorney, Defense Attorney, Claim Examiner). Used by
   * `isReadOnly` to decide whether view-page form fields are locked.
   * Internal admins (anyone NOT in this set) edit freely; the server's
   * permission attributes remain authoritative.
   */
  get isPatientUser(): boolean {
    const roles = (this.configState.getOne('currentUser') as any)?.roles ?? [];
    const externalUserRoles = new Set([
      'patient',
      'applicant attorney',
      'defense attorney',
      'claim examiner',
    ]);
    return roles.some((role: string) => externalUserRoles.has(role?.toLowerCase() ?? ''));
  }

  /**
   * Read-only gate for view-page form fields. Per O5 strict-parity decision
   * (2026-05-04), OLD has no return-to-booker correction loop -- staff
   * comments via `InternalUserComments` (approve) or `RejectionNotes`
   * (reject) and the booker re-files from scratch on rejection. So:
   *
   *  - Internal admin tier (anyone NOT in the 4 external roles): editable;
   *    server permission attributes remain authoritative.
   *  - External roles (Patient / AA / DA / CE): read-only on the view page.
   *    Booking is the canonical create surface; edits to an existing
   *    appointment are not part of the OLD-parity flow.
   *
   * #122 (2026-05-14): reactive forms toggle this gate at the FormGroup
   * level via `form.disable()` in ngOnInit. The template no longer threads
   * `[disabled]="isReadOnly"` through every input.
   */
  get isReadOnly(): boolean {
    return this.isPatientUser;
  }

  // ----- W1-1 transition state-machine UI helpers -----

  get currentStatus(): AppointmentStatusType | undefined {
    return this.appointment?.appointment?.appointmentStatus as AppointmentStatusType | undefined;
  }

  /**
   * Internal user with the right to take office actions on this appointment.
   * MVP heuristic: any user who is NOT an external booker. Server-side
   * `[Authorize(CaseEvaluationPermissions.Appointments.Edit)]` is the
   * authoritative gate; this getter just hides the dropdown for external
   * users. Tightening to a proper PermissionService check is on the ledger.
   */
  get canTakeOfficeAction(): boolean {
    if (!this.appointment) {
      return false;
    }
    const status = this.currentStatus;
    return (
      // S-5.3b (W-VIEW-10): internal staff = NOT in any of the 4 external roles.
      // Pending shows Approve/Reject; Approved shows the G-02-05 direct Cancel.
      // Server [Authorize(Appointments.*)] gates remain authoritative.
      !this.isPatientUser &&
      (status === AppointmentStatusType.Pending || status === AppointmentStatusType.Approved)
    );
  }

  /**
   * Action keys the office can pick at the current status.
   *  Pending:  approve | reject  (OLD parity -- no send-back path)
   *  Approved: cancel            (G-02-05 one-step staff cancel)
   */
  get availableActions(): TransitionAction[] {
    const status = this.currentStatus;
    if (status === AppointmentStatusType.Pending) {
      return ['approve', 'reject'];
    }
    if (status === AppointmentStatusType.Approved) {
      return ['cancel'];
    }
    return [];
  }

  /**
   * G-01-07: the booker may re-request a REJECTED appointment they created
   * (OLD parity: the appointment-edit "Re-Request" button gated on
   * status == Rejected && createdById == loginUser). creatorId is the ABP
   * audit author = the original booker; identityUserId is the patient's user,
   * so creatorId is the correct "did I create this" check.
   */
  get canReRequest(): boolean {
    const creatorId = this.appointment?.appointment?.creatorId;
    const currentUserId = (this.configState.getOne('currentUser') as any)?.id;
    return (
      this.currentStatus === AppointmentStatusType.Rejected &&
      !!creatorId &&
      creatorId === currentUserId
    );
  }

  /**
   * A view-page caller is "internal" when they hold none of the four external
   * roles -- the same negation `canTakeOfficeAction` uses (isPatientUser covers
   * Patient / AA / DA / CE). Server permission attributes remain authoritative.
   */
  get isInternalUser(): boolean {
    return !this.isPatientUser;
  }

  /**
   * B (2026-06-10): may the current user add/edit/remove accessors on THIS
   * appointment? Internal staff always; an Applicant/Defense Attorney only when
   * they created it (reuses the canReRequest creator-compare). Cosmetic gate for
   * the "Add" control -- the server's EnsureCanManageAccessorsAsync is the real
   * authority. Paralegal-ready: the paralegal feature adds `|| this.isParalegal`
   * to the attorney branch (additive).
   */
  canManageAccessors(): boolean {
    if (this.isInternalUser) {
      return true;
    }
    const creatorId = this.appointment?.appointment?.creatorId;
    const currentUserId = (this.configState.getOne('currentUser') as any)?.id;
    return (
      (this.isApplicantAttorney || this.isDefenseAttorney) &&
      !!creatorId &&
      creatorId === currentUserId
    );
  }

  /**
   * Navigate to the booking form in re-request mode, carrying the source
   * confirmation number. The booking form auto-loads + prefills from the
   * rejected source and submits via reSubmit (which reuses the source conf #).
   */
  reRequest(): void {
    const conf = this.appointment?.appointment?.requestConfirmationNumber;
    if (!conf) {
      return;
    }
    this.router.navigate(['/appointments/add'], {
      queryParams: { mode: 'rerequest', source: conf },
    });
  }

  /** Triggered when the office clicks Approve, Reject, or Cancel in the toolbar. */
  dispatchAction(action: TransitionAction): void {
    if (!this.appointment?.appointment?.id) {
      return;
    }
    switch (action) {
      case 'approve':
        this.approveModalVisible = true;
        break;
      case 'reject':
        this.rejectModalVisible = true;
        break;
      case 'cancel':
        this.cancelModalVisible = true;
        break;
    }
  }

  /**
   * Modal-success callback: refresh nav-properties + reset dropdown.
   *
   * S-7.2 (2.11): the modal hands us the post-transition `AppointmentDto`
   * directly (the server response from Approve / Reject). Patch
   * `appointment.appointment` from that dto immediately so the status pill
   * flips on the same change-detection cycle the modal closed in. We still
   * re-fetch in the background to refresh nav-property snapshots.
   */
  onActionSucceeded(dto: AppointmentDto): void {
    if (this.appointment?.appointment && dto) {
      this.appointment.appointment = { ...this.appointment.appointment, ...dto };
    }
    const id = this.appointment?.appointment?.id;
    if (!id) {
      return;
    }
    this.appointmentService.getWithNavigationProperties(id).subscribe({
      next: (data) => {
        this.appointment = data;
      },
    });
  }

  /**
   * AP1 (decision 4): external bookers (Patient / AA / DA / CE) may request a
   * reschedule or cancellation on an Approved appointment from the read-only
   * Review page. Internal staff use the appointments-list dropdown instead.
   */
  get canRequestChange(): boolean {
    return (
      this.isPatientUser &&
      this.currentStatus === AppointmentStatusType.Approved &&
      !!this.appointment?.appointment?.id
    );
  }

  openRescheduleRequest(): void {
    this.cancelRequestVisible = false;
    this.rescheduleRequestVisible = true;
  }

  openCancelRequest(): void {
    this.rescheduleRequestVisible = false;
    this.cancelRequestVisible = true;
  }

  /** Send Back (2026-06-14): staff opens the "Request info" modal (Pending only). */
  openRequestInfo(): void {
    this.requestInfoModalVisible = true;
  }

  /** Reload after a successful send-back so the status flips to Info Requested. */
  onInfoRequestSucceeded(): void {
    const id = this.appointment?.appointment?.id;
    if (id) {
      this.appointmentService.getWithNavigationProperties(id).subscribe({
        next: (data) => {
          this.appointment = data;
        },
      });
    }
  }

  /** True when staff may send this appointment back for more information (Pending only). */
  get canRequestInfo(): boolean {
    return this.isInternalUser && this.currentStatus === AppointmentStatusType.Pending;
  }

  /**
   * External submissions stay Pending (no `.Approve` permission, so no
   * auto-approve chain). Toast confirmation + refresh so the status pill flips
   * to RescheduleRequested / CancellationRequested.
   */
  onChangeRequestSucceeded(dto: AppointmentChangeRequestDto): void {
    this.toaster.success(
      this.localization.instant(
        dto.changeRequestType === ChangeRequestType.Cancel
          ? '::Appointment:Toast:CancelRequested'
          : '::Appointment:Toast:RescheduleRequested',
      ),
    );
    const id = this.appointment?.appointment?.id;
    if (id) {
      this.appointmentService.getWithNavigationProperties(id).subscribe({
        next: (data) => {
          this.appointment = data;
        },
      });
    }
  }

  save(): Promise<void> {
    return new Promise<void>((resolve, reject) => {
      const selected = this.appointment?.appointment;
      if (
        !selected?.id ||
        !selected.patientId ||
        !selected.identityUserId ||
        !selected.appointmentTypeId ||
        !selected.locationId ||
        !selected.doctorAvailabilityId ||
        !selected.requestConfirmationNumber
      ) {
        this.errorMessage = 'Appointment data is incomplete and cannot be saved.';
        reject(new Error(this.errorMessage));
        return;
      }

      this.errorMessage = '';
      this.successMessage = '';
      this.isSaving = true;

      // #122 (2026-05-14): single source of truth -- getRawValue includes
      // disabled controls so the payload shape stays identical whether or
      // not isReadOnly disabled the form (and the server permission gate
      // is authoritative anyway).
      const raw = this.form.getRawValue();
      const dateOfBirth = this.formatDateOfBirthForApi(raw.patientDateOfBirth);
      const patientPayload: PatientUpdateDto = {
        firstName: raw.patientFirstName,
        lastName: raw.patientLastName,
        middleName: raw.patientMiddleName || undefined,
        email: raw.patientEmail,
        genderId: (raw.patientGenderId as any) ?? undefined,
        dateOfBirth: dateOfBirth ?? undefined,
        phoneNumber: raw.patientPhoneNumber || undefined,
        socialSecurityNumber: raw.patientSocialSecurityNumber || undefined,
        address: raw.patientAddress || undefined,
        city: raw.patientCity || undefined,
        zipCode: raw.patientZipCode || undefined,
        cellPhoneNumber: raw.patientCellPhoneNumber || undefined,
        phoneNumberTypeId: (raw.patientPhoneNumberTypeId as any) ?? undefined,
        street: raw.patientStreet || undefined,
        interpreterVendorName: raw.patientNeedsInterpreter
          ? raw.patientInterpreterVendorName || undefined
          : undefined,
        // S-5.5: send the user-edited Unit # from the form, falling back to the
        // loaded value if the form was untouched (preserves prior preserve-only
        // behavior when the new field has no input).
        apptNumber: raw.patientApptNumber || this.appointment?.patient?.apptNumber || undefined,
        othersLanguageName: this.appointment?.patient?.othersLanguageName ?? undefined,
        stateId: raw.patientStateId ?? undefined,
        appointmentLanguageId: raw.patientAppointmentLanguageId ?? undefined,
        identityUserId: selected.identityUserId,
        tenantId: this.appointment?.patient?.tenantId ?? undefined,
        concurrencyStamp: this.appointment?.patient?.concurrencyStamp,
      };

      const patientId = this.appointment?.patient?.id;
      const updateUrl =
        patientId && this.isExternalUserNonPatient
          ? `/api/app/patients/for-appointment-booking/${patientId}`
          : '/api/app/patients/me';
      this.restService
        .request<any, any>(
          {
            method: 'PUT',
            url: updateUrl,
            body: patientPayload,
          },
          { apiName: 'Default' },
        )
        .subscribe({
          next: (updatedPatient) => {
            // R2 (2026-05-04): the backend AppointmentManager.UpdateAsync
            // accepts ONLY the 14 fields below. isPatientAlreadyExist,
            // internalUserComments, appointmentApproveDate, appointmentStatus
            // are intentionally NOT on AppointmentUpdateDto -- they are set
            // via dedicated transitions (Approve / Reject / SendBack-removed)
            // and the EF entity preserves prior values across this update.
            // Earlier the view sent those fields and TypeScript silently
            // dropped them; the proxy regen now enforces strict shape.
            const payload: AppointmentUpdateDto = {
              panelNumber: raw.panelNumber || undefined,
              appointmentDate: selected.appointmentDate,
              requestConfirmationNumber: selected.requestConfirmationNumber!,
              dueDate: selected.dueDate,
              patientId: selected.patientId!,
              identityUserId: selected.identityUserId!,
              appointmentTypeId: selected.appointmentTypeId!,
              locationId: selected.locationId!,
              doctorAvailabilityId: selected.doctorAvailabilityId!,
              concurrencyStamp: selected.concurrencyStamp ?? '',
              patientEmail: selected.patientEmail,
              applicantAttorneyEmail: selected.applicantAttorneyEmail,
              defenseAttorneyEmail: selected.defenseAttorneyEmail,
              claimExaminerEmail: selected.claimExaminerEmail,
              // 2026-06-09: per-appointment Referred By -- the form loads/saves
              // the appointment's own value (not the patient's).
              refferedBy: raw.patientRefferedBy || undefined,
            };

            this.appointmentService.update(selected.id!, payload).subscribe({
              next: async (updated) => {
                let savedClean = true;
                try {
                  if (this.appointment?.appointment) {
                    this.appointment.appointment = { ...this.appointment.appointment, ...updated };
                  }
                  if (this.appointment?.patient) {
                    this.appointment.patient = { ...this.appointment.patient, ...updatedPatient };
                  }
                  await this.upsertEmployerDetails(updated.id);
                  await this.upsertApplicantAttorneyDetails(updated.id);
                  // S-5.4: persist Defense Attorney edits alongside AA on save.
                  // Claim Information is read-only on the view page (booking
                  // form remains the canonical edit surface), so no inline
                  // upsert call here for injuries.
                  await this.upsertDefenseAttorneyDetails(updated.id);
                  this.form.patchValue(
                    { panelNumber: updated.panelNumber ?? '' },
                    { emitEvent: false },
                  );
                  this.successMessage =
                    'Appointment, patient, employer, applicant attorney, and defense attorney details updated successfully.';
                } catch {
                  this.errorMessage =
                    'Appointment and patient updated, but a downstream save (employer / attorney) failed.';
                  savedClean = false;
                } finally {
                  this.isSaving = false;
                }
                if (savedClean) {
                  resolve();
                } else {
                  reject(new Error(this.errorMessage));
                }
              },
              error: () => {
                this.errorMessage = 'Patient updated, but appointment save failed.';
                this.isSaving = false;
                reject(new Error(this.errorMessage));
              },
            });
          },
          error: () => {
            this.errorMessage = 'Failed to save patient details.';
            this.isSaving = false;
            reject(new Error(this.errorMessage));
          },
        });
    });
  }

  openUploadDocuments(): void {
    // The documents block is already embedded below on this view
    // (<app-appointment-documents id="appointment-documents-anchor">). The
    // button just scrolls it into view so the booker can click the upload
    // input. The earlier '/file-management' target hit Volo's tenant-wide
    // explorer which external roles cannot access (403). The per-appointment
    // upload path (AppointmentDocumentsAppService.UploadStreamAsync) is
    // already permission-granted to the four external roles via
    // BookingBaselineGrants.
    document
      .getElementById('appointment-documents-anchor')
      ?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  openAddAuthorizedUserModal(): void {
    this.authorizedUserModalMode = 'create';
    this.editingAuthorizedUserId = null;
    this.authorizedUserForm.reset({
      identityUserId: null,
      firstName: '',
      lastName: '',
      email: '',
      userRole: '',
      accessTypeId: 23,
    });
    // CREATE: identity fields are free-typed.
    this.authorizedUserForm.get('firstName')?.enable();
    this.authorizedUserForm.get('lastName')?.enable();
    this.authorizedUserForm.get('email')?.enable();
    this.authorizedUserForm.get('userRole')?.enable();
    this.isAuthorizedUserModalOpen = true;
  }

  openEditAuthorizedUserModal(item: AppointmentAuthorizedUserRow): void {
    this.authorizedUserModalMode = 'edit';
    this.editingAuthorizedUserId = item.accessorId;
    this.authorizedUserForm.reset({
      identityUserId: item.identityUserId,
      firstName: item.firstName,
      lastName: item.lastName,
      email: item.email,
      userRole: item.userRole,
      accessTypeId: item.accessTypeId,
    });
    // EDIT: the person is fixed (the update contract changes only the
    // rights); show identity read-only, edit just View/Edit.
    this.authorizedUserForm.get('firstName')?.disable();
    this.authorizedUserForm.get('lastName')?.disable();
    this.authorizedUserForm.get('email')?.disable();
    this.authorizedUserForm.get('userRole')?.disable();
    this.isAuthorizedUserModalOpen = true;
  }

  closeAuthorizedUserModal(): void {
    this.isAuthorizedUserModalOpen = false;
  }

  async saveAuthorizedUserFromModal(): Promise<void> {
    const appointmentId = this.appointment?.appointment?.id;
    if (!appointmentId) {
      return;
    }
    if (this.authorizedUserForm.invalid) {
      this.authorizedUserForm.markAllAsTouched();
      return;
    }

    const draft = this.authorizedUserForm.getRawValue();

    if (this.authorizedUserModalMode === 'edit' && this.editingAuthorizedUserId) {
      // EDIT changes only the rights; the update contract keys by the
      // existing identityUserId.
      await firstValueFrom(
        this.restService.request<any, any>(
          {
            method: 'PUT',
            url: `/api/app/appointment-accessors/${this.editingAuthorizedUserId}`,
            body: {
              appointmentId,
              identityUserId: draft.identityUserId,
              accessTypeId: draft.accessTypeId,
            },
          },
          { apiName: 'Default' },
        ),
      );
    } else {
      const email = (draft.email ?? '').trim();
      // Dedup by email -- the typed email is the accessor's identity key.
      const duplicate = this.appointmentAuthorizedUsers.some(
        (x) => x.email.toLowerCase() === email.toLowerCase(),
      );
      if (duplicate) {
        return;
      }
      // CREATE resolves the email to a user or provisions + invites one.
      await firstValueFrom(
        this.restService.request<any, any>(
          {
            method: 'POST',
            url: '/api/app/appointment-accessors',
            body: {
              appointmentId,
              email,
              firstName: (draft.firstName ?? '').trim() || undefined,
              lastName: (draft.lastName ?? '').trim() || undefined,
              role: draft.userRole,
              accessTypeId: draft.accessTypeId,
            },
          },
          { apiName: 'Default' },
        ),
      );
    }

    this.closeAuthorizedUserModal();
    this.loadAppointmentAccessors(appointmentId);
  }

  async removeAuthorizedUser(item: AppointmentAuthorizedUserRow): Promise<void> {
    if (!item?.accessorId) {
      return;
    }

    await firstValueFrom(
      this.restService.request<any, any>(
        {
          method: 'DELETE',
          url: `/api/app/appointment-accessors/${item.accessorId}`,
        },
        { apiName: 'Default' },
      ),
    );

    this.appointmentAuthorizedUsers = this.appointmentAuthorizedUsers.filter(
      (x) => x.accessorId !== item.accessorId,
    );
  }

  getAccessTypeLabel(value: number): string {
    return this.accessTypeOptions.find((x) => x.value === value)?.label ?? '';
  }

  // Phase 1 / C2 / D4 (2026-06-11): label for the Applicant Attorney picker.
  // Show the resolved display name (First+Last -> FirmName -> email) so a firm
  // account surfaces its firm name; append the email in parens when it differs
  // so staff keep the unique identifier for disambiguation.
  applicantAttorneyOptionLabel(opt: ExternalAuthorizedUserOption): string {
    const display = resolveExternalUserDisplayName(
      opt.firstName,
      opt.lastName,
      opt.firmName,
      opt.email,
    );
    return display && opt.email && display !== opt.email ? `${display} (${opt.email})` : display;
  }

  // 2026-06-22 (HIPAA-scoped lookup): type-to-search the external-user lookup
  // for an Applicant / Defense Attorney, mirroring the patient lookup typeahead.
  // The server scopes results -- internal staff search the tenant, an external
  // caller sees only co-parties on shared appointments -- so no list of every
  // attorney is ever exposed. Selecting a result loads the full record via the
  // existing on*AttorneySelected by-id path.
  readonly searchApplicantAttorney = (
    text$: Observable<string>,
  ): Observable<ExternalAuthorizedUserOption[]> =>
    this.searchExternalAttorney(text$, 'applicant attorney');

  readonly searchDefenseAttorney = (
    text$: Observable<string>,
  ): Observable<ExternalAuthorizedUserOption[]> =>
    this.searchExternalAttorney(text$, 'defense attorney');

  private searchExternalAttorney(
    text$: Observable<string>,
    role: string,
  ): Observable<ExternalAuthorizedUserOption[]> {
    return text$.pipe(
      debounceTime(250),
      distinctUntilChanged(),
      switchMap((term) => {
        const trimmed = (term ?? '').trim();
        if (trimmed.length < 2) {
          return of<ExternalAuthorizedUserOption[]>([]);
        }
        return this.restService
          .request<any, ListResultDto<ExternalAuthorizedUserOption>>(
            {
              method: 'GET',
              url: '/api/public/external-signup/external-user-lookup',
              params: { filter: trimmed },
            },
            { apiName: 'Default' },
          )
          .pipe(
            map((res) => (res?.items ?? []).filter((x) => x.userRole?.toLowerCase() === role)),
            catchError(() => of<ExternalAuthorizedUserOption[]>([])),
          );
      }),
    );
  }

  // Dropdown display label ("Name (email)" / firm fallback). Reuses the shared
  // option label so the typeahead reads the same as the prior select did.
  readonly formatAttorneyResult = (opt: ExternalAuthorizedUserOption): string =>
    this.applicantAttorneyOptionLabel(opt);

  // Keep the typed email in the search box after a pick (the input is bound to
  // the *EmailSearch control); the full record loads via on*AttorneySelected.
  readonly formatAttorneyInput = (opt: ExternalAuthorizedUserOption | string): string =>
    typeof opt === 'string' ? opt : (opt?.email ?? '');

  onApplicantAttorneyTypeaheadSelect(event: NgbTypeaheadSelectItemEvent): void {
    event.preventDefault();
    const opt = event.item as ExternalAuthorizedUserOption;
    this.form.get('applicantAttorneyEmailSearch')?.setValue(opt.email ?? '');
    this.onApplicantAttorneySelected(opt.identityUserId);
  }

  onDefenseAttorneyTypeaheadSelect(event: NgbTypeaheadSelectItemEvent): void {
    event.preventDefault();
    const opt = event.item as ExternalAuthorizedUserOption;
    this.form.get('defenseAttorneyEmailSearch')?.setValue(opt.email ?? '');
    this.onDefenseAttorneySelected(opt.identityUserId);
  }

  private loadExternalAuthorizedUsers(): void {
    this.restService
      .request<any, ListResultDto<ExternalAuthorizedUserOption>>(
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
          // S-5.4: NOTE the lookup endpoint excludes Defense Attorney from
          // `allowedRoleNames` per D-2 (DA does not surface in any picker), so
          // this filter will return an empty array even for tenants that have
          // DAs registered. The view-page DA pre-fill flow relies on the
          // email-search box, not the dropdown; keeping the dropdown bound for
          // consistency with the AA layout.
          this.defenseAttorneyOptions = (result?.items ?? []).filter(
            (x: ExternalAuthorizedUserOption) => x.userRole?.toLowerCase() === 'defense attorney',
          );
          this.refreshAuthorizedUserRoles();
        },
      });
  }

  loadApplicantAttorneyByEmail(): void {
    const email = (this.form.get('applicantAttorneyEmailSearch')?.value as string | null)?.trim();
    if (!email) return;
    this.isApplicantAttorneyLoading = true;
    this.restService
      .request<any, ApplicantAttorneyLookupResult | null>(
        {
          method: 'GET',
          url: '/api/app/appointments/applicant-attorney-details-for-booking',
          params: { email },
        },
        { apiName: 'Default' },
      )
      .subscribe({
        next: (data) => {
          if (data) {
            this.applyApplicantAttorneyLookup(data);
          }
          this.isApplicantAttorneyLoading = false;
        },
        error: () => {
          this.isApplicantAttorneyLoading = false;
        },
      });
  }

  onApplicantAttorneySelected(identityUserId: string | null): void {
    if (!identityUserId) return;
    this.isApplicantAttorneyLoading = true;
    this.restService
      .request<any, ApplicantAttorneyLookupResult | null>(
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
            this.applyApplicantAttorneyLookup(data);
          }
          this.isApplicantAttorneyLoading = false;
        },
        error: () => {
          this.isApplicantAttorneyLoading = false;
        },
      });
  }

  // S-5.4: load DA details by email (used when the form is empty and the
  // viewer types an address into the search box). Mirrors AA's equivalent.
  loadDefenseAttorneyByEmail(): void {
    const email = (this.form.get('defenseAttorneyEmailSearch')?.value as string | null)?.trim();
    if (!email) return;
    this.isDefenseAttorneyLoading = true;
    this.restService
      .request<any, DefenseAttorneyLookupResult | null>(
        {
          method: 'GET',
          url: '/api/app/appointments/defense-attorney-details-for-booking',
          params: { email },
        },
        { apiName: 'Default' },
      )
      .subscribe({
        next: (data) => {
          if (data) {
            this.applyDefenseAttorneyLookup(data);
          }
          this.isDefenseAttorneyLoading = false;
        },
        error: () => {
          this.isDefenseAttorneyLoading = false;
        },
      });
  }

  // S-5.4: load DA details by IdentityUserId (selecting from the dropdown).
  // The dropdown is currently always empty for DA because the lookup endpoint
  // excludes the role per D-2; retained for layout parity with AA.
  onDefenseAttorneySelected(identityUserId: string | null): void {
    if (!identityUserId) return;
    this.isDefenseAttorneyLoading = true;
    this.restService
      .request<any, DefenseAttorneyLookupResult | null>(
        {
          method: 'GET',
          url: '/api/app/appointments/defense-attorney-details-for-booking',
          params: { identityUserId },
        },
        { apiName: 'Default' },
      )
      .subscribe({
        next: (data) => {
          if (data) {
            this.applyDefenseAttorneyLookup(data);
          }
          this.isDefenseAttorneyLoading = false;
        },
        error: () => {
          this.isDefenseAttorneyLoading = false;
        },
      });
  }

  // #122 (2026-05-14): consolidated AA-lookup -> form patch helper.
  // Previously 4 nearly-identical assignment blocks (loadByEmail, onSelected,
  // bindFromResponse, loadForCurrentUser) -- now one helper used by all.
  private applyApplicantAttorneyLookup(data: ApplicantAttorneyLookupResult): void {
    this.applicantAttorneyId = data.applicantAttorneyId ?? null;
    this.form.patchValue(
      {
        applicantAttorneyIdentityUserId: data.identityUserId,
        applicantAttorneyFirstName: data.firstName ?? '',
        applicantAttorneyLastName: data.lastName ?? '',
        applicantAttorneyEmail: data.email ?? '',
        applicantAttorneyFirmName: data.firmName ?? '',
        applicantAttorneyWebAddress: data.webAddress ?? '',
        applicantAttorneyPhoneNumber: data.phoneNumber ?? '',
        applicantAttorneyFaxNumber: data.faxNumber ?? '',
        applicantAttorneyStreet: data.street ?? '',
        applicantAttorneyCity: data.city ?? '',
        applicantAttorneyStateId: data.stateId ?? null,
        applicantAttorneyZipCode: data.zipCode ?? '',
      },
      { emitEvent: false },
    );
  }

  private applyDefenseAttorneyLookup(data: DefenseAttorneyLookupResult): void {
    this.defenseAttorneyId = data.defenseAttorneyId ?? null;
    this.form.patchValue(
      {
        defenseAttorneyIdentityUserId: data.identityUserId,
        defenseAttorneyFirstName: data.firstName ?? '',
        defenseAttorneyLastName: data.lastName ?? '',
        defenseAttorneyEmail: data.email ?? '',
        defenseAttorneyFirmName: data.firmName ?? '',
        defenseAttorneyWebAddress: data.webAddress ?? '',
        defenseAttorneyPhoneNumber: data.phoneNumber ?? '',
        defenseAttorneyFaxNumber: data.faxNumber ?? '',
        defenseAttorneyStreet: data.street ?? '',
        defenseAttorneyCity: data.city ?? '',
        defenseAttorneyStateId: data.stateId ?? null,
        defenseAttorneyZipCode: data.zipCode ?? '',
      },
      { emitEvent: false },
    );
  }

  private loadAppointmentAccessors(appointmentId?: string): void {
    if (!appointmentId) {
      this.appointmentAuthorizedUsers = [];
      return;
    }

    this.restService
      .request<any, PagedResultDto<any>>(
        {
          method: 'GET',
          url: '/api/app/appointment-accessors',
          params: {
            appointmentId,
            skipCount: 0,
            maxResultCount: 100,
          },
        },
        { apiName: 'Default' },
      )
      .subscribe({
        next: (result) => {
          this.appointmentAuthorizedUsers = (result?.items ?? []).map((item) => {
            const accessor = item?.appointmentAccessor;
            const identityUser = item?.identityUser;
            const option = this.externalAuthorizedUserOptions.find(
              (x) => x.identityUserId === accessor?.identityUserId,
            );
            return {
              accessorId: accessor?.id ?? '',
              identityUserId: accessor?.identityUserId ?? '',
              firstName: identityUser?.name ?? option?.firstName ?? '',
              lastName: identityUser?.surname ?? option?.lastName ?? '',
              email: identityUser?.email ?? option?.email ?? '',
              userRole: option?.userRole ?? '',
              accessTypeId: Number(accessor?.accessTypeId ?? 23),
            } as AppointmentAuthorizedUserRow;
          });
        },
      });
  }

  private refreshAuthorizedUserRoles(): void {
    if (
      this.appointmentAuthorizedUsers.length === 0 ||
      this.externalAuthorizedUserOptions.length === 0
    ) {
      return;
    }

    this.appointmentAuthorizedUsers = this.appointmentAuthorizedUsers.map((item) => {
      const option = this.externalAuthorizedUserOptions.find(
        (x) => x.identityUserId === item.identityUserId,
      );
      return option ? { ...item, userRole: option.userRole || item.userRole } : item;
    });
  }

  private bindApplicantAttorneyFromResponse(data: any): void {
    // BUG-042: always load via GET /{id}/applicant-attorney, which returns
    // the authoritative STORED (booked) name -- preferring it over the
    // linked IdentityUser. The previous nav-DTO branch sourced the name
    // from the IdentityUser, so a registered attorney showed their account
    // name instead of the booked name, and an unregistered attorney showed
    // nothing. The endpoint now handles both cases uniformly.
    this.loadApplicantAttorneyDetails(data?.appointment?.id, () => {
      if (this.isApplicantAttorney && !this.form.get('applicantAttorneyIdentityUserId')?.value) {
        this.loadApplicantAttorneyForCurrentUser();
      }
    });
  }

  private loadApplicantAttorneyForCurrentUser(): void {
    const currentUserId = (this.configState.getOne('currentUser') as any)?.id;
    if (!currentUserId) return;
    this.restService
      .request<any, ApplicantAttorneyLookupResult | null>(
        {
          method: 'GET',
          url: '/api/app/appointments/applicant-attorney-details-for-booking',
          params: { identityUserId: currentUserId },
        },
        { apiName: 'Default' },
      )
      .subscribe({
        next: (data) => {
          if (data) {
            this.applyApplicantAttorneyLookup(data);
          }
        },
      });
  }

  private loadApplicantAttorneyDetails(appointmentId?: string, onEmpty?: () => void): void {
    if (!appointmentId) {
      onEmpty?.();
      return;
    }

    this.restService
      .request<any, ApplicantAttorneyLookupResult | null>(
        {
          method: 'GET',
          url: `/api/app/appointments/${appointmentId}/applicant-attorney`,
        },
        { apiName: 'Default' },
      )
      .subscribe({
        next: (data) => {
          if (data) {
            this.applyApplicantAttorneyLookup(data);
            this.overlayApplicantAttorneySnapshot();
          } else {
            onEmpty?.();
          }
        },
        error: () => {
          onEmpty?.();
        },
      });
  }

  // S-5.4: load the DA already linked to this appointment (if any) and seed
  // the form. Endpoint returns null when no DA join row exists for the
  // appointment, in which case the form stays empty and the user can add a DA
  // via the email-search box (mirror of AA bindFromResponse + AA-pre-fill).
  private bindDefenseAttorneyForAppointment(appointmentId?: string): void {
    if (!appointmentId) {
      return;
    }
    this.restService
      .request<any, DefenseAttorneyLookupResult | null>(
        {
          method: 'GET',
          url: `/api/app/appointments/${appointmentId}/defense-attorney`,
        },
        { apiName: 'Default' },
      )
      .subscribe({
        next: (data) => {
          if (data) {
            this.applyDefenseAttorneyLookup(data);
          }
          this.overlayDefenseAttorneySnapshot();
        },
      });
  }

  /**
   * #9 (2026-06-19): display-only override. After the live attorney master loads,
   * overlay the appointment's booking-time snapshot (when present) so the detail shows
   * what was recorded at booking, not a later master self-edit. A null snapshot
   * (pre-migration appointments) leaves the master values in place (fallback). Only the
   * initial display load calls these -- the edit-lookup paths do not -- so picking a
   * different attorney while editing is unaffected. Staff appointment-edit saves still
   * re-capture the snapshot from the master (see AppointmentApplicantAttorneyManager).
   */
  private overlayApplicantAttorneySnapshot(): void {
    const a = this.appointment?.appointment;
    if (!a) {
      return;
    }
    const patch: Record<string, unknown> = {};
    const set = (ctrl: string, val: unknown) => {
      if (val !== null && val !== undefined) {
        patch[ctrl] = val;
      }
    };
    set('applicantAttorneyFirstName', a.applicantAttorneyFirstName);
    set('applicantAttorneyLastName', a.applicantAttorneyLastName);
    set('applicantAttorneyFirmName', a.applicantAttorneyFirmName);
    set('applicantAttorneyWebAddress', a.applicantAttorneyWebAddress);
    set('applicantAttorneyPhoneNumber', a.applicantAttorneyPhoneNumber);
    set('applicantAttorneyFaxNumber', a.applicantAttorneyFaxNumber);
    set('applicantAttorneyStreet', a.applicantAttorneyStreet);
    set('applicantAttorneyCity', a.applicantAttorneyCity);
    set('applicantAttorneyStateId', a.applicantAttorneyStateId);
    set('applicantAttorneyZipCode', a.applicantAttorneyZipCode);
    if (Object.keys(patch).length > 0) {
      this.form.patchValue(patch, { emitEvent: false });
    }
  }

  private overlayDefenseAttorneySnapshot(): void {
    const a = this.appointment?.appointment;
    if (!a) {
      return;
    }
    const patch: Record<string, unknown> = {};
    const set = (ctrl: string, val: unknown) => {
      if (val !== null && val !== undefined) {
        patch[ctrl] = val;
      }
    };
    set('defenseAttorneyFirstName', a.defenseAttorneyFirstName);
    set('defenseAttorneyLastName', a.defenseAttorneyLastName);
    set('defenseAttorneyFirmName', a.defenseAttorneyFirmName);
    set('defenseAttorneyWebAddress', a.defenseAttorneyWebAddress);
    set('defenseAttorneyPhoneNumber', a.defenseAttorneyPhoneNumber);
    set('defenseAttorneyFaxNumber', a.defenseAttorneyFaxNumber);
    set('defenseAttorneyStreet', a.defenseAttorneyStreet);
    set('defenseAttorneyCity', a.defenseAttorneyCity);
    set('defenseAttorneyStateId', a.defenseAttorneyStateId);
    set('defenseAttorneyZipCode', a.defenseAttorneyZipCode);
    if (Object.keys(patch).length > 0) {
      this.form.patchValue(patch, { emitEvent: false });
    }
  }

  // S-5.4: load injury details (Claim Information rows) for the appointment.
  // Read-only at MVP -- the booking form (appointment-add) is the canonical
  // create/edit surface for injuries; the view page just lists them.
  private loadInjuryDetails(appointmentId?: string): void {
    if (!appointmentId) {
      return;
    }
    this.restService
      .request<any, any[]>(
        {
          method: 'GET',
          url: `/api/app/appointment-injury-details/by-appointment/${appointmentId}`,
        },
        { apiName: 'Default' },
      )
      .subscribe({
        next: (items) => {
          this.injuryDetails = (items ?? []).map((item) => {
            const detail = item?.appointmentInjuryDetail;
            const wcabOffice = item?.wcabOffice;
            const claimExaminer = item?.claimExaminer;
            const primaryInsurance = item?.primaryInsurance;
            return {
              id: detail?.id ?? '',
              dateOfInjury: detail?.dateOfInjury ?? null,
              toDateOfInjury: detail?.toDateOfInjury ?? null,
              isCumulativeInjury: !!detail?.isCumulativeInjury,
              claimNumber: detail?.claimNumber ?? '',
              wcabAdj: detail?.wcabAdj ?? '',
              wcabOfficeName: wcabOffice?.displayName ?? wcabOffice?.name ?? '',
              bodyParts: ((item?.bodyParts ?? []) as Array<{ bodyPartDescription?: string }>)
                .map((b) => (b?.bodyPartDescription ?? '').trim())
                .filter((d) => d.length > 0),
              bodyPartsSummary: detail?.bodyPartsSummary ?? '',
              insuranceCompanyName: primaryInsurance?.isActive
                ? (primaryInsurance?.name ?? '')
                : '',
              claimExaminerName: claimExaminer?.isActive ? (claimExaminer?.name ?? '') : '',
            } as AppointmentInjuryDetailRow;
          });
        },
      });
  }

  private async upsertDefenseAttorneyDetails(appointmentId?: string): Promise<void> {
    // S-5.4 mirror of upsertApplicantAttorneyDetails. Bails when no DA is
    // selected (matches the backend's existing silent-bail guard for missing
    // IdentityUserId; the appointment-level DefenseAttorneyEmail column from
    // S-5.1 already captures unregistered DA emails for fan-out independent
    // of whether a join row exists).
    const raw = this.form.getRawValue();
    if (!appointmentId || !raw.defenseAttorneyEnabled || !raw.defenseAttorneyIdentityUserId) {
      return;
    }

    const body = {
      defenseAttorneyId: this.defenseAttorneyId ?? undefined,
      identityUserId: raw.defenseAttorneyIdentityUserId,
      firstName: raw.defenseAttorneyFirstName,
      lastName: raw.defenseAttorneyLastName,
      email: raw.defenseAttorneyEmail,
      firmName: raw.defenseAttorneyFirmName || undefined,
      webAddress: raw.defenseAttorneyWebAddress || undefined,
      phoneNumber: raw.defenseAttorneyPhoneNumber || undefined,
      faxNumber: raw.defenseAttorneyFaxNumber || undefined,
      street: raw.defenseAttorneyStreet || undefined,
      city: raw.defenseAttorneyCity || undefined,
      stateId: raw.defenseAttorneyStateId ?? undefined,
      zipCode: raw.defenseAttorneyZipCode || undefined,
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

  private async upsertApplicantAttorneyDetails(appointmentId?: string): Promise<void> {
    const raw = this.form.getRawValue();
    if (!appointmentId || !raw.applicantAttorneyEnabled || !raw.applicantAttorneyIdentityUserId) {
      return;
    }

    const body = {
      applicantAttorneyId: this.applicantAttorneyId ?? undefined,
      identityUserId: raw.applicantAttorneyIdentityUserId,
      firstName: raw.applicantAttorneyFirstName,
      lastName: raw.applicantAttorneyLastName,
      email: raw.applicantAttorneyEmail,
      firmName: raw.applicantAttorneyFirmName || undefined,
      webAddress: raw.applicantAttorneyWebAddress || undefined,
      phoneNumber: raw.applicantAttorneyPhoneNumber || undefined,
      faxNumber: raw.applicantAttorneyFaxNumber || undefined,
      street: raw.applicantAttorneyStreet || undefined,
      city: raw.applicantAttorneyCity || undefined,
      stateId: raw.applicantAttorneyStateId ?? undefined,
      zipCode: raw.applicantAttorneyZipCode || undefined,
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

  private loadEmployerDetails(appointmentId?: string): void {
    if (!appointmentId) {
      return;
    }

    this.restService
      .request<any, PagedResultDto<any>>(
        {
          method: 'GET',
          url: '/api/app/appointment-employer-details',
          params: {
            appointmentId,
            skipCount: 0,
            maxResultCount: 1,
          },
        },
        { apiName: 'Default' },
      )
      .subscribe((response) => {
        const item = response?.items?.[0];
        const employer = item?.appointmentEmployerDetail;
        if (!employer?.id) {
          return;
        }

        this.employerDetailId = employer.id;
        this.employerDetailConcurrencyStamp = employer.concurrencyStamp ?? null;
        this.form.patchValue(
          {
            employerName: employer.employerName ?? '',
            employerOccupation: employer.occupation ?? '',
            employerPhoneNumber: employer.phoneNumber ?? '',
            employerStreet: employer.street ?? '',
            employerCity: employer.city ?? '',
            employerStateId: employer.stateId ?? null,
            employerZipCode: employer.zipCode ?? '',
          },
          { emitEvent: false },
        );
      });
  }

  private hasEmployerData(): boolean {
    const raw = this.form.getRawValue();
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

  private async upsertEmployerDetails(appointmentId?: string): Promise<void> {
    if (!appointmentId || !this.hasEmployerData()) {
      return;
    }

    const raw = this.form.getRawValue();
    const body = {
      appointmentId,
      employerName: raw.employerName,
      occupation: raw.employerOccupation,
      phoneNumber: raw.employerPhoneNumber || undefined,
      street: raw.employerStreet || undefined,
      city: raw.employerCity || undefined,
      stateId: raw.employerStateId || undefined,
      zipCode: raw.employerZipCode || undefined,
      concurrencyStamp: this.employerDetailConcurrencyStamp ?? undefined,
    };

    if (this.employerDetailId) {
      const updated = await firstValueFrom(
        this.restService.request<any, any>(
          {
            method: 'PUT',
            url: `/api/app/appointment-employer-details/${this.employerDetailId}`,
            body,
          },
          { apiName: 'Default' },
        ),
      );
      this.employerDetailConcurrencyStamp =
        updated?.concurrencyStamp ?? this.employerDetailConcurrencyStamp;
      return;
    }

    if (!raw.employerName || !raw.employerOccupation) {
      return;
    }

    const created = await firstValueFrom(
      this.restService.request<any, any>(
        {
          method: 'POST',
          url: '/api/app/appointment-employer-details',
          body,
        },
        { apiName: 'Default' },
      ),
    );
    this.employerDetailId = created?.id ?? null;
    this.employerDetailConcurrencyStamp = created?.concurrencyStamp ?? null;
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

  // S-5.5: inverse of formatDateOfBirthForApi. The API returns dateOfBirth as
  // an ISO 8601 string (e.g. "1990-05-15T00:00:00"); ngbDatepicker requires
  // an NgbDateStruct ({ year, month, day }) and silently shows blank when given
  // a string. Convert here so the datepicker input populates on appointment load.
  private parseDateOfBirthFromApi(
    value: string | null | undefined,
  ): { year: number; month: number; day: number } | null {
    if (!value) return null;
    const datePart = value.split('T')[0];
    const parts = datePart.split('-');
    if (parts.length !== 3) return null;
    const year = Number(parts[0]);
    const month = Number(parts[1]);
    const day = Number(parts[2]);
    if (!year || !month || !day) return null;
    return { year, month, day };
  }
}
