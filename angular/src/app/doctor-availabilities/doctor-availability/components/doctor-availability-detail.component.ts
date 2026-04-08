import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { LocalizationPipe, AutofocusDirective } from '@abp/ng.core';
import {
  DateAdapter,
  TimeAdapter,
  ModalComponent,
  ModalCloseDirective,
  ButtonComponent,
} from '@abp/ng.theme.shared';

import { LookupSelectComponent } from '@volo/abp.commercial.ng.ui';

import {
  NgbNavModule,
  NgbDatepickerModule,
  NgbTimepickerModule,
  NgbDateAdapter,
  NgbTimeAdapter,
} from '@ng-bootstrap/ng-bootstrap';
import { DoctorAvailabilityDetailViewService } from '../services/doctor-availability-detail.service';

@Component({
  selector: 'app-doctor-availability-detail-modal',
  changeDetection: ChangeDetectionStrategy.Default,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NgbDatepickerModule,
    NgbTimepickerModule,
    NgbNavModule,
    NgxValidateCoreModule,
    AutofocusDirective,
    ModalCloseDirective,
    LocalizationPipe,
    ModalComponent,
    ButtonComponent,
    LookupSelectComponent,
  ],
  providers: [
    { provide: NgbDateAdapter, useClass: DateAdapter },
    { provide: NgbTimeAdapter, useClass: TimeAdapter },
  ],
  templateUrl: './doctor-availability-detail.component.html',
  styles: [],
})
export class DoctorAvailabilityDetailModalComponent {
  public readonly service = inject(DoctorAvailabilityDetailViewService);
}
