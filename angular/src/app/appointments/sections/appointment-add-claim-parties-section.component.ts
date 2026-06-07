import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { PagedResultDto } from '@abp/ng.core';
import { Observable } from 'rxjs';
import { AppLookupSelectComponent } from '../../shared/components/app-lookup-select.component';
import {
  AddressAutocompleteComponent,
  AddressFieldMap,
} from '../../shared/address/address-autocomplete.component';
import type { LookupDto, LookupRequestDto } from '../../proxy/shared/models';

/**
 * CI1 (2026-06-05): appointment-level Claim Information parties -- a single
 * Primary Insurance (optional) + a single Claim Examiner (REQUIRED: Name +
 * Email). One each per appointment, rendered below the attorney sections,
 * replacing the per-injury insurance/CE cards that lived in the
 * claim-information modal (those orphaned cards are removed by CI2).
 *
 * State ownership mirrors the attorney section: this is template-only. All
 * FormControls (appointmentInsurance* / appointmentClaimExaminer*) live on the
 * parent `form`; the parent posts one insurance + one CE after the appointment
 * is created. CE Name + Email carry Validators.required on the parent, so the
 * standard submit guard blocks submission until they are filled.
 */
@Component({
  selector: 'app-appointment-add-claim-parties-section',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    AppLookupSelectComponent,
    AddressAutocompleteComponent,
  ],
  templateUrl: './appointment-add-claim-parties-section.component.html',
})
export class AppointmentAddClaimPartiesSectionComponent {
  @Input({ required: true }) form!: FormGroup;
  @Input({ required: true }) getStateLookup!: (
    input: LookupRequestDto,
  ) => Observable<PagedResultDto<LookupDto<string>>>;
  @Input() isFieldInvalid: (name: string) => boolean = () => false;

  readonly insuranceAddressFields: AddressFieldMap = {
    street: 'appointmentInsuranceStreet',
    city: 'appointmentInsuranceCity',
    state: 'appointmentInsuranceStateId',
    zip: 'appointmentInsuranceZip',
  };

  readonly claimExaminerAddressFields: AddressFieldMap = {
    street: 'appointmentClaimExaminerStreet',
    city: 'appointmentClaimExaminerCity',
    state: 'appointmentClaimExaminerStateId',
    zip: 'appointmentClaimExaminerZip',
  };
}
