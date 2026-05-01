import { Directive, OnInit, inject } from '@angular/core';

import { ListService, PermissionService } from '@abp/ng.core';

import type { DefenseAttorneyWithNavigationPropertiesDto } from '../../../proxy/defense-attorneys/models';
import { DefenseAttorneyViewService } from '../services/defense-attorney.service';
import { DefenseAttorneyDetailViewService } from '../services/defense-attorney-detail.service';

export const ChildTabDependencies = [];

export const ChildComponentDependencies = [];

@Directive()
export abstract class AbstractDefenseAttorneyComponent implements OnInit {
  public readonly list = inject(ListService);
  public readonly service = inject(DefenseAttorneyViewService);
  public readonly serviceDetail = inject(DefenseAttorneyDetailViewService);
  public readonly permissionService = inject(PermissionService);

  protected title = '::DefenseAttorneys';
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

  update(record: DefenseAttorneyWithNavigationPropertiesDto) {
    this.serviceDetail.update(record);
  }

  delete(record: DefenseAttorneyWithNavigationPropertiesDto) {
    this.service.delete(record);
  }

  checkActionButtonVisibility() {
    if (this.isActionButtonVisible !== null) {
      return;
    }

    const canEdit = this.permissionService.getGrantedPolicy('CaseEvaluation.DefenseAttorneys.Edit');
    const canDelete = this.permissionService.getGrantedPolicy(
      'CaseEvaluation.DefenseAttorneys.Delete',
    );
    this.isActionButtonVisible = canEdit || canDelete;
  }
}
