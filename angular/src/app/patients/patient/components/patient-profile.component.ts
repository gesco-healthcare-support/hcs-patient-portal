import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import {
  AuthService,
  AutofocusDirective,
  ConfigStateService,
  LocalizationPipe,
  RestService,
} from '@abp/ng.core';
import { PageComponent } from '@abp/ng.components/page';
import { DateAdapter, TimeAdapter, ButtonComponent } from '@abp/ng.theme.shared';
import { TopHeaderNavbarComponent } from '../../../shared/components/top-header-navbar/top-header-navbar.component';
import {
  PatientDto,
  PatientUpdateDto,
  PatientWithNavigationPropertiesDto,
} from '../../../proxy/patients/models';
import { genderOptions } from '../../../proxy/enums/gender.enum';
import { phoneNumberTypeOptions } from '../../../proxy/enums/phone-number-type.enum';
import { finalize } from 'rxjs/operators';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import {
  NgbDateAdapter,
  NgbDatepickerModule,
  NgbTimeAdapter,
  NgbNavModule,
} from '@ng-bootstrap/ng-bootstrap';

@Component({
  selector: 'app-patient-profile',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    LocalizationPipe,
    PageComponent,
    TopHeaderNavbarComponent,
    ButtonComponent,
    NgxValidateCoreModule,
    AutofocusDirective,
    NgbDatepickerModule,
    NgbNavModule,
  ],
  providers: [
    { provide: NgbDateAdapter, useClass: DateAdapter },
    { provide: NgbTimeAdapter, useClass: TimeAdapter },
  ],
  templateUrl: './patient-profile.component.html',
  styleUrl: './patient-profile.component.scss',
})
export class PatientProfileComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly configState = inject(ConfigStateService);
  private readonly restService = inject(RestService);
  private readonly authService = inject(AuthService);

  readonly title = '::MyProfile';
  readonly genderOptions = genderOptions;
  readonly phoneNumberTypeOptions = phoneNumberTypeOptions;

  isBusy = false;
  isLoading = true;
  notFound = false;
  selected?: PatientWithNavigationPropertiesDto;

  readonly form = this.fb.group({
    firstName: [null as string | null, [Validators.required, Validators.maxLength(50)]],
    lastName: [null as string | null, [Validators.required, Validators.maxLength(50)]],
    middleName: [null as string | null, [Validators.maxLength(50)]],
    email: [
      null as string | null,
      [Validators.required, Validators.maxLength(50), Validators.email],
    ],
    genderId: [null as number | null, [Validators.required]],
    dateOfBirth: [null as string | null, [Validators.required]],
    phoneNumber: [null as string | null, [Validators.maxLength(20)]],
    socialSecurityNumber: [null as string | null, [Validators.maxLength(20)]],
    address: [null as string | null, [Validators.maxLength(100)]],
    city: [null as string | null, [Validators.maxLength(50)]],
    zipCode: [null as string | null, [Validators.maxLength(15)]],
    refferedBy: [null as string | null, [Validators.maxLength(50)]],
    cellPhoneNumber: [null as string | null, [Validators.maxLength(12)]],
    phoneNumberTypeId: [null as number | null, [Validators.required]],
    street: [null as string | null, [Validators.maxLength(255)]],
    interpreterVendorName: [null as string | null, [Validators.maxLength(255)]],
    apptNumber: [null as string | null, [Validators.maxLength(100)]],
    othersLanguageName: [null as string | null, [Validators.maxLength(100)]],
    stateId: [null as string | null],
    appointmentLanguageId: [null as string | null],
    identityUserId: [null as string | null, [Validators.required]],
    tenantId: [null as string | null],
  });

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

  get isExternalUserNonPatient(): boolean {
    const roles = this.currentUser?.roles ?? [];
    return roles.some(
      (r) => r?.toLowerCase() === 'applicant attorney' || r?.toLowerCase() === 'defense attorney',
    );
  }

  ngOnInit(): void {
    this.loadMyProfile();
  }

  save(): void {
    if (this.isExternalUserNonPatient) {
      return;
    }
    if (this.form.invalid || !this.selected?.patient?.id) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const dateOfBirth = this.formatDateOfBirthForApi(raw.dateOfBirth);

    this.isBusy = true;
    this.restService
      .request<any, PatientDto>(
        {
          method: 'PUT',
          url: '/api/app/patients/me',
          body: {
            ...raw,
            dateOfBirth: dateOfBirth ?? undefined,
            concurrencyStamp: this.selected.patient.concurrencyStamp,
          } as PatientUpdateDto,
        },
        { apiName: 'Default' },
      )
      .pipe(finalize(() => (this.isBusy = false)))
      .subscribe((updated) => {
        if (this.selected?.patient) {
          this.selected.patient = {
            ...this.selected.patient,
            ...updated,
          };
        }
      });
  }

  goBack(): void {
    this.router.navigateByUrl('/');
  }

  openMyProfile(): void {
    this.router.navigateByUrl('/doctor-management/patients/my-profile');
  }

  logout(): void {
    this.authService.logout().subscribe();
  }

  private loadMyProfile(): void {
    if (this.isExternalUserNonPatient) {
      this.restService
        .request<
          any,
          { identityUserId: string; firstName: string; lastName: string; email: string }
        >(
          {
            method: 'GET',
            url: '/api/app/external-users/me',
          },
          { apiName: 'Default' },
        )
        .pipe(finalize(() => (this.isLoading = false)))
        .subscribe((response) => {
          if (!response?.identityUserId) {
            this.notFound = true;
            return;
          }
          this.selected = {
            patient: {
              id: response.identityUserId,
              firstName: response.firstName,
              lastName: response.lastName,
              email: response.email,
              identityUserId: response.identityUserId,
            } as any,
          } as PatientWithNavigationPropertiesDto;
          this.form.patchValue({
            firstName: response.firstName,
            lastName: response.lastName,
            email: response.email,
            identityUserId: response.identityUserId,
          });
        });
    } else {
      this.restService
        .request<any, PatientWithNavigationPropertiesDto>(
          {
            method: 'GET',
            url: '/api/app/patients/me',
          },
          { apiName: 'Default' },
        )
        .pipe(finalize(() => (this.isLoading = false)))
        .subscribe((response) => {
          if (!response?.patient) {
            this.notFound = true;
            return;
          }

          this.selected = response;
          this.form.patchValue({
            ...response.patient,
          });
        });
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
