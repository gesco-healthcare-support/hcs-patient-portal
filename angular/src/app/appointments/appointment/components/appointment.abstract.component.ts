import { Directive, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

import { ListService, PermissionService } from '@abp/ng.core';

import {
  AppointmentStatusType,
  appointmentStatusTypeOptions,
} from '../../../proxy/enums/appointment-status-type.enum';
import type { AppointmentWithNavigationPropertiesDto } from '../../../proxy/appointments/models';
import { AppointmentViewService } from '../services/appointment.service';
import { AppointmentDetailViewService } from '../services/appointment-detail.service';

export const ChildTabDependencies = [];

export const ChildComponentDependencies = [];

@Directive()
export abstract class AbstractAppointmentComponent implements OnInit {
  public readonly list = inject(ListService);
  public readonly service = inject(AppointmentViewService);
  public readonly serviceDetail = inject(AppointmentDetailViewService);
  public readonly permissionService = inject(PermissionService);
  private readonly route = inject(ActivatedRoute);

  protected title = '::Appointments';
  protected isActionButtonVisible: boolean | null = null;

  appointmentStatusTypeOptions = appointmentStatusTypeOptions;

  ngOnInit() {
    // W2-6: dashboard-card deep-link support. /appointments?appointmentStatus=N
    // pre-filters the queue grid before the first page-load query fires.
    // Subscribing here (vs reading the snapshot once) keeps the filter in
    // sync if a future flow updates the URL without a full route change.
    this.route.queryParamMap.subscribe((params) => {
      const raw = params.get('appointmentStatus');
      if (raw !== null && raw !== '') {
        const parsed = Number(raw);
        if (!Number.isNaN(parsed)) {
          this.service.filters.appointmentStatus = parsed as AppointmentStatusType;
        }
      }
    });
    this.service.hookToQuery();
    this.checkActionButtonVisibility();
  }

  clearFilters() {
    this.service.clearFilters();
  }

  showForm() {
    this.serviceDetail.showForm();
  }

  create() {
    this.serviceDetail.selected = undefined;
    this.serviceDetail.showForm();
  }

  update(record: AppointmentWithNavigationPropertiesDto) {
    this.serviceDetail.update(record);
  }

  delete(record: AppointmentWithNavigationPropertiesDto) {
    this.service.delete(record);
  }

  checkActionButtonVisibility() {
    if (this.isActionButtonVisible !== null) {
      return;
    }

    const canEdit = this.permissionService.getGrantedPolicy('CaseEvaluation.Appointments.Edit');
    const canDelete = this.permissionService.getGrantedPolicy('CaseEvaluation.Appointments.Delete');
    // W2-1: Review (read-only drill-down to /appointments/view/:id) is gated
    // by the broader Default permission so tenant admins / office staff who
    // can list appointments can drill in even without Edit/Delete grants.
    const canReview = this.permissionService.getGrantedPolicy('CaseEvaluation.Appointments');
    this.isActionButtonVisible = canEdit || canDelete || canReview;
  }
}
