import { Component, OnInit, inject } from '@angular/core';
import { AuthService, ConfigStateService, ListService, LocalizationPipe } from '@abp/ng.core';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';
import { Router } from '@angular/router';
import { PageComponent } from '@abp/ng.components/page';
import { TopHeaderNavbarComponent } from '../shared/components/top-header-navbar/top-header-navbar.component';
import { NgxDatatableDefaultDirective, NgxDatatableListDirective } from '@abp/ng.theme.shared';
import { AppointmentViewService } from '../appointments/appointment/services/appointment.service';
import { AppointmentDetailViewService } from '../appointments/appointment/services/appointment-detail.service';

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
    TopHeaderNavbarComponent],
    providers: [
      ListService,
      AppointmentViewService,
      AppointmentDetailViewService,
    ]
})
export class HomeComponent implements OnInit {
  private authService = inject(AuthService);
  private configState = inject(ConfigStateService);
  protected list = inject(ListService);
  protected service = inject(AppointmentViewService);
  private router = inject(Router);
  patientAppointmentRows: any[] = [];
  readonly patientDatatableMessages = {
    emptyMessage: 'No Data Available',
  };

  ngOnInit(): void {
    if (!this.isPatientUser) {
      return;
    }

    const currentUserId = this.currentUser?.id;
    if (currentUserId) {
      if (this.isAttorneyUser) {
        this.service.filters.accessorIdentityUserId = currentUserId as any;
      } else {
        this.service.filters.identityUserId = currentUserId as any;
      }
    }

    this.service.hookToQuery();
  }

  /** Applicant Attorney or Defense Attorney (use AppointmentAccessor filter, not Patient). */
  private get isAttorneyUser(): boolean {
    const roles = this.currentUser?.roles ?? [];
    const attorneyRoles = ['applicant attorney', 'defense attorney'];
    return roles.some(role => attorneyRoles.includes(role?.toLowerCase() ?? ''));
  }

  get hasLoggedIn(): boolean {
    return this.authService.isAuthenticated;
  }

  /** Patient, Applicant Attorney, and Defense Attorney share the same layout. */
  get isPatientUser(): boolean {
    if (!this.hasLoggedIn) {
      return false;
    }

    const roles = this.currentUser?.roles ?? [];
    const externalUserRoles = ['patient', 'applicant attorney', 'defense attorney'];
    return roles.some(role => externalUserRoles.includes(role?.toLowerCase() ?? ''));
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
