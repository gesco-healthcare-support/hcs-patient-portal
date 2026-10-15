import { AppointmentAddCustomFieldsComponent } from './appointment-add-custom-fields.component';
import { CustomFieldType } from '../../proxy/enums/custom-field-type.enum';

/**
 * The booking form's "Additional Details" section. Rendering is the template's job; the one piece
 * of logic is how a Picklist / Tickbox / Radio field's options are read out of the single
 * comma-separated `MultipleValues` string they are stored in.
 *
 * <p>It had no spec. It injects nothing, so it is built with `new`.</p>
 */
describe('AppointmentAddCustomFieldsComponent', () => {
  it('exposes the field-type enum to the template', () => {
    expect(new AppointmentAddCustomFieldsComponent().CustomFieldType).toBe(CustomFieldType);
  });

  it('reads no options from an absent or empty string', () => {
    const c = new AppointmentAddCustomFieldsComponent();
    expect(c.optionsFromMultipleValues(null)).toEqual([]);
    expect(c.optionsFromMultipleValues(undefined)).toEqual([]);
    expect(c.optionsFromMultipleValues('')).toEqual([]);
  });

  it('splits on commas, trims each option and drops the empty ones', () => {
    const c = new AppointmentAddCustomFieldsComponent();
    expect(c.optionsFromMultipleValues(' Left, Right ,, Both ,')).toEqual([
      'Left',
      'Right',
      'Both',
    ]);
  });
});
