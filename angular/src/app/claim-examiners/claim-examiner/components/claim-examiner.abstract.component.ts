import { Directive, OnInit, inject } from '@angular/core';

import { ListService, PermissionService } from '@abp/ng.core';

import type { ClaimExaminerWithNavigationPropertiesDto } from '../../../proxy/claim-examiners/models';
import { ClaimExaminerViewService } from '../services/claim-examiner.service';
import { ClaimExaminerDetailViewService } from '../services/claim-examiner-detail.service';

export const ChildTabDependencies = [];

export const ChildComponentDependencies = [];

@Directive()
export abstract class AbstractClaimExaminerComponent implements OnInit {
  public readonly list = inject(ListService);
  public readonly service = inject(ClaimExaminerViewService);
  public readonly serviceDetail = inject(ClaimExaminerDetailViewService);
  public readonly permissionService = inject(PermissionService);

  protected title = '::ClaimExaminers';
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

  update(record: ClaimExaminerWithNavigationPropertiesDto) {
    this.serviceDetail.update(record);
  }

  delete(record: ClaimExaminerWithNavigationPropertiesDto) {
    this.service.delete(record);
  }

  checkActionButtonVisibility() {
    if (this.isActionButtonVisible !== null) {
      return;
    }

    const canEdit = this.permissionService.getGrantedPolicy('CaseEvaluation.ClaimExaminers.Edit');
    const canDelete = this.permissionService.getGrantedPolicy(
      'CaseEvaluation.ClaimExaminers.Delete',
    );
    this.isActionButtonVisible = canEdit || canDelete;
  }
}
