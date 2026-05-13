import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NgxMaskDirective } from 'ngx-mask';
import {
  ConfigStateService,
  ListResultDto,
  LocalizationPipe,
  PagedResultDto,
  PermissionDirective,
  RestService,
} from '@abp/ng.core';
import type {
  AppointmentDto,
  AppointmentUpdateDto,
  AppointmentWithNavigationPropertiesDto,
} from '../../../proxy/appointments/models';
import { AppointmentStatusType } from '../../../proxy/enums/appointment-status-type.enum';
import { genderOptions } from '../../../proxy/enums/gender.enum';
import { phoneNumberTypeOptions } from '../../../proxy/enums/phone-number-type.enum';
import type { PatientUpdateDto } from '../../../proxy/patients/models';
import type { LookupDto, LookupRequestDto } from '../../../proxy/shared/models';
import { AppointmentService } from '../../../proxy/appointments/appointment.service';
import { LookupSelectComponent } from '@volo/abp.commercial.ng.ui';
import { firstValueFrom } from 'rxjs';
import { NgbDatepickerModule, NgbDateStruct } from '@ng-bootstrap/ng-bootstrap';
import { ApproveConfirmationModalComponent } from './approve-confirmation-modal.component';
import { RejectAppointmentModalComponent } from './reject-appointment-modal.component';
import { AppointmentDocumentsComponent } from '../../../appointment-documents/appointment-documents.component';
import { AppointmentPacketComponent } from '../../../appointment-packet/appointment-packet.component';

type TransitionAction = 'approve' | 'reject';

