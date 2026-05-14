import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { LocalizationPipe, PagedResultDto } from '@abp/ng.core';
import { AppLookupSelectComponent } from '../../shared/components/app-lookup-select.component';
import type { LookupDto, LookupRequestDto } from '../../proxy/shared/models';
import { Observable } from 'rxjs';

/**
 * #121 phase T3 (2026-05-13) -- Employer Details section, extracted
 * from `AppointmentAddComponent`. Flat 7-field block (Name +
 * Occupation + Phone + Street + City + State + ZipCode) with no
 * cross-section cascade and no role-conditional visibility.
 *
 * State ownership:
 *   - parent  -> all 7 FormControls live on the main form FormGroup.
 *                Passed in by reference; child wraps its template in
 *                `[formGroup]="form"` so formControlName directives bind
 *                to the parent's controls.
 *   - parent  -> getStateLookup callback (used by `<abp-lookup-select>`
 *                for the state autocomplete).
 */
@Component({
  selector: 'app-appointment-add-employer-details',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, LocalizationPipe, AppLookupSelectComponent],
  templateUrl: './appointment-add-employer-details.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppointmentAddEmployerDetailsComponent {
  @Input({ required: true }) form!: FormGroup;
  @Input({ required: true }) getStateLookup!: (
    input: LookupRequestDto,
  ) => Observable<PagedResultDto<LookupDto<string>>>;
}
