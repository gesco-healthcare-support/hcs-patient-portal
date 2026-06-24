import { Directive, OnInit, inject } from '@angular/core';

import { ListService, PermissionService } from '@abp/ng.core';

import type { AppointmentDocumentTypeDto } from '../../../proxy/appointment-document-types/models';
import { AppointmentDocumentTypeViewService } from '../services/appointment-document-type.service';
import { AppointmentDocumentTypeDetailViewService } from '../services/appointment-document-type-detail.service';

export const ChildTabDependencies = [];

export const ChildComponentDependencies = [];

@Directive()
export abstract class AbstractAppointmentDocumentTypeComponent implements OnInit {
  public readonly list = inject(ListService);
  public readonly service = inject(AppointmentDocumentTypeViewService);
  public readonly serviceDetail = inject(AppointmentDocumentTypeDetailViewService);
  public readonly permissionService = inject(PermissionService);

  protected title = '::AppointmentDocumentTypes';
  protected isActionButtonVisible: boolean | null = null;

  ngOnInit() {
    this.service.hookToQuery();
    this.service.loadAppointmentTypes();
    this.serviceDetail.loadAppointmentTypes();
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

  update(record: AppointmentDocumentTypeDto) {
    this.serviceDetail.update(record);
  }

  delete(record: AppointmentDocumentTypeDto) {
    this.service.delete(record);
  }

  checkActionButtonVisibility() {
    if (this.isActionButtonVisible !== null) {
      return;
    }

    const canEdit = this.permissionService.getGrantedPolicy(
      'CaseEvaluation.AppointmentDocumentTypes.Edit',
    );
    const canDelete = this.permissionService.getGrantedPolicy(
      'CaseEvaluation.AppointmentDocumentTypes.Delete',
    );
    this.isActionButtonVisible = canEdit || canDelete;
  }
}
