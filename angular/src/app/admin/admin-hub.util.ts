/**
 * Pure helpers + static config for the internal Admin hub (Prompt 16, Part B).
 * Mirrors the Users hub util: section rail metadata gated by granted policy,
 * plus the section-specific pure logic (template preview/insert, audit chips +
 * CSV, locked-role check) so the hub component stays thin and the logic is unit
 * tested without DI.
 */

/** The four sections of the Admin hub, in rail order. */
export type AdminSectionKey = 'templates' | 'parameters' | 'roles' | 'audit';

/** Rail metadata for one section: label, icon, deep-link route, gating policy. */
export interface AdminSection {
  key: AdminSectionKey;
  /** IconComponent name. */
  icon: string;
  label: string;
  /** Absolute router path the rail item links to (deep-linkable + route-guarded). */
  route: string;
  /** ABP policy gating the rail item + its route. */
  policy: string;
}

/**
 * Section rail. Visibility keys off the granted policy (same mechanism as the
 * Users hub): Notification Templates + System Parameters ride custom
 * CaseEvaluation policies (Supervisor + IT Admin; Intake reads System
 * Parameters); Users & Roles and Audit Logs ride the stock ABP framework
 * policies (IT Admin / host). A section the caller is not granted simply does
 * not render -- no dead links, no click-into-403.
 */
export const ADMIN_SECTIONS: AdminSection[] = [
  {
    key: 'templates',
    icon: 'doc',
    label: 'Notification Templates',
    route: '/admin/templates',
    policy: 'CaseEvaluation.NotificationTemplates',
  },
  {
    key: 'parameters',
    icon: 'settings',
    label: 'System Parameters',
    route: '/admin/parameters',
    policy: 'CaseEvaluation.SystemParameters',
  },
  {
    key: 'roles',
    icon: 'users',
    label: 'Users & Roles',
    route: '/admin/roles',
    policy: 'AbpIdentity.Roles',
  },
  {
    key: 'audit',
    icon: 'clock',
    label: 'Audit Logs',
    route: '/admin/audit',
    policy: 'AuditLogging.AuditLogs',
  },
];

// ---------------------------------------------------------------------------
// Permission matrix
// ---------------------------------------------------------------------------

/**
 * The system role whose permissions are all-granted and not editable. IT Admin
 * is the platform super-role; the matrix shows its grants read-only (no save).
 */
export const LOCKED_ROLE_NAME = 'IT Admin';

export function isLockedRole(roleName: string | null | undefined): boolean {
  return (roleName ?? '').trim() === LOCKED_ROLE_NAME;
}

/** Internal vs external grouping for the role rail, by role name. */
export function roleKind(roleName: string | null | undefined): 'Internal' | 'External' {
  const internal = ['IT Admin', 'Staff Supervisor', 'Intake Staff'];
  return internal.includes((roleName ?? '').trim()) ? 'Internal' : 'External';
}

// ---------------------------------------------------------------------------
// Notification-template editor
// ---------------------------------------------------------------------------

/** A live-preview segment: plain text, or a variable placeholder to highlight. */
export interface PreviewSegment {
  text: string;
  isVar: boolean;
}

// ##Name## where Name is one or more ASCII word characters -- matches the
// server-side TemplateVariableSubstitutor grammar exactly.
const TOKEN_RE = /##([A-Za-z0-9_]+)##/g;

/**
 * Splits template text into plain + variable segments for the live preview.
 * Each variable segment renders the humanized label (or token) highlighted, so
 * the editor sees where each ##Var## lands without a server round-trip.
 */
export function previewSegments(
  text: string | null | undefined,
  labelByToken: Record<string, string> = {},
): PreviewSegment[] {
  const src = text ?? '';
  const segments: PreviewSegment[] = [];
  let lastIndex = 0;
  TOKEN_RE.lastIndex = 0;
  let match: RegExpExecArray | null;
  while ((match = TOKEN_RE.exec(src)) !== null) {
    if (match.index > lastIndex) {
      segments.push({ text: src.slice(lastIndex, match.index), isVar: false });
    }
    const token = match[1];
    segments.push({ text: labelByToken[token] ?? humanizeToken(token), isVar: true });
    lastIndex = match.index + match[0].length;
  }
  if (lastIndex < src.length) {
    segments.push({ text: src.slice(lastIndex), isVar: false });
  }
  return segments;
}

/** Appends a ##token## placeholder to the body, matching the prototype insert. */
export function insertVariable(body: string | null | undefined, token: string): string {
  const base = body ?? '';
  const needsSpace = base.length > 0 && !base.endsWith(' ') && !base.endsWith('\n');
  return base + (needsSpace ? ' ' : '') + '##' + token + '##';
}

/** "EmailSubjectIdentity" -> "Email Subject Identity"; acronyms stay intact. */
export function humanizeToken(token: string): string {
  return (token ?? '').replace(/([a-z0-9])([A-Z])/g, '$1 $2');
}

