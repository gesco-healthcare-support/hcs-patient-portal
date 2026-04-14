import { ChangeDetectionStrategy, Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  NgbDateAdapter,
  NgbTimeAdapter,
  NgbCollapseModule,
  NgbDatepickerModule,
  NgbTimepickerModule,
  NgbDropdownModule,
  NgbPaginationModule,
} from '@ng-bootstrap/ng-bootstrap';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { ListService, LocalizationPipe, PermissionDirective } from '@abp/ng.core';
import { DateAdapter, TimeAdapter } from '@abp/ng.theme.shared';
import { PageComponent, PageToolbarContainerComponent } from '@abp/ng.components/page';
import {
  AdvancedEntityFiltersComponent,
  AdvancedEntityFiltersFormComponent,
  LookupSelectComponent,
} from '@volo/abp.commercial.ng.ui';
import { DoctorAvailabilityViewService } from '../services/doctor-availability.service';
import { DoctorAvailabilityDetailViewService } from '../services/doctor-availability-detail.service';
import {
  AbstractDoctorAvailabilityComponent,
  ChildTabDependencies,
  ChildComponentDependencies,
} from './doctor-availability.abstract.component';

@Component({
  selector: 'app-doctor-availability',
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [
    ...ChildTabDependencies,
    NgbCollapseModule,
    NgbDatepickerModule,
    NgbTimepickerModule,
    NgbDropdownModule,
    NgbPaginationModule,
    NgxValidateCoreModule,
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    PermissionDirective,
    LocalizationPipe,
    RouterLink,
    PageComponent,
    PageToolbarContainerComponent,
    AdvancedEntityFiltersComponent,
    AdvancedEntityFiltersFormComponent,
    LookupSelectComponent,

    ...ChildComponentDependencies,
  ],
  providers: [
    ListService,
    DoctorAvailabilityViewService,
    DoctorAvailabilityDetailViewService,
    { provide: NgbDateAdapter, useClass: DateAdapter },
    { provide: NgbTimeAdapter, useClass: TimeAdapter },
  ],
  templateUrl: './doctor-availability.component.html',
  styles: `
    ::ng-deep.datatable-row-detail {
      background: transparent !important;
    }
  `,
})
export class DoctorAvailabilityComponent extends AbstractDoctorAvailabilityComponent {}
