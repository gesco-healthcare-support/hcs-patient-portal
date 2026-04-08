import { Directive, OnInit, inject } from '@angular/core';

import { ListService, PermissionService } from '@abp/ng.core';

import type { AppointmentTypeDto } from '../../../proxy/appointment-types/models';
import { AppointmentTypeViewService } from '../services/appointment-type.service';
import { AppointmentTypeDetailViewService } from '../services/appointment-type-detail.service';

export const ChildTabDependencies = [];

export const ChildComponentDependencies = [];

@Directive()
export abstract class AbstractAppointmentTypeComponent implements OnInit {
  public readonly list = inject(ListService);
  public readonly service = inject(AppointmentTypeViewService);
  public readonly serviceDetail = inject(AppointmentTypeDetailViewService);
  public readonly permissionService = inject(PermissionService);

  protected title = '::AppointmentTypes';
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

  update(record: AppointmentTypeDto) {
    this.serviceDetail.update(record);
  }

  delete(record: AppointmentTypeDto) {
    this.service.delete(record);
  }

  checkActionButtonVisibility() {
    if (this.isActionButtonVisible !== null) {
      return;
    }

    const canEdit = this.permissionService.getGrantedPolicy('CaseEvaluation.AppointmentTypes.Edit');
    const canDelete = this.permissionService.getGrantedPolicy(
      'CaseEvaluation.AppointmentTypes.Delete',
    );
    this.isActionButtonVisible = canEdit || canDelete;
  }
}