// ---------------------------------------------------------------------------
// Audit logs
// ---------------------------------------------------------------------------

export function auditMethodClass(method: string | null | undefined): string {
  switch ((method ?? '').toUpperCase()) {
    case 'GET':
      return 'get';
    case 'POST':
      return 'post';
    case 'PUT':
      return 'put';
    case 'DELETE':
      return 'del';
    default:
      return '';
  }
}

export function auditStatusClass(status: number | null | undefined): string {
  const code = status ?? 0;
  if (code >= 500) {
    return 's5';
  }
  if (code >= 400) {
    return 's4';
  }
  return 's2';
}

export function auditResultLabel(status: number | null | undefined): string {
  const code = status ?? 0;
  if (code >= 500) {
    return 'Server error';
  }
  if (code >= 400) {
    return 'Denied / throttled';
  }
  return 'Success';
}

/** One row of the audit CSV export. */
export interface AuditCsvRow {
  time: string;
  user: string;
  method: string;
  url: string;
  status: number;
  durationMs: number;
  ip: string;
  client: string;
  tenant: string;
}

const AUDIT_CSV_HEADER = [
  'Time',
  'User',
  'Method',
  'URL',
  'Status',
  'Duration (ms)',
  'IP',
  'Client',
  'Tenant',
];

/** RFC-4180-ish CSV (quote fields containing comma, quote, or newline). */
export function buildAuditCsv(rows: AuditCsvRow[]): string {
  const escape = (value: unknown): string => {
    const text = String(value ?? '');
    return /[",\n]/.test(text) ? '"' + text.replace(/"/g, '""') + '"' : text;
  };
  const lines = [AUDIT_CSV_HEADER.join(',')];
  for (const row of rows) {
    lines.push(
      [
        row.time,
        row.user,
        row.method,
        row.url,
        row.status,
        row.durationMs,
        row.ip,
        row.client,
        row.tenant,
      ]
        .map(escape)
        .join(','),
    );
  }
  return lines.join('\n');
}

// ---------------------------------------------------------------------------
// System parameters
// ---------------------------------------------------------------------------

/** One editable numeric system-parameter field. */
export interface SpField {
  /** Key on SystemParameterDto / SystemParameterUpdateDto. */
  key: string;
  label: string;
  /** Honest unit per the backend entity (days unless noted). */
  unit: string;
  hint?: string;
}

export interface SpGroup {
  title: string;
  icon: string;
  /** Tint class for the group icon chip. */
  tint: string;
  fields: SpField[];
}

/**
 * Grouped editor layout. Units are labeled to the STORED unit (verified against
 * the SystemParameter domain entity: all are days except the slot duration in
 * minutes) -- the prototype mislabeled several as hours; we label honestly.
 */
export const SP_GROUPS: SpGroup[] = [
  {
    title: 'Booking windows',
    icon: 'calendar',
    tint: 'tint-blue',
    fields: [
      {
        key: 'appointmentLeadTime',
        label: 'Minimum lead time',
        unit: 'days',
        hint: 'Earliest a new request may be scheduled.',
      },
      { key: 'appointmentMaxTimePQME', label: 'Max window - Panel QME', unit: 'days' },
      { key: 'appointmentMaxTimeAME', label: 'Max window - AME', unit: 'days' },
      { key: 'appointmentMaxTimeOTHER', label: 'Max window - other types', unit: 'days' },
      { key: 'appointmentMaxTimeInternal', label: 'Max window - internal booking', unit: 'days' },
    ],
  },
  {
    title: 'Cancellation & auto-cancel',
    icon: 'x',
    tint: 'tint-red',
    fields: [
      {
        key: 'appointmentCancelTime',
        label: 'Cancel cutoff',
        unit: 'days',
        hint: 'Minimum days before the slot to allow an external cancel request.',
      },
      {
        key: 'autoCancelCutoffTime',
        label: 'Joint-declaration auto-cancel cutoff',
        unit: 'days',
      },
    ],
  },
  {
    title: 'Deadlines & reminders',
    icon: 'clock',
    tint: 'tint-amber',
    fields: [
      {
        key: 'appointmentDueDays',
        label: 'Decision deadline',
        unit: 'days',
        hint: 'Days staff have to decide a request. Legal limit is 5 - 3 leaves a 2-day buffer.',
      },
      { key: 'appointmentDurationTime', label: 'Default slot duration', unit: 'minutes' },
      { key: 'reminderCutoffTime', label: 'Reminder sent before visit', unit: 'days' },
      {
        key: 'pendingAppointmentOverDueNotificationDays',
        label: 'Pending-overdue alert after',
        unit: 'days',
      },
      {
        key: 'jointDeclarationUploadCutoffDays',
        label: 'Joint-declaration upload cutoff',
        unit: 'days',
      },
    ],
  },
];
