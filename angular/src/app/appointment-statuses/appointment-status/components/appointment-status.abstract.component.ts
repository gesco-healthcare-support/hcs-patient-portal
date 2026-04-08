import { Directive, OnInit, inject } from '@angular/core';

import { ListService, PermissionService } from '@abp/ng.core';

import type { AppointmentStatusDto } from '../../../proxy/appointment-statuses/models';
import { AppointmentStatusViewService } from '../services/appointment-status.service';
import { AppointmentStatusDetailViewService } from '../services/appointment-status-detail.service';

export const ChildTabDependencies = [];

export const ChildComponentDependencies = [];

@Directive()
export abstract class AbstractAppointmentStatusComponent implements OnInit {
  public readonly list = inject(ListService);
  public readonly service = inject(AppointmentStatusViewService);
  public readonly serviceDetail = inject(AppointmentStatusDetailViewService);
  public readonly permissionService = inject(PermissionService);

  protected title = '::AppointmentStatuses';
  protected isActionButtonVisible: boolean | null = null;

  ngOnInit() {
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

  update(record: AppointmentStatusDto) {
    this.serviceDetail.update(record);
  }

  delete(record: AppointmentStatusDto) {
    this.service.delete(record);
  }

  checkActionButtonVisibility() {
    if (this.isActionButtonVisible !== null) {
      return;
    }

    const canEdit = this.permissionService.getGrantedPolicy(
      'CaseEvaluation.AppointmentStatuses.Edit',
    );
    const canDelete = this.permissionService.getGrantedPolicy(
      'CaseEvaluation.AppointmentStatuses.Delete',
    );
    this.isActionButtonVisible = canEdit || canDelete;
  }
}
