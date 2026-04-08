import { Directive, OnInit, inject } from '@angular/core';

import { ListService, PermissionService } from '@abp/ng.core';

import type { WcabOfficeWithNavigationPropertiesDto } from '../../../proxy/wcab-offices/models';
import { WcabOfficeViewService } from '../services/wcab-office.service';
import { WcabOfficeDetailViewService } from '../services/wcab-office-detail.service';

export const ChildTabDependencies = [];

export const ChildComponentDependencies = [];

@Directive()
export abstract class AbstractWcabOfficeComponent implements OnInit {
  public readonly list = inject(ListService);
  public readonly service = inject(WcabOfficeViewService);
  public readonly serviceDetail = inject(WcabOfficeDetailViewService);
  public readonly permissionService = inject(PermissionService);

  protected title = '::WcabOffices';
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

  update(record: WcabOfficeWithNavigationPropertiesDto) {
    this.serviceDetail.update(record);
  }

  delete(record: WcabOfficeWithNavigationPropertiesDto) {
    this.service.delete(record);
  }

  exportToExcel() {
    this.service.exportToExcel();
  }

  checkActionButtonVisibility() {
    if (this.isActionButtonVisible !== null) {
      return;
    }

    const canEdit = this.permissionService.getGrantedPolicy('CaseEvaluation.WcabOffices.Edit');
    const canDelete = this.permissionService.getGrantedPolicy('CaseEvaluation.WcabOffices.Delete');
    this.isActionButtonVisible = canEdit || canDelete;
  }
}
