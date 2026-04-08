import { Directive, OnInit, inject } from '@angular/core';

import { ListService, PermissionService } from '@abp/ng.core';

import type { LocationWithNavigationPropertiesDto } from '../../../proxy/locations/models';
import { LocationViewService } from '../services/location.service';
import { LocationDetailViewService } from '../services/location-detail.service';

export const ChildTabDependencies = [];

export const ChildComponentDependencies = [];

@Directive()
export abstract class AbstractLocationComponent implements OnInit {
  public readonly list = inject(ListService);
  public readonly service = inject(LocationViewService);
  public readonly serviceDetail = inject(LocationDetailViewService);
  public readonly permissionService = inject(PermissionService);

  protected title = '::Locations';
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

  update(record: LocationWithNavigationPropertiesDto) {
    this.serviceDetail.update(record);
  }

  delete(record: LocationWithNavigationPropertiesDto) {
    this.service.delete(record);
  }

  checkActionButtonVisibility() {
    if (this.isActionButtonVisible !== null) {
      return;
    }

    const canEdit = this.permissionService.getGrantedPolicy('CaseEvaluation.Locations.Edit');
    const canDelete = this.permissionService.getGrantedPolicy('CaseEvaluation.Locations.Delete');
    this.isActionButtonVisible = canEdit || canDelete;
  }
}
