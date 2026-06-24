import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import {
  NgbDateAdapter,
  NgbTimeAdapter,
  NgbCollapseModule,
  NgbDatepickerModule,
  NgbTimepickerModule,
  NgbDropdownModule,
} from '@ng-bootstrap/ng-bootstrap';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';
import { ListService, LocalizationPipe, PermissionDirective, UtcToLocalPipe } from '@abp/ng.core';
import {
  DateAdapter,
  TimeAdapter,
  NgxDatatableDefaultDirective,
  NgxDatatableListDirective,
} from '@abp/ng.theme.shared';
import { PageComponent, PageToolbarContainerComponent } from '@abp/ng.components/page';
import {
  AdvancedEntityFiltersComponent,
  AdvancedEntityFiltersFormComponent,
  LookupSelectComponent,
} from '@volo/abp.commercial.ng.ui';
import { AppointmentStatusType } from '../../../proxy/enums/appointment-status-type.enum';
import type { AppointmentWithNavigationPropertiesDto } from '../../../proxy/appointments/models';
import type { AppointmentChangeRequestDto } from '../../../proxy/appointment-change-requests/models';
import { ChangeRequestType } from '../../../proxy/appointment-change-requests/change-request-type.enum';
import { AppointmentChangeRequestApprovalService } from '../../../proxy/appointment-change-requests/appointment-change-request-approval.service';
import { AppointmentViewService } from '../services/appointment.service';
import { AppointmentDetailViewService } from '../services/appointment-detail.service';
import { AppointmentDetailModalComponent } from './appointment-detail.component';
import { RescheduleRequestModalComponent } from './reschedule-request-modal.component';
import { CancellationRequestModalComponent } from './cancellation-request-modal.component';
import { planAutoApprove } from './change-request-auto-approve';
import {
  AbstractAppointmentComponent,
  ChildTabDependencies,
  ChildComponentDependencies,
} from './appointment.abstract.component';

@Component({
  selector: 'app-appointment',
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [
    ...ChildTabDependencies,
    NgbCollapseModule,
    NgbDatepickerModule,
    NgbTimepickerModule,
    NgbDropdownModule,
    NgxValidateCoreModule,
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterLink,
    NgxDatatableModule,
    NgxDatatableDefaultDirective,
    NgxDatatableListDirective,
    PermissionDirective,
    LocalizationPipe,
    PageComponent,
    PageToolbarContainerComponent,
    AdvancedEntityFiltersComponent,
    AdvancedEntityFiltersFormComponent,
    LookupSelectComponent,

    UtcToLocalPipe,
    AppointmentDetailModalComponent,
    RescheduleRequestModalComponent,
    CancellationRequestModalComponent,
    ...ChildComponentDependencies,
  ],
  providers: [
    ListService,
    AppointmentViewService,
    AppointmentDetailViewService,
    { provide: NgbDateAdapter, useClass: DateAdapter },
    { provide: NgbTimeAdapter, useClass: TimeAdapter },
  ],
  templateUrl: './appointment.component.html',
  styles: `
    ::ng-deep.datatable-row-detail {
      background: transparent !important;
    }
  `,
})
export class AppointmentComponent extends AbstractAppointmentComponent {
  private readonly approvalService = inject(AppointmentChangeRequestApprovalService);
  private readonly toaster = inject(ToasterService);
  private readonly localization = inject(LocalizationService);

  readonly approvedStatus = AppointmentStatusType.Approved;
  readonly pendingStatus = AppointmentStatusType.Pending;

  // 2026-06-11: decision-SLA badge. Staff have 3 days (kept below the legal
  // 5-day limit for safety) to decide a Pending request. Hardcoded client-side
  // to match the server default (SystemParameter.PendingAppointmentOverDueNotificationDays);
  // the dashboard tile + daily digest are the server-authoritative surfaces.
  private readonly decisionDueDays = 3;

  rescheduleVisible = false;
  cancelVisible = false;
  selectedRow: AppointmentWithNavigationPropertiesDto | null = null;

  /**
   * For a Pending row, the decision-deadline countdown shown next to the
   * status: "Decision due in N day(s)", "Decision due today", or "OVERDUE".
   * Returns null for non-Pending rows or when CreationTime is missing.
   */
  decisionBadge(
    row: AppointmentWithNavigationPropertiesDto,
  ): { text: string; overdue: boolean } | null {
    if (row.appointment?.appointmentStatus !== this.pendingStatus) {
      return null;
    }
    const created = row.appointment?.creationTime;
    if (!created) {
      return null;
    }
    const requested = new Date(created);
    const dueDate = new Date(
      requested.getFullYear(),
      requested.getMonth(),
      requested.getDate() + this.decisionDueDays,
    );
    const today = new Date();
    const todayMidnight = new Date(today.getFullYear(), today.getMonth(), today.getDate());
    const msPerDay = 24 * 60 * 60 * 1000;
    const daysLeft = Math.round((dueDate.getTime() - todayMidnight.getTime()) / msPerDay);
    if (daysLeft < 0) {
      return { text: 'OVERDUE', overdue: true };
    }
    if (daysLeft === 0) {
      return { text: 'Decision due today', overdue: false };
    }
    return {
      text: `Decision due in ${daysLeft} day${daysLeft === 1 ? '' : 's'}`,
      overdue: false,
    };
  }

  openReschedule(row: AppointmentWithNavigationPropertiesDto): void {
    this.selectedRow = row;
    this.cancelVisible = false;
    this.rescheduleVisible = true;
  }

  openCancel(row: AppointmentWithNavigationPropertiesDto): void {
    this.selectedRow = row;
    this.rescheduleVisible = false;
    this.cancelVisible = true;
  }

  /**
   * AP1 (decision 2): Angular orchestrates request + auto-approve. The submit
   * already happened in the modal; if the caller holds the Approve permission
   * (internal staff), chain the approval immediately with the NoBill outcome.
   * Otherwise the request stays Pending for the supervisor queue. If the chained
   * approve fails, the request degrades to Pending (recoverable) and ABP shows
   * the error toast.
   */
  onChangeRequestSucceeded(dto: AppointmentChangeRequestDto): void {
    const canApprove = this.permissionService.getGrantedPolicy(
      'CaseEvaluation.AppointmentChangeRequests.Approve',
    );
    const plan = planAutoApprove(dto.changeRequestType, canApprove);

    if (!plan || !dto.id) {
      this.toaster.success(
        this.localization.instant(
          dto.changeRequestType === ChangeRequestType.Cancel
            ? '::Appointment:Toast:CancelRequested'
            : '::Appointment:Toast:RescheduleRequested',
        ),
      );
      this.list.get();
      return;
    }

    const approve$ =
      plan.kind === 'reschedule'
        ? this.approvalService.approveReschedule(dto.id, { rescheduleOutcome: plan.outcome })
        : this.approvalService.approveCancellation(dto.id, { cancellationOutcome: plan.outcome });

    approve$.subscribe({
      next: () => {
        this.toaster.success(
          this.localization.instant(
            plan.kind === 'reschedule'
              ? '::Appointment:Toast:RescheduleApproved'
              : '::Appointment:Toast:CancelApproved',
          ),
        );
        this.list.get();
      },
      error: () => {
        this.list.get();
      },
    });
  }
}
