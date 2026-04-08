import { Directive, OnInit, inject } from '@angular/core';

import { ListService, PermissionService } from '@abp/ng.core';

import type { StateDto } from '../../../proxy/states/models';
import { StateViewService } from '../services/state.service';
import { StateDetailViewService } from '../services/state-detail.service';

export const ChildTabDependencies = [];

export const ChildComponentDependencies = [];

@Directive()
export abstract class AbstractStateComponent implements OnInit {
  public readonly list = inject(ListService);
  public readonly service = inject(StateViewService);
  public readonly serviceDetail = inject(StateDetailViewService);
  public readonly permissionService = inject(PermissionService);

  protected title = '::States';
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

  update(record: StateDto) {
    this.serviceDetail.update(record);
  }

  delete(record: StateDto) {
    this.service.delete(record);
  }

  checkActionButtonVisibility() {
    if (this.isActionButtonVisible !== null) {
      return;
    }

    const canEdit = this.permissionService.getGrantedPolicy('CaseEvaluation.States.Edit');
    const canDelete = this.permissionService.getGrantedPolicy('CaseEvaluation.States.Delete');
    this.isActionButtonVisible = canEdit || canDelete;
  }
}
