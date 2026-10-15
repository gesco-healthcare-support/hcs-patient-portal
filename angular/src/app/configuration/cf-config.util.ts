import { FLAGGABLE_FIELDS, FlaggableField } from '../appointments/appointment/send-back-fields';
import {
  AppointmentTypeFieldConfigBatchItemDto,
  AppointmentTypeFieldConfigDto,
} from '../proxy/appointment-type-field-configs/models';

/** The five lookup sections of the Configuration hub. */
export type ConfigSectionKey = 'types' | 'statuses' | 'doctypes' | 'languages' | 'states';

/**
 * Static metadata for one Configuration section. Drives the rail (label, icon,
 * route, gating policy) and the section table / modal shape (which columns and
 * form fields a section supports) so the component stays declarative.
 */
export interface ConfigSection {
  key: ConfigSectionKey;
  /** Plural heading + rail label, e.g. "Appointment Types". */
  label: string;
  /** Lowercase singular for modal titles + toasts, e.g. "appointment type". */
  singular: string;
  /** IconComponent name for the rail item. */
  icon: string;
  /** Absolute router path this section is mounted at (rail routerLink). */
  route: string;
  /** ABP permission required to see/use the section (rail gating + route guard). */
  policy: string;
  /** Types carry a Description column + modal field. */
  hasDescription: boolean;
  /** Document types carry an Active flag (isActive) instead of any "required". */
  hasActive: boolean;
  /** #4: document types are offered to a SET of appointment types (M2M) plus an
   * "applies to all" flag; drives the multi-select in the create/edit modal. */
  hasAppointmentTypes?: boolean;
  /**
   * Singular noun for the usage count -- the unit differs per section
   * (B2 counts appointments for types, patients for languages, etc.), so the
   * label cannot be a fixed "appointment". Empty when usage is not tracked.
   */
  usageNoun: string;
}

export const CONFIG_SECTIONS: ConfigSection[] = [
  {
    key: 'types',
    label: 'Appointment Types',
    singular: 'appointment type',
    icon: 'list',
    route: '/appointment-management/appointment-types',
    policy: 'CaseEvaluation.AppointmentTypes',
    hasDescription: true,
    hasActive: false,
    usageNoun: 'appointment',
  },
  {
    key: 'statuses',
    label: 'Appointment Statuses',
    singular: 'status',
    icon: 'check',
    route: '/appointment-management/appointment-statuses',
    policy: 'CaseEvaluation.AppointmentStatuses',
    hasDescription: false,
    hasActive: false,
    usageNoun: '',
  },
  {
    key: 'doctypes',
    label: 'Document Types',
    singular: 'document type',
    icon: 'doc',
    route: '/appointment-management/document-types',
    policy: 'CaseEvaluation.AppointmentDocumentTypes',
    hasDescription: false,
    hasActive: true,
    hasAppointmentTypes: true,
    usageNoun: 'document',
  },
  {
    key: 'languages',
    label: 'Appointment Languages',
    singular: 'language',
    icon: 'list',
    route: '/appointment-management/appointment-languages',
    policy: 'CaseEvaluation.AppointmentLanguages',
    hasDescription: false,
    hasActive: false,
    usageNoun: 'patient',
  },
  {
    key: 'states',
    label: 'States',
    singular: 'state',
    icon: 'map',
    route: '/configurations/states',
    policy: 'CaseEvaluation.States',
    hasDescription: false,
    hasActive: false,
    usageNoun: 'reference',
  },
];

/** A left-rail item for the shared Configuration rail (the five lookups plus WCAB). */
export interface ConfigRailItem {
  label: string;
  icon: string;
  route: string;
  policy: string;
}

/**
 * The Configuration hub rail, shared by the hub and the WCAB Offices page
 * (#3, 2026-06-19). The five lookup sections derive from CONFIG_SECTIONS so the
 * rail never drifts; WCAB is appended -- it is a rich page (address/state) that
 * keeps its own component but now renders inside the hub shell.
 */
export const CONFIG_RAIL_ITEMS: ConfigRailItem[] = [
  ...CONFIG_SECTIONS.map((s) => ({
    label: s.label,
    icon: s.icon,
    route: s.route,
    policy: s.policy,
  })),
  {
    label: 'WCAB Offices',
    icon: 'map',
    route: '/doctor-management/wcab-offices',
    policy: 'CaseEvaluation.WcabOffices',
  },
];

