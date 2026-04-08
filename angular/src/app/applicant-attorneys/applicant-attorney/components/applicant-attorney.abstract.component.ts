import { Directive, OnInit, inject } from '@angular/core';

import { ListService, PermissionService } from '@abp/ng.core';

import type { ApplicantAttorneyWithNavigationPropertiesDto } from '../../../proxy/applicant-attorneys/models';
import { ApplicantAttorneyViewService } from '../services/applicant-attorney.service';
import { ApplicantAttorneyDetailViewService } from '../services/applicant-attorney-detail.service';

export const ChildTabDependencies = [];

export const ChildComponentDependencies = [];

@Directive()
export abstract class AbstractApplicantAttorneyComponent implements OnInit {
  public readonly list = inject(ListService);
  public readonly service = inject(ApplicantAttorneyViewService);
  public readonly serviceDetail = inject(ApplicantAttorneyDetailViewService);
  public readonly permissionService = inject(PermissionService);

  protected title = '::ApplicantAttorneys';
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

  update(record: ApplicantAttorneyWithNavigationPropertiesDto) {
    this.serviceDetail.update(record);
  }

  delete(record: ApplicantAttorneyWithNavigationPropertiesDto) {
    this.service.delete(record);
  }

  checkActionButtonVisibility() {
    if (this.isActionButtonVisible !== null) {
      return;
    }

    const canEdit = this.permissionService.getGrantedPolicy(
      'CaseEvaluation.ApplicantAttorneys.Edit',
    );
    const canDelete = this.permissionService.getGrantedPolicy(
      'CaseEvaluation.ApplicantAttorneys.Delete',
    );
    this.isActionButtonVisible = canEdit || canDelete;
  }
}
