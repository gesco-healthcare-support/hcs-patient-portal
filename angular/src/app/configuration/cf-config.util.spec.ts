import {
  buildFieldCatalog,
  CONFIG_SECTIONS,
  emptyFieldState,
  fieldStateFromConfigs,
  fieldStateToBatch,
} from './cf-config.util';
import { AppointmentTypeFieldConfigDto } from '../proxy/appointment-type-field-configs/models';

describe('cf-config.util', () => {
  describe('CONFIG_SECTIONS', () => {
    it('defines the five lookup sections in rail order', () => {
      expect(CONFIG_SECTIONS.map((s) => s.key)).toEqual([
        'types',
        'statuses',
        'doctypes',
        'languages',
        'states',
      ]);
    });

    it('marks only types with a description and only doctypes with an active flag', () => {
      const byKey = (k: string) => CONFIG_SECTIONS.find((s) => s.key === k)!;
      expect(byKey('types').hasDescription).toBe(true);
      expect(byKey('doctypes').hasActive).toBe(true);
      expect(byKey('statuses').hasDescription).toBe(false);
      expect(byKey('languages').hasActive).toBe(false);
    });
  });

  describe('buildFieldCatalog', () => {
    it('groups fields by booking-form section in first-seen order', () => {
      const catalog = buildFieldCatalog();
      expect(catalog.map((g) => g.group)).toEqual([
        'Patient Demographics',
        'Employer Details',
        'Applicant Attorney',
        'Defense Attorney',
        'Insurance Carrier',
        'Claim Examiner',
        'Claim Information',
        'Documents',
        'Schedule',
      ]);
    });

    it('places all seventy fields across the groups exactly once', () => {
      const catalog = buildFieldCatalog();
      const keys: string[] = [];
      for (const group of catalog) {
        for (const field of group.fields) {
          keys.push(field.key);
        }
      }
      expect(keys.length).toBe(70);
      expect(new Set(keys).size).toBe(70);
    });

    it('keeps the Patient Demographics group together', () => {
      const patient = buildFieldCatalog().find((g) => g.group === 'Patient Demographics')!;
      expect(patient.fields.map((f) => f.key)).toContain('socialSecurityNumber');
      expect(patient.fields.length).toBe(17);
    });
  });

  describe('emptyFieldState', () => {
    it('returns a default state for every catalog key', () => {
      const state = emptyFieldState();
      expect(Object.keys(state).length).toBe(70);
      expect(state['panelNumber']).toEqual({
        hidden: false,
        readOnly: false,
        required: false,
        defaultValue: '',
      });
    });
  });

  describe('fieldStateFromConfigs', () => {
    it('overlays a persisted row onto the default state', () => {
      const configs: AppointmentTypeFieldConfigDto[] = [
        {
          fieldName: 'socialSecurityNumber',
          hidden: true,
          readOnly: false,
          required: false,
          defaultValue: null,
        },
      ];
      const state = fieldStateFromConfigs(configs);
      expect(state['socialSecurityNumber'].hidden).toBe(true);
      // untouched fields stay default
      expect(state['panelNumber'].hidden).toBe(false);
    });

    it('coerces a null defaultValue to an empty string', () => {
      const configs: AppointmentTypeFieldConfigDto[] = [
        {
          fieldName: 'street',
          hidden: false,
          readOnly: true,
          required: false,
          defaultValue: null,
        },
      ];
      expect(fieldStateFromConfigs(configs)['street'].defaultValue).toBe('');
    });

    it('ignores a stale FieldName that is not in the catalog', () => {
      const configs: AppointmentTypeFieldConfigDto[] = [
        {
          fieldName: 'someRetiredField',
          hidden: true,
          readOnly: true,
          required: true,
          defaultValue: 'x',
        },
      ];
      const state = fieldStateFromConfigs(configs);
      expect(state['someRetiredField']).toBeUndefined();
      expect(Object.keys(state).length).toBe(70);
    });
  });

  describe('fieldStateToBatch', () => {
    it('omits fields that are still at their default', () => {
      expect(fieldStateToBatch(emptyFieldState())).toEqual([]);
    });

    it('includes a hidden-only field and trims its default value to null', () => {
      const state = emptyFieldState();
      state['panelNumber'] = {
        hidden: true,
        readOnly: false,
        required: false,
        defaultValue: '   ',
      };
      const batch = fieldStateToBatch(state);
      expect(batch.length).toBe(1);
      expect(batch[0].fieldName).toBe('panelNumber');
      expect(batch[0].hidden).toBe(true);
      expect(batch[0].defaultValue).toBeNull();
    });

    it('persists a trimmed non-empty default value', () => {
      const state = emptyFieldState();
      state['appointmentLanguageId'] = {
        hidden: false,
        readOnly: false,
        required: true,
        defaultValue: '  English  ',
      };
      const batch = fieldStateToBatch(state);
      expect(batch.length).toBe(1);
      expect(batch[0].fieldName).toBe('appointmentLanguageId');
      expect(batch[0].required).toBe(true);
      expect(batch[0].defaultValue).toBe('English');
    });

    it('round-trips a deviating field through configs -> state -> batch', () => {
      const configs: AppointmentTypeFieldConfigDto[] = [
        {
          fieldName: 'street',
          hidden: false,
          readOnly: true,
          required: false,
          defaultValue: 'Suite 100',
        },
      ];
      const batch = fieldStateToBatch(fieldStateFromConfigs(configs));
      expect(batch.length).toBe(1);
      expect(batch[0]).toEqual({
        fieldName: 'street',
        hidden: false,
        readOnly: true,
        required: false,
        defaultValue: 'Suite 100',
      });
    });
  });
});
