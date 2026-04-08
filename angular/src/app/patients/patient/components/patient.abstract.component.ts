import { Directive, OnInit, inject } from '@angular/core';
import { ListService, PermissionService } from '@abp/ng.core';

import { genderOptions } from '../../../proxy/enums/gender.enum';
import { phoneNumberTypeOptions } from '../../../proxy/enums/phone-number-type.enum';
import type { PatientWithNavigationPropertiesDto } from '../../../proxy/patients/models';
import { PatientViewService } from '../services/patient.service';
import { PatientDetailViewService } from '../services/patient-detail.service';

export const ChildTabDependencies = [];

export const ChildComponentDependencies = [];

@Directive()
export abstract class AbstractPatientComponent implements OnInit {
  public readonly list = inject(ListService);
  public readonly service = inject(PatientViewService);
  public readonly serviceDetail = inject(PatientDetailViewService);
  public readonly permissionService = inject(PermissionService);

  protected title = '::Patients';
  protected isActionButtonVisible: boolean | null = null;

  genderOptions = genderOptions;
  phoneNumberTypeOptions = phoneNumberTypeOptions;

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

  update(record: PatientWithNavigationPropertiesDto) {
    this.serviceDetail.update(record);
  }

  delete(record: PatientWithNavigationPropertiesDto) {
    this.service.delete(record);
  }

  checkActionButtonVisibility() {
    if (this.isActionButtonVisible !== null) {
      return;
    }

    const canEdit = this.permissionService.getGrantedPolicy('CaseEvaluation.Patients.Edit');
    const canDelete = this.permissionService.getGrantedPolicy('CaseEvaluation.Patients.Delete');
    this.isActionButtonVisible = canEdit || canDelete;
  }
}
