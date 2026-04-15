import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormControl, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  ConfigStateService,
  ListResultDto,
  LocalizationPipe,
  PagedResultDto,
  RestService,
} from '@abp/ng.core';
import type {
  AppointmentUpdateDto,
  AppointmentWithNavigationPropertiesDto,
} from '../../../proxy/appointments/models';
import { genderOptions } from '../../../proxy/enums/gender.enum';
import { phoneNumberTypeOptions } from '../../../proxy/enums/phone-number-type.enum';
import type { PatientUpdateDto } from '../../../proxy/patients/models';
import type { LookupDto, LookupRequestDto } from '../../../proxy/shared/models';
import { AppointmentService } from '../../../proxy/appointments/appointment.service';
import { LookupSelectComponent } from '@volo/abp.commercial.ng.ui';
import { firstValueFrom } from 'rxjs';
import { NgbDatepickerModule } from '@ng-bootstrap/ng-bootstrap';

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

@Component({
  selector: 'app-appointment-view',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    LocalizationPipe,
    LookupSelectComponent,
    NgbDatepickerModule,
  ],
  templateUrl: './appointment-view.component.html',
})
export class AppointmentViewComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly configState = inject(ConfigStateService);
  private readonly appointmentService = inject(AppointmentService);
  private readonly restService = inject(RestService);

  appointment: AppointmentWithNavigationPropertiesDto | null = null;
  isLoading = true;
  isSaving = false;
  errorMessage = '';
  successMessage = '';
  panelNumber = '';
  readonly stateIdControl = new FormControl<string | null>(null);
  readonly employerStateIdControl = new FormControl<string | null>(null);
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
  authorizedUserDraft = {
    identityUserId: null as string | null,
    firstName: '',
    lastName: '',
    email: '',
    userRole: '',
    accessTypeId: 23,
  };
  employerDetailId: string | null = null;
  employerDetailConcurrencyStamp: string | null = null;
  patientForm = {
    firstName: '',
    lastName: '',
    middleName: '',
    email: '',
    genderId: null as number | null,
    dateOfBirth: '' as string | null,
    cellPhoneNumber: '',
    phoneNumber: '',
    phoneNumberTypeId: null as number | null,
    socialSecurityNumber: '',
    street: '',
    address: '',
    city: '',
    stateId: null as string | null,
    zipCode: '',
    appointmentLanguageId: null as string | null,
    needsInterpreter: false,
    interpreterVendorName: '',
    refferedBy: '',
  };
  employerForm = {
    employerName: '',
    occupation: '',
    phoneNumber: '',
    street: '',
    city: '',
    zipCode: '',
  };
  applicantAttorneyEnabled = true;
  applicantAttorneyForm = {
    applicantAttorneyId: null as string | null,
    identityUserId: null as string | null,
    firstName: '',
    lastName: '',
    email: '',
    firmName: '',
    webAddress: '',
    phoneNumber: '',
    faxNumber: '',
    street: '',
    city: '',
    stateId: null as string | null,
    zipCode: '',
  };
  readonly applicantAttorneyStateIdControl = new FormControl<string | null>(null);
  applicantAttorneyEmailSearch = '';
  isApplicantAttorneyLoading = false;
  applicantAttorneyOptions: ExternalAuthorizedUserOption[] = [];

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
        this.panelNumber = data.appointment?.panelNumber ?? '';
        this.loadEmployerDetails(data.appointment?.id);
        this.bindApplicantAttorneyFromResponse(data);
        this.loadAppointmentAccessors(data.appointment?.id);
        const patient = data.patient;
        this.patientForm = {
          firstName: patient?.firstName ?? '',
          lastName: patient?.lastName ?? '',
          middleName: patient?.middleName ?? '',
          email: patient?.email ?? '',
          genderId: (patient?.genderId as number | undefined) ?? null,
          dateOfBirth: patient?.dateOfBirth ?? null,
          cellPhoneNumber: patient?.cellPhoneNumber ?? '',
          phoneNumber: patient?.phoneNumber ?? '',
          phoneNumberTypeId: (patient?.phoneNumberTypeId as number | undefined) ?? null,
          socialSecurityNumber: patient?.socialSecurityNumber ?? '',
          street: patient?.street ?? '',
          address: patient?.address ?? '',
          city: patient?.city ?? '',
          stateId: patient?.stateId ?? null,
          zipCode: patient?.zipCode ?? '',
          appointmentLanguageId: patient?.appointmentLanguageId ?? null,
          needsInterpreter: !!patient?.interpreterVendorName,
          interpreterVendorName: patient?.interpreterVendorName ?? '',
          refferedBy: patient?.refferedBy ?? '',
        };
        this.stateIdControl.setValue(patient?.stateId ?? null, { emitEvent: false });
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

  get isExternalUserNonPatient(): boolean {
    const roles = (this.configState.getOne('currentUser') as any)?.roles ?? [];
    return roles.some(
      (r: string) =>
        r?.toLowerCase() === 'applicant attorney' || r?.toLowerCase() === 'defense attorney',
    );
  }

  get isApplicantAttorney(): boolean {
    const roles = (this.configState.getOne('currentUser') as any)?.roles ?? [];
    return roles.some((r: string) => r?.toLowerCase() === 'applicant attorney');
  }

  save(): void {
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
      return;
    }

    this.errorMessage = '';
    this.successMessage = '';
    this.isSaving = true;

    const dateOfBirth = this.formatDateOfBirthForApi(this.patientForm.dateOfBirth);
    const patientPayload: PatientUpdateDto = {
      firstName: this.patientForm.firstName,
      lastName: this.patientForm.lastName,
      middleName: this.patientForm.middleName || undefined,
      email: this.patientForm.email,
      genderId: (this.patientForm.genderId as any) ?? undefined,
      dateOfBirth: dateOfBirth ?? undefined,
      phoneNumber: this.patientForm.phoneNumber || undefined,
      socialSecurityNumber: this.patientForm.socialSecurityNumber || undefined,
      address: this.patientForm.address || undefined,
      city: this.patientForm.city || undefined,
      zipCode: this.patientForm.zipCode || undefined,
      refferedBy: this.patientForm.refferedBy || undefined,
      cellPhoneNumber: this.patientForm.cellPhoneNumber || undefined,
      phoneNumberTypeId: (this.patientForm.phoneNumberTypeId as any) ?? undefined,
      street: this.patientForm.street || undefined,
      interpreterVendorName: this.patientForm.needsInterpreter
        ? this.patientForm.interpreterVendorName || undefined
        : undefined,
      apptNumber: this.appointment?.patient?.apptNumber ?? undefined,
      othersLanguageName: this.appointment?.patient?.othersLanguageName ?? undefined,
      stateId: this.stateIdControl.value ?? undefined,
      appointmentLanguageId: this.patientForm.appointmentLanguageId ?? undefined,
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
          const payload: AppointmentUpdateDto = {
            panelNumber: this.panelNumber || undefined,
            appointmentDate: selected.appointmentDate,
            isPatientAlreadyExist: selected.isPatientAlreadyExist,
            requestConfirmationNumber: selected.requestConfirmationNumber,
            dueDate: selected.dueDate,
            internalUserComments: selected.internalUserComments,
            appointmentApproveDate: selected.appointmentApproveDate,
            appointmentStatus: selected.appointmentStatus,
            patientId: selected.patientId,
            identityUserId: selected.identityUserId,
            appointmentTypeId: selected.appointmentTypeId,
            locationId: selected.locationId,
            doctorAvailabilityId: selected.doctorAvailabilityId,
            concurrencyStamp: selected.concurrencyStamp,
          };

          this.appointmentService.update(selected.id, payload).subscribe({
            next: async (updated) => {
              try {
                if (this.appointment?.appointment) {
                  this.appointment.appointment = { ...this.appointment.appointment, ...updated };
                }
                if (this.appointment?.patient) {
                  this.appointment.patient = { ...this.appointment.patient, ...updatedPatient };
                }
                await this.upsertEmployerDetails(updated.id);
                await this.upsertApplicantAttorneyDetails(updated.id);
                this.panelNumber = updated.panelNumber ?? '';
                this.successMessage =
                  'Appointment, patient, employer and applicant attorney details updated successfully.';
              } catch {
                this.errorMessage =
                  'Appointment and patient updated, but employer details save failed.';
              } finally {
                this.isSaving = false;
              }
            },
            error: () => {
              this.errorMessage = 'Patient updated, but appointment save failed.';
              this.isSaving = false;
            },
          });
        },
        error: () => {
          this.errorMessage = 'Failed to save patient details.';
          this.isSaving = false;
        },
      });
  }

  openUploadDocuments(): void {
    this.router.navigateByUrl('/file-management');
  }

  openHelp(): void {
    window.open('https://abp.io/docs/latest/getting-started', '_blank');
  }

  openAddAuthorizedUserModal(): void {
    this.authorizedUserModalMode = 'create';
    this.editingAuthorizedUserId = null;
    this.authorizedUserDraft = {
      identityUserId: null,
      firstName: '',
      lastName: '',
      email: '',
      userRole: '',
      accessTypeId: 23,
    };
    this.isAuthorizedUserModalOpen = true;
  }

  openEditAuthorizedUserModal(item: AppointmentAuthorizedUserRow): void {
    this.authorizedUserModalMode = 'edit';
    this.editingAuthorizedUserId = item.accessorId;
    this.authorizedUserDraft = {
      identityUserId: item.identityUserId,
      firstName: item.firstName,
      lastName: item.lastName,
      email: item.email,
      userRole: item.userRole,
      accessTypeId: item.accessTypeId,
    };
    this.isAuthorizedUserModalOpen = true;
  }

  closeAuthorizedUserModal(): void {
    this.isAuthorizedUserModalOpen = false;
  }

  onAuthorizedUserIdentityChange(identityUserId: string | null): void {
    this.authorizedUserDraft.identityUserId = identityUserId;
    const selected = this.externalAuthorizedUserOptions.find(
      (x) => x.identityUserId === identityUserId,
    );
    this.authorizedUserDraft.firstName = selected?.firstName ?? '';
    this.authorizedUserDraft.lastName = selected?.lastName ?? '';
    this.authorizedUserDraft.email = selected?.email ?? '';
    this.authorizedUserDraft.userRole = selected?.userRole ?? '';
  }

  async saveAuthorizedUserFromModal(): Promise<void> {
    const appointmentId = this.appointment?.appointment?.id;
    if (!appointmentId || !this.authorizedUserDraft.identityUserId) {
      return;
    }

    const duplicate = this.appointmentAuthorizedUsers.some(
      (x) =>
        x.identityUserId === this.authorizedUserDraft.identityUserId &&
        x.accessorId !== this.editingAuthorizedUserId,
    );
    if (duplicate) {
      return;
    }

    const body = {
      appointmentId,
      identityUserId: this.authorizedUserDraft.identityUserId,
      accessTypeId: this.authorizedUserDraft.accessTypeId,
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
          this.refreshAuthorizedUserRoles();
        },
      });
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
        } | null
      >(
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
            this.applicantAttorneyForm = {
              applicantAttorneyId: data.applicantAttorneyId ?? null,
              identityUserId: data.identityUserId,
              firstName: data.firstName ?? '',
              lastName: data.lastName ?? '',
              email: data.email ?? '',
              firmName: data.firmName ?? '',
              webAddress: data.webAddress ?? '',
              phoneNumber: data.phoneNumber ?? '',
              faxNumber: data.faxNumber ?? '',
              street: data.street ?? '',
              city: data.city ?? '',
              stateId: data.stateId ?? null,
              zipCode: data.zipCode ?? '',
            };
            this.applicantAttorneyStateIdControl.setValue(data.stateId ?? null, {
              emitEvent: false,
            });
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
            this.applicantAttorneyForm = {
              applicantAttorneyId: data.applicantAttorneyId ?? null,
              identityUserId: data.identityUserId,
              firstName: data.firstName ?? '',
              lastName: data.lastName ?? '',
              email: data.email ?? '',
              firmName: data.firmName ?? '',
              webAddress: data.webAddress ?? '',
              phoneNumber: data.phoneNumber ?? '',
              faxNumber: data.faxNumber ?? '',
              street: data.street ?? '',
              city: data.city ?? '',
              stateId: data.stateId ?? null,
              zipCode: data.zipCode ?? '',
            };
            this.applicantAttorneyStateIdControl.setValue(data.stateId ?? null, {
              emitEvent: false,
            });
          }
          this.isApplicantAttorneyLoading = false;
        },
        error: () => {
          this.isApplicantAttorneyLoading = false;
        },
      });
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
      this.applicantAttorneyForm = {
        applicantAttorneyId: applicant.id ?? null,
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
        stateId: applicant.stateId ?? null,
        zipCode: applicant.zipCode ?? '',
      };
      this.applicantAttorneyStateIdControl.setValue(applicant.stateId ?? null, {
        emitEvent: false,
      });
    } else {
      this.loadApplicantAttorneyDetails(data?.appointment?.id, () => {
        if (this.isApplicantAttorney && !this.applicantAttorneyForm.identityUserId) {
          this.loadApplicantAttorneyForCurrentUser();
        }
      });
    }
  }

  private loadApplicantAttorneyForCurrentUser(): void {
    const currentUserId = (this.configState.getOne('currentUser') as any)?.id;
    if (!currentUserId) return;
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
        } | null
      >(
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
            this.applicantAttorneyForm = {
              applicantAttorneyId: data.applicantAttorneyId ?? null,
              identityUserId: data.identityUserId,
              firstName: data.firstName ?? '',
              lastName: data.lastName ?? '',
              email: data.email ?? '',
              firmName: data.firmName ?? '',
              webAddress: data.webAddress ?? '',
              phoneNumber: data.phoneNumber ?? '',
              faxNumber: data.faxNumber ?? '',
              street: data.street ?? '',
              city: data.city ?? '',
              stateId: data.stateId ?? null,
              zipCode: data.zipCode ?? '',
            };
            this.applicantAttorneyStateIdControl.setValue(data.stateId ?? null, {
              emitEvent: false,
            });
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
        } | null
      >(
        {
          method: 'GET',
          url: `/api/app/appointments/${appointmentId}/applicant-attorney`,
        },
        { apiName: 'Default' },
      )
      .subscribe({
        next: (data) => {
          if (data) {
            this.applicantAttorneyForm = {
              applicantAttorneyId: data.applicantAttorneyId ?? null,
              identityUserId: data.identityUserId,
              firstName: data.firstName ?? '',
              lastName: data.lastName ?? '',
              email: data.email ?? '',
              firmName: data.firmName ?? '',
              webAddress: data.webAddress ?? '',
              phoneNumber: data.phoneNumber ?? '',
              faxNumber: data.faxNumber ?? '',
              street: data.street ?? '',
              city: data.city ?? '',
              stateId: data.stateId ?? null,
              zipCode: data.zipCode ?? '',
            };
            this.applicantAttorneyStateIdControl.setValue(data.stateId ?? null, {
              emitEvent: false,
            });
          } else {
            onEmpty?.();
          }
        },
        error: () => {
          onEmpty?.();
        },
      });
  }

  private async upsertApplicantAttorneyDetails(appointmentId?: string): Promise<void> {
    if (
      !appointmentId ||
      !this.applicantAttorneyEnabled ||
      !this.applicantAttorneyForm.identityUserId
    ) {
      return;
    }

    const body = {
      applicantAttorneyId: this.applicantAttorneyForm.applicantAttorneyId ?? undefined,
      identityUserId: this.applicantAttorneyForm.identityUserId,
      firstName: this.applicantAttorneyForm.firstName,
      lastName: this.applicantAttorneyForm.lastName,
      email: this.applicantAttorneyForm.email,
      firmName: this.applicantAttorneyForm.firmName || undefined,
      webAddress: this.applicantAttorneyForm.webAddress || undefined,
      phoneNumber: this.applicantAttorneyForm.phoneNumber || undefined,
      faxNumber: this.applicantAttorneyForm.faxNumber || undefined,
      street: this.applicantAttorneyForm.street || undefined,
      city: this.applicantAttorneyForm.city || undefined,
      stateId: this.applicantAttorneyStateIdControl.value ?? undefined,
      zipCode: this.applicantAttorneyForm.zipCode || undefined,
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
        this.employerForm = {
          employerName: employer.employerName ?? '',
          occupation: employer.occupation ?? '',
          phoneNumber: employer.phoneNumber ?? '',
          street: employer.street ?? '',
          city: employer.city ?? '',
          zipCode: employer.zipCode ?? '',
        };
        this.employerStateIdControl.setValue(employer.stateId ?? null, { emitEvent: false });
      });
  }

  private hasEmployerData(): boolean {
    return !!(
      this.employerForm.employerName ||
      this.employerForm.occupation ||
      this.employerForm.phoneNumber ||
      this.employerForm.street ||
      this.employerForm.city ||
      this.employerStateIdControl.value ||
      this.employerForm.zipCode
    );
  }

  private async upsertEmployerDetails(appointmentId?: string): Promise<void> {
    if (!appointmentId || !this.hasEmployerData()) {
      return;
    }

    const body = {
      appointmentId,
      employerName: this.employerForm.employerName,
      occupation: this.employerForm.occupation,
      phoneNumber: this.employerForm.phoneNumber || undefined,
      street: this.employerForm.street || undefined,
      city: this.employerForm.city || undefined,
      stateId: this.employerStateIdControl.value || undefined,
      zipCode: this.employerForm.zipCode || undefined,
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

    if (!this.employerForm.employerName || !this.employerForm.occupation) {
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
}