/**
 * A lookup row normalized across all five sections so the table renders one
 * shape. Section-specific columns (description, isActive) are optional.
 */
export interface ConfigRow {
  id: string;
  name: string;
  description?: string | null;
  usageCount?: number | null;
  isSystem: boolean;
  isActive?: boolean;
  /** #4: appointment types this document type is offered for (M2M). */
  appointmentTypeIds?: string[];
  /** #4: offered for every appointment type. */
  appliesToAll?: boolean;
  concurrencyStamp?: string;
}

/** The create/edit modal draft, superset across sections. */
export interface ConfigFormState {
  id: string | null;
  name: string;
  description: string;
  isActive: boolean;
  isSystem: boolean;
  /** #4 (document types): the offered appointment-type set + "applies to all". */
  appointmentTypeIds: string[];
  appliesToAll: boolean;
  concurrencyStamp?: string;
}

/** True when a row cannot be deleted: system-locked, or still referenced. */
export function isDeleteLocked(row: ConfigRow): boolean {
  return row.isSystem || (row.usageCount ?? 0) > 0;
}

/** Editable state for one field row in the Field Configuration panel. */
export interface FieldConfigState {
  hidden: boolean;
  readOnly: boolean;
  required: boolean;
  defaultValue: string;
}

/** A catalog group (booking-form section) with its fields, for template rendering. */
export interface FieldCatalogGroup {
  group: string;
  fields: FlaggableField[];
}

/**
 * Groups the shared FlaggableField registry by its `group`, preserving
 * first-seen order. This is the field catalog the Field Configuration panel
 * renders; persisting each field's `key` keeps FieldName aligned with the
 * booking-form control names (see send-back-fields.ts).
 */
export function buildFieldCatalog(): FieldCatalogGroup[] {
  const groups: FieldCatalogGroup[] = [];
  for (const field of FLAGGABLE_FIELDS) {
    let group = groups.find((g) => g.group === field.group);
    if (!group) {
      group = { group: field.group, fields: [] };
      groups.push(group);
    }
    group.fields.push(field);
  }
  return groups;
}

/** Default state (visible, editable, optional, no default value) for every catalog field. */
export function emptyFieldState(): Record<string, FieldConfigState> {
  const state: Record<string, FieldConfigState> = {};
  for (const field of FLAGGABLE_FIELDS) {
    state[field.key] = { hidden: false, readOnly: false, required: false, defaultValue: '' };
  }
  return state;
}

/**
 * Overlays persisted config rows onto the default state so the panel reflects
 * saved values. Rows whose FieldName is not in the catalog are ignored -- the
 * backend stores any name, but the catalog is the source of truth, so a stale
 * key simply does not surface (it would also no-op on the booking form).
 */
export function fieldStateFromConfigs(
  configs: AppointmentTypeFieldConfigDto[],
): Record<string, FieldConfigState> {
  const state = emptyFieldState();
  for (const config of configs) {
    const name = config.fieldName;
    if (!name || !(name in state)) {
      continue;
    }
    state[name] = {
      hidden: !!config.hidden,
      readOnly: !!config.readOnly,
      required: !!config.required,
      defaultValue: config.defaultValue ?? '',
    };
  }
  return state;
}

/**
 * Maps the panel state to the batch-save payload. Only fields that DEVIATE from
 * the default are emitted: the backend reconciler replaces the full set, so an
 * omitted field is deleted (reset to default) and an unchanged-default field
 * never needs a row. A blank default value is sent as null.
 */
export function fieldStateToBatch(
  state: Record<string, FieldConfigState>,
): AppointmentTypeFieldConfigBatchItemDto[] {
  const items: AppointmentTypeFieldConfigBatchItemDto[] = [];
  for (const field of FLAGGABLE_FIELDS) {
    const s = state[field.key];
    if (!s) {
      continue;
    }
    const trimmedDefault = s.defaultValue.trim();
    const isDefault = !s.hidden && !s.readOnly && !s.required && trimmedDefault === '';
    if (isDefault) {
      continue;
    }
    items.push({
      fieldName: field.key,
      hidden: s.hidden,
      readOnly: s.readOnly,
      required: s.required,
      defaultValue: trimmedDefault === '' ? null : trimmedDefault,
    });
  }
  return items;
}
