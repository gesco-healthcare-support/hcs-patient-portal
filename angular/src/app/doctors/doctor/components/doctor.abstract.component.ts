import { Directive, OnInit, inject } from '@angular/core';

import { ListService, PermissionService } from '@abp/ng.core';

import { genderOptions } from '../../../proxy/enums/gender.enum';
import type { DoctorWithNavigationPropertiesDto } from '../../../proxy/doctors/models';
import { DoctorViewService } from '../services/doctor.service';
import { DoctorDetailViewService } from '../services/doctor-detail.service';

export const ChildTabDependencies = [];

export const ChildComponentDependencies = [];

@Directive()
export abstract class AbstractDoctorComponent implements OnInit {
  public readonly list = inject(ListService);
  public readonly service = inject(DoctorViewService);
  public readonly serviceDetail = inject(DoctorDetailViewService);
  public readonly permissionService = inject(PermissionService);

  protected title = '::Doctors';
  protected isActionButtonVisible: boolean | null = null;

  genderOptions = genderOptions;

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

  update(record: DoctorWithNavigationPropertiesDto) {
    this.serviceDetail.update(record);
  }

  delete(record: DoctorWithNavigationPropertiesDto) {
    this.service.delete(record);
  }

  checkActionButtonVisibility() {
    if (this.isActionButtonVisible !== null) {
      return;
    }

    const canEdit = this.permissionService.getGrantedPolicy('CaseEvaluation.Doctors.Edit');
    const canDelete = this.permissionService.getGrantedPolicy('CaseEvaluation.Doctors.Delete');
    this.isActionButtonVisible = canEdit || canDelete;
  }
}
