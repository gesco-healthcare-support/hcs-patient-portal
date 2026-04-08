import { Directive, OnInit, inject } from '@angular/core';

import { ListService, PermissionService } from '@abp/ng.core';

import type { AppointmentLanguageDto } from '../../../proxy/appointment-languages/models';
import { AppointmentLanguageViewService } from '../services/appointment-language.service';
import { AppointmentLanguageDetailViewService } from '../services/appointment-language-detail.service';

export const ChildTabDependencies = [];

export const ChildComponentDependencies = [];

@Directive()
export abstract class AbstractAppointmentLanguageComponent implements OnInit {
  public readonly list = inject(ListService);
  public readonly service = inject(AppointmentLanguageViewService);
  public readonly serviceDetail = inject(AppointmentLanguageDetailViewService);
  public readonly permissionService = inject(PermissionService);

  protected title = '::AppointmentLanguages';
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

  update(record: AppointmentLanguageDto) {
    this.serviceDetail.update(record);
  }

  delete(record: AppointmentLanguageDto) {
    this.service.delete(record);
  }

  checkActionButtonVisibility() {
    if (this.isActionButtonVisible !== null) {
      return;
    }

    const canEdit = this.permissionService.getGrantedPolicy(
      'CaseEvaluation.AppointmentLanguages.Edit',
    );
    const canDelete = this.permissionService.getGrantedPolicy(
      'CaseEvaluation.AppointmentLanguages.Delete',
    );
    this.isActionButtonVisible = canEdit || canDelete;
  }
}