type ExternalAuthorizedUserOption = {
  identityUserId: string;
  firstName: string;
  lastName: string;
  email: string;
  userRole: string;
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
    LookupSelectComponent,
    NgbDatepickerModule,
    ApproveConfirmationModalComponent,
    RejectAppointmentModalComponent,
    AppointmentDocumentsComponent,
    AppointmentPacketComponent,
    NgxMaskDirective,
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

  // W1-1: state-machine transition UI
  readonly AppointmentStatusType = AppointmentStatusType;
  approveModalVisible = false;
  rejectModalVisible = false;

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
  readonly genderOptions = genderOptions;
  readonly phoneNumberTypeOptions = phoneNumberTypeOptions;
  readonly accessTypeOptions = [
    { value: 23, label: 'View' },
    { value: 24, label: 'Edit' },
  ];
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

  // #122 (2026-05-14): flat + prefixed FormGroup mirrors booker (#121) shape
  // so future shared section components (e.g. <app-patient-demographics>) can
  // drop in once both pages expose the same control surface. Save reads via
  // `this.form.getRawValue()`; loads call `this.form.patchValue({...})`.
  readonly form: FormGroup = this.fb.group({
    // top-level
    panelNumber: [''],
    // patient (19 controls)
    patientFirstName: [''],
    patientLastName: [''],
    patientMiddleName: [''],
    patientEmail: [''],
    patientGenderId: [null as number | null],
    // S-5.5: ngbDatepicker's CVA needs NgbDateStruct; load helper converts
    // ISO -> { year, month, day } on first patch.
    patientDateOfBirth: [null as NgbDateStruct | string | null],
    patientCellPhoneNumber: [''],
    patientPhoneNumber: [''],
    patientPhoneNumberTypeId: [null as number | null],
    patientSocialSecurityNumber: [''],
    patientStreet: [''],
    patientAddress: [''],
    patientApptNumber: [''], // "Unit #" -- view page only field
    patientCity: [''],
    patientStateId: [null as string | null],
    patientZipCode: [''],
    patientAppointmentLanguageId: [null as string | null],
    patientNeedsInterpreter: [false],
    patientInterpreterVendorName: [''],
    patientRefferedBy: [''],
    // employer (7 controls)
    employerName: [''],
    employerOccupation: [''],
    employerPhoneNumber: [''],
    employerStreet: [''],
    employerCity: [''],
    employerStateId: [null as string | null],
    employerZipCode: [''],
    // applicant attorney (14 controls + enabled toggle + email search)
    applicantAttorneyEnabled: [true],
    applicantAttorneyEmailSearch: [''],
    applicantAttorneyIdentityUserId: [null as string | null],
    applicantAttorneyFirstName: [''],
    applicantAttorneyLastName: [''],
    applicantAttorneyEmail: [''],
    applicantAttorneyFirmName: [''],
    applicantAttorneyWebAddress: [''],
    applicantAttorneyPhoneNumber: [''],
    applicantAttorneyFaxNumber: [''],
    applicantAttorneyStreet: [''],
    applicantAttorneyCity: [''],
    applicantAttorneyStateId: [null as string | null],
    applicantAttorneyZipCode: [''],
    // defense attorney (mirror of AA)
    defenseAttorneyEnabled: [true],
    defenseAttorneyEmailSearch: [''],
    defenseAttorneyIdentityUserId: [null as string | null],
    defenseAttorneyFirstName: [''],
    defenseAttorneyLastName: [''],
    defenseAttorneyEmail: [''],
    defenseAttorneyFirmName: [''],
    defenseAttorneyWebAddress: [''],
    defenseAttorneyPhoneNumber: [''],
    defenseAttorneyFaxNumber: [''],
    defenseAttorneyStreet: [''],
    defenseAttorneyCity: [''],
    defenseAttorneyStateId: [null as string | null],
    defenseAttorneyZipCode: [''],
  });

  // #122 (2026-05-14): authorized-user modal sub-form. Kept separate from
  // `form` because it represents draft state for a per-row append/edit
  // operation that submits via its own POST/PUT, not via save().
  readonly authorizedUserForm: FormGroup = this.fb.group({
    identityUserId: [null as string | null],
    firstName: [''],
    lastName: [''],
    email: [''],
    userRole: [''],
    accessTypeId: [23 as number],
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
    this.loadExternalAuthorizedUsers();

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
          { panelNumber: data.appointment?.panelNumber ?? '' },
          { emitEvent: false },
        );
        this.loadEmployerDetails(data.appointment?.id);
        this.bindApplicantAttorneyFromResponse(data);
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
            patientSocialSecurityNumber: patient?.socialSecurityNumber ?? '',
            patientStreet: patient?.street ?? '',
            patientAddress: patient?.address ?? '',
            patientApptNumber: patient?.apptNumber ?? '',
            patientCity: patient?.city ?? '',
            patientStateId: patient?.stateId ?? null,
            patientZipCode: patient?.zipCode ?? '',
            patientAppointmentLanguageId: patient?.appointmentLanguageId ?? null,
            patientNeedsInterpreter: !!patient?.interpreterVendorName,
            patientInterpreterVendorName: patient?.interpreterVendorName ?? '',
            patientRefferedBy: patient?.refferedBy ?? '',
          },
          { emitEvent: false },
        );
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

  goBack(): void {
    this.router.navigateByUrl('/');
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
      !this.isPatientUser && status === AppointmentStatusType.Pending
    );
  }

  /**
   * Action keys the office can pick at the current status.
   *  Pending: approve | reject  (OLD parity -- no send-back path)
   */
  get availableActions(): TransitionAction[] {
    const status = this.currentStatus;
    if (status === AppointmentStatusType.Pending) {
      return ['approve', 'reject'];
    }
    return [];
  }

  /** Triggered when the office clicks Approve or Reject in the toolbar. */
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
        refferedBy: raw.patientRefferedBy || undefined,
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
    this.isAuthorizedUserModalOpen = true;
  }

  closeAuthorizedUserModal(): void {
    this.isAuthorizedUserModalOpen = false;
  }

  onAuthorizedUserIdentityChange(): void {
    const identityUserId = this.authorizedUserForm.get('identityUserId')?.value as string | null;
    const selected = this.externalAuthorizedUserOptions.find(
      (x) => x.identityUserId === identityUserId,
    );
    this.authorizedUserForm.patchValue(
      {
        firstName: selected?.firstName ?? '',
        lastName: selected?.lastName ?? '',
        email: selected?.email ?? '',
        userRole: selected?.userRole ?? '',
      },
      { emitEvent: false },
    );
  }

  async saveAuthorizedUserFromModal(): Promise<void> {
    const appointmentId = this.appointment?.appointment?.id;
    const draft = this.authorizedUserForm.getRawValue();
    if (!appointmentId || !draft.identityUserId) {
      return;
    }

    const duplicate = this.appointmentAuthorizedUsers.some(
      (x) =>
        x.identityUserId === draft.identityUserId && x.accessorId !== this.editingAuthorizedUserId,
    );
    if (duplicate) {
      return;
    }

    const body = {
      appointmentId,
      identityUserId: draft.identityUserId,
      accessTypeId: draft.accessTypeId,
    };

    if (this.authorizedUserModalMode === 'edit' && this.editingAuthorizedUserId) {
      await firstValueFrom(
        this.restService.request<any, any>(
          {
            method: 'PUT',
            url: `/api/app/appointment-accessors/${this.editingAuthorizedUserId}`,
            body,
          },
          { apiName: 'Default' },
        ),
      );
    } else {
      await firstValueFrom(
        this.restService.request<any, any>(
          {
            method: 'POST',
            url: '/api/app/appointment-accessors',
            body,
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
    const appAtt = data?.appointmentApplicantAttorney;
    const applicant = appAtt?.applicantAttorney;
    const identityUser = appAtt?.identityUser;
    if (applicant && identityUser) {
      this.applyApplicantAttorneyLookup({
        applicantAttorneyId: applicant.id ?? undefined,
        identityUserId: identityUser.id ?? '',
        firstName: identityUser.name ?? '',
        lastName: identityUser.surname ?? '',
        email: identityUser.email ?? '',
        firmName: applicant.firmName ?? '',
        webAddress: applicant.webAddress ?? '',
        phoneNumber: applicant.phoneNumber ?? '',
        faxNumber: applicant.faxNumber ?? '',
        street: applicant.street ?? '',
        city: applicant.city ?? '',
        stateId: applicant.stateId ?? undefined,
        zipCode: applicant.zipCode ?? '',
      });
    } else {
      this.loadApplicantAttorneyDetails(data?.appointment?.id, () => {
        if (this.isApplicantAttorney && !this.form.get('applicantAttorneyIdentityUserId')?.value) {
          this.loadApplicantAttorneyForCurrentUser();
        }
      });
    }
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
        },
      });
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
