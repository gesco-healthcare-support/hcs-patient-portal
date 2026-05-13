import { Component, OnInit, inject } from '@angular/core';
import {
  AuthService,
  ConfigStateService,
  ListService,
  LocalizationPipe,
  RestService,
} from '@abp/ng.core';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';
import { Router } from '@angular/router';
import { PageComponent } from '@abp/ng.components/page';
import { TopHeaderNavbarComponent } from '../shared/components/top-header-navbar/top-header-navbar.component';
import { NgxDatatableDefaultDirective, NgxDatatableListDirective } from '@abp/ng.theme.shared';
import { FormsModule, ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { AppointmentViewService } from '../appointments/appointment/services/appointment.service';
import { AppointmentDetailViewService } from '../appointments/appointment/services/appointment-detail.service';
import { AppointmentStatusType } from '../proxy/enums/appointment-status-type.enum';

@Component({
  selector: 'app-home',
  standalone: true,
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss'],
  imports: [
    LocalizationPipe,
    NgxDatatableModule,
    NgxDatatableDefaultDirective,
    NgxDatatableListDirective,
    PageComponent,
    TopHeaderNavbarComponent,
    FormsModule,
    ReactiveFormsModule,
  ],
  providers: [ListService, AppointmentViewService, AppointmentDetailViewService],
})
export class HomeComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly configState = inject(ConfigStateService);
  private readonly restService = inject(RestService);
  private readonly fb = inject(FormBuilder);
  protected list = inject(ListService);
  protected service = inject(AppointmentViewService);
  private readonly router = inject(Router);
  patientAppointmentRows: any[] = [];
  readonly patientDatatableMessages = {
    emptyMessage: 'No Data Available',
  };

  // Quick + Advanced Search (OLD parity)
  quickSearchText = '';
  advancedSearchOpen = false;
  advancedSearchForm = this.fb.group({
    appointmentTypeId: [null as string | null],
    confirmationNumber: [null as string | null],
    locationId: [null as string | null],
    appointmentStatus: [null as AppointmentStatusType | null],
    claimNumber: [null as string | null],
    dateOfInjury: [null as string | null],
    dateOfBirth: [null as string | null],
    socialSecurityNumber: [null as string | null],
  });

  advancedAppointmentTypeOptions: { id: string; displayName: string }[] = [];
  advancedLocationOptions: { id: string; displayName: string }[] = [];
  advancedAppointmentStatusOptions = [
    { value: AppointmentStatusType.Pending, label: 'Pending' },
    { value: AppointmentStatusType.Approved, label: 'Approved' },
    { value: AppointmentStatusType.Rejected, label: 'Rejected' },
    { value: AppointmentStatusType.NoShow, label: 'No Show' },
    { value: AppointmentStatusType.CancelledNoBill, label: 'Cancelled-NoBill' },
    { value: AppointmentStatusType.CancelledLate, label: 'Cancelled-Late' },
    { value: AppointmentStatusType.RescheduledNoBill, label: 'Rescheduled-NoBill' },
    { value: AppointmentStatusType.RescheduledLate, label: 'Rescheduled-Late' },
    { value: AppointmentStatusType.CheckedIn, label: 'Checked-In' },
    { value: AppointmentStatusType.CheckedOut, label: 'Checked-Out' },
    { value: AppointmentStatusType.Billed, label: 'Billed' },
  ];

  get isPatientRole(): boolean {
    const roles = this.currentUser?.roles ?? [];
    return roles.some((r) => r?.toLowerCase() === 'patient');
  }

  ngOnInit(): void {
    // Issue 1.1 (2026-05-12): anonymous redirect now lives in the
    // CanMatchFn at app.routes.ts ':' route (see
    // shared/auth/post-login-redirect.guard.ts). The CanMatchFn runs
    // BEFORE this chunk downloads, so by the time we get here the user
    // is authenticated. The prior in-component redirect (which caused
    // the empty-home-shell flash for anon users) has been removed.
    if (!this.isPatientUser) {
      return;
    }

    // S-NEW-2 (Adrian 2026-04-30): the server narrows the appointment list to
    // appointments where the caller is involved (booker / patient / AA link /
    // DA link / CE email) when the caller holds an external role. We do NOT
    // pre-set client-side filters here -- doing so previously restricted
    // attorneys to "I am the booker" or "I am on the AppointmentAccessor",
    // which missed the natural cases where a Patient enters their AA's email
    // on the booking form (link row created without that AA being the
    // booker). Letting the server's S-NEW-2 visibility filter run unfiltered
    // returns the union of all involvement modes.
    this.service.hookToQuery();
    this.loadAdvancedSearchLookups();
  }

  applyQuickSearch(): void {
    this.service.filters = {
      ...this.service.filters,
      filterText: this.quickSearchText || null,
    };
    this.list.get();
  }

  resetQuickSearch(): void {
    this.quickSearchText = '';
    this.applyQuickSearch();
  }

  applyAdvancedSearch(): void {
    const v = this.advancedSearchForm.value;
    this.service.filters = {
      ...this.service.filters,
      filterText: v.confirmationNumber || this.quickSearchText || null,
      appointmentTypeId: v.appointmentTypeId || null,
      locationId: v.locationId || null,
      appointmentStatus: v.appointmentStatus ?? null,
    };
    this.list.get();
  }

  resetAdvancedSearch(): void {
    this.advancedSearchForm.reset();
    this.service.filters = {} as any;
    this.applyQuickSearch();
  }

  private loadAdvancedSearchLookups(): void {
    this.restService
      .request<any, { items: { id: string; displayName: string }[] }>(
        {
          method: 'GET',
          url: '/api/app/appointments/appointment-type-lookup',
          params: { skipCount: 0, maxResultCount: 200 },
        },
        { apiName: 'Default' },
      )
      .subscribe({
        next: (res) => (this.advancedAppointmentTypeOptions = res?.items ?? []),
        error: () => (this.advancedAppointmentTypeOptions = []),
      });
    this.restService
      .request<any, { items: { id: string; displayName: string }[] }>(
        {
          method: 'GET',
          url: '/api/app/appointments/location-lookup',
          params: { skipCount: 0, maxResultCount: 200 },
        },
        { apiName: 'Default' },
      )
      .subscribe({
        next: (res) => (this.advancedLocationOptions = res?.items ?? []),
        error: () => (this.advancedLocationOptions = []),
      });
  }

  get hasLoggedIn(): boolean {
    return this.authService.isAuthenticated;
  }

  /** Patient, Applicant Attorney, Defense Attorney, and Claim Examiner share the same layout. */
  get isPatientUser(): boolean {
    if (!this.hasLoggedIn) {
      return false;
    }

    const roles = this.currentUser?.roles ?? [];
    const externalUserRoles = new Set([
      'patient',
      'applicant attorney',
      'defense attorney',
      'claim examiner',
    ]);
    return roles.some((role) => externalUserRoles.has(role?.toLowerCase() ?? ''));
  }

  get displayUserName(): string {
    const user = this.currentUser;
    if (!user) {
      return '';
    }

    const fullName = [user.name, user.surname].filter(Boolean).join(' ').trim();
    return fullName || user.userName || '';
  }

  get displayTenantName(): string {
    const tenant = this.currentTenant;
    return tenant?.name || tenant?.tenantName || 'Tenant';
  }

  get displayRoleName(): string {
    const role = this.currentUser?.roles?.[0];
    return role || 'Patient';
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

  login() {
    this.authService.navigateToLogin();
  }

  bookAppointment() {
    this.router.navigateByUrl('/appointments/add?type=1');
  }

  /** W2-2: Re-evaluation flow uses appointment-add with type=2; same form, different downstream filtering. */
  bookReEvaluation() {
    this.router.navigateByUrl('/appointments/add?type=2');
  }

  openAppointmentDetail(appointmentId?: string): void {
    if (!appointmentId) {
      return;
    }

    this.router.navigate(['/appointments/view', appointmentId]);
  }

  openMyProfile() {
    this.router.navigateByUrl('/doctor-management/patients/my-profile');
  }
}
