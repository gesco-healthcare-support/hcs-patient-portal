import { ChangeDetectionStrategy, Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import {
  NgbDateAdapter,
  NgbTimeAdapter,
  NgbCollapseModule,
  NgbDatepickerModule,
  NgbTimepickerModule,
  NgbDropdownModule,
} from '@ng-bootstrap/ng-bootstrap';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';
import { ListService, LocalizationPipe, PermissionDirective } from '@abp/ng.core';
import {
  DateAdapter,
  TimeAdapter,
  NgxDatatableDefaultDirective,
  NgxDatatableListDirective,
} from '@abp/ng.theme.shared';
import { PageComponent, PageToolbarContainerComponent } from '@abp/ng.components/page';
import {
  AdvancedEntityFiltersComponent,
  AdvancedEntityFiltersFormComponent,
  LookupSelectComponent,
} from '@volo/abp.commercial.ng.ui';
import { DefenseAttorneyViewService } from '../services/defense-attorney.service';
import { DefenseAttorneyDetailViewService } from '../services/defense-attorney-detail.service';
import { DefenseAttorneyDetailModalComponent } from './defense-attorney-detail.component';
import {
  AbstractDefenseAttorneyComponent,
  ChildTabDependencies,
  ChildComponentDependencies,
} from './defense-attorney.abstract.component';

@Component({
  selector: 'app-defense-attorney',
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [
    ...ChildTabDependencies,
    NgbCollapseModule,
    NgbDatepickerModule,
    NgbTimepickerModule,
    NgbDropdownModule,
    NgxValidateCoreModule,
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    NgxDatatableModule,
    NgxDatatableDefaultDirective,
    NgxDatatableListDirective,
    PermissionDirective,
    LocalizationPipe,
    PageComponent,
    PageToolbarContainerComponent,
    AdvancedEntityFiltersComponent,
    AdvancedEntityFiltersFormComponent,
    LookupSelectComponent,

    DefenseAttorneyDetailModalComponent,
    ...ChildComponentDependencies,
  ],
  providers: [
    ListService,
    DefenseAttorneyViewService,
    DefenseAttorneyDetailViewService,
    { provide: NgbDateAdapter, useClass: DateAdapter },
    { provide: NgbTimeAdapter, useClass: TimeAdapter },
  ],
  templateUrl: './defense-attorney.component.html',
  styles: `
    ::ng-deep.datatable-row-detail {
      background: transparent !important;
    }
  `,
})
export class DefenseAttorneyComponent extends AbstractDefenseAttorneyComponent {}
