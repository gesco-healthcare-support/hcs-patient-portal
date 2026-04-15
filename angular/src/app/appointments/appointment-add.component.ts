import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
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
import { firstValueFrom, of } from 'rxjs';
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
  private readonly configState = inject(ConfigStateService);
  private readonly restService = inject(RestService);

  activeTabId = 'appointment';
  isSaving = false;
  isProfileLoading = true;
  patientLabel = '';
  patientLoadMessage = '';
  isLocationSelected = false;
  checkForAppointmentTypeSelected = false;
  isAvailableDatesLoading = false;
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
    email: [
      { value: null as string | null, disabled: true },
      [Validators.required, Validators.maxLength(50), Validators.email],
    ],
    genderId: [null as number | null],
    dateOfBirth: [null as string | null],
    cellPhoneNumber: [null as string | null, [Validators.maxLength(12)]],
    phoneNumber: [null as string | null, [Validators.maxLength(20)]],
    phoneNumberTypeId: [null as number | null],
    socialSecurityNumber: [null as string | null, [Validators.maxLength(20)]],
    street: [null as string | null, [Validators.maxLength(255)]],
    address: [null as string | null, [Validators.maxLength(100)]],
    city: [null as string | null, [Validators.maxLength(50)]],
    stateId: [null as string | null],
    zipCode: [null as string | null, [Validators.maxLength(15)]],
    appointmentLanguageId: [null as string | null],
    needsInterpreter: [null as boolean | null],
    interpreterVendorName: [null as string | null, [Validators.maxLength(255)]],
    refferedBy: [null as string | null, [Validators.maxLength(50)]],
    employerName: [null as string | null, [Validators.maxLength(255)]],
    employerOccupation: [null as string | null, [Validators.maxLength(255)]],
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
  });

  constructor() {
    this.form
      .get('locationId')
      ?.valueChanges.subscribe((locationId) => this.updateLocationSelection(locationId));
    this.form.get('locationId')?.valueChanges.subscribe(() => this.loadAvailableDatesBySelection());
    this.form
      .get('appointmentTypeId')
      ?.valueChanges.subscribe(() => this.loadAvailableDatesBySelection());
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

  /** True when user is Applicant Attorney or Defense Attorney (external user without Patient record). */
  get isExternalUserNonPatient(): boolean {
    const roles = this.currentUser?.roles ?? [];
    return roles.some(
      (r) => r?.toLowerCase() === 'applicant attorney' || r?.toLowerCase() === 'defense attorney',
    );
  }

  /** True when current user is Applicant Attorney (hide load/select UI for them). */
  get isApplicantAttorney(): boolean {
    const roles = this.currentUser?.roles ?? [];
    return roles.some((r: string) => r?.toLowerCase() === 'applicant attorney');
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
        patientId: rawAfter.patientId as string,
        identityUserId: rawAfter.identityUserId as string,
        appointmentTypeId: rawAfter.appointmentTypeId as string,
        locationId: rawAfter.locationId as string,
        doctorAvailabilityId: rawAfter.doctorAvailabilityId as string,
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
        this.form.get('email')?.enable();
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
          dateOfBirth: patient.dateOfBirth ?? null,
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
      genderId: (raw.genderId as number) ?? 0,
      dateOfBirth,
      phoneNumberTypeId: (raw.phoneNumberTypeId as number) ?? 1,
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
          dateOfBirth: profile.patient.dateOfBirth ?? null,
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
          dateOfBirth: patient.dateOfBirth ?? null,
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
    const identityUserId = raw.identityUserId as string;
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
    if (!appointmentId || !raw.applicantAttorneyEnabled || !raw.applicantAttorneyIdentityUserId) {
      return;
    }
    const body = {
      applicantAttorneyId: this.applicantAttorneyId ?? undefined,
      identityUserId: raw.applicantAttorneyIdentityUserId,
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

    if (!this.isLocationSelected) {
      this.form.patchValue({
        appointmentDate: null,
        appointmentTime: null,
        doctorAvailabilityId: null,
      });
      this.form.get('appointmentDate')?.clearValidators();
    } else {
      this.form.get('appointmentDate')?.setValidators([Validators.required]);
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

    this.fetchAllAvailableSlots(locationId, appointmentTypeId)
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

    console.log('Date check:', {
      selected: selected.toISOString(),
      threshold: threshold.toISOString(),
      isBefore: selected < threshold,
    });

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
}
