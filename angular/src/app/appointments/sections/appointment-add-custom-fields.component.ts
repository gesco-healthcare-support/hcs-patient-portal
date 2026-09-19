import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { FormArray, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { NgbDatepickerModule } from '@ng-bootstrap/ng-bootstrap';
import { CustomFieldType } from '../../proxy/enums/custom-field-type.enum';

/**
 * #121 phase T1 (2026-05-13) -- "Additional Details" custom-fields
 * section, extracted from <c>AppointmentAddComponent</c>. Parity layout
 * comes verbatim from OLD's `appointment-add.component.html`:797-828:
 * card + h6 header + 6-column responsive grid (1/2/3/4/6 items per row
 * at xs/sm/md/lg/xl).
 *
 * Owns rendering only. The parent retains:
 *   - the FormArray definition on its main FormGroup
 *     (`customFieldsValues`),
 *   - the data loader that rebuilds the array on AppointmentType change
 *     (`loadCustomFieldsForAppointmentType`),
 *   - the builder that creates one FormGroup per CustomField
 *     (`buildCustomFieldGroup`),
 *   - the per-type serializer used at submit time.
 *
 * The child takes the FormArray as an Input by reference. Parent and
 * child observe the same form-state instance; there is no duplicate
 * store and no event plumbing for value changes.
 *
 * The 7 field types (Alphanumeric / Numeric / Picklist / Tickbox /
 * Date / Radio / Time) render branches mirror OLD's renderer matrix
 * documented in `docs/research/stage-2-3-booking-and-view.md` B1.4.
 */
@Component({
  selector: 'app-appointment-add-custom-fields',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, NgbDatepickerModule],
  templateUrl: './appointment-add-custom-fields.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppointmentAddCustomFieldsComponent {
  @Input({ required: true }) customFieldsArray!: FormArray<FormGroup>;

  // Exposed to the template so @if branches can compare against the
  // numeric enum values without leaking the import into the HTML.
  readonly CustomFieldType = CustomFieldType;

  /**
   * Helper consumed only by this template to render Picklist / Tickbox /
   * Radio option lists. OLD stores options as a comma-separated string
   * in `MultipleValues` (no separate option entity); split + trim +
   * drop empty.
   */
  optionsFromMultipleValues(multipleValues: string | null | undefined): string[] {
    if (!multipleValues) return [];
    return multipleValues
      .split(',')
      .map((s) => s.trim())
      .filter((s) => s.length > 0);
  }
}
