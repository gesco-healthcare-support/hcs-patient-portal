/**
 * Presentation catalog for the notification-template admin page
 * (`/admin/templates`, `InternalAdminHubComponent` `templates` section).
 *
 * The backend stores ~64 per-tenant `NotificationTemplate` rows keyed by a
 * cryptic OLD-derived PascalCase `TemplateCode` (e.g. `PatientAppointmentApprovedExt`).
 * This module maps each code to a human-friendly name, a "when it fires" blurb,
 * and a lifecycle GROUP so non-technical staff can find and edit the right email.
 *
 * This is a code-side catalog (single source of truth for presentation), NOT a
 * tenant-editable field: it stays in lockstep with the backend
 * `NotificationTemplateConsts.Codes` set and never drifts per tenant. It changes
 * NO email content and adds NO template -- purely how the admin page labels and
 * organizes the existing rows.
 *
 * Drift handling: the runtime helpers below never hide a template. Any code the
 * API returns that is not catalogued falls back to a humanized name in the
 * "Other notices" group, so a future backend code still shows up. The
 * completeness guard lives in the sibling spec, which asserts every code in
 * {@link KNOWN_TEMPLATE_CODES} has a catalog entry.
 */

/** Lifecycle bucket a template belongs to, in display order via {@link TEMPLATE_GROUPS}. */
export type TemplateGroupKey =
  | 'accounts'
  | 'request'
  | 'status'
  | 'change'
  | 'documents'
  | 'reminders'
  | 'other';

/** Display metadata for one lifecycle group (rail/section header). */
export interface TemplateGroupMeta {
  key: TemplateGroupKey;
  title: string;
}

/**
 * Lifecycle groups in the order the admin list renders them: onboarding first,
 * then the request/decision flow, change requests, documents, scheduled
 * reminders, and finally the misc/unwired notices.
 */
export const TEMPLATE_GROUPS: readonly TemplateGroupMeta[] = [
  { key: 'accounts', title: 'Accounts & access' },
  { key: 'request', title: 'Appointment requests' },
  { key: 'status', title: 'Approvals & status changes' },
  { key: 'change', title: 'Reschedule & cancellation requests' },
  { key: 'documents', title: 'Documents & packets' },
  { key: 'reminders', title: 'Reminders & scheduled notices' },
  { key: 'other', title: 'Other notices' },
];

/** Friendly presentation for one template code. */
export interface TemplateCatalogEntry {
  /** Human-friendly name shown as the primary label. */
  name: string;
  /** One-line "when does this send" description for admins. */
  whenItFires: string;
  /** Lifecycle bucket for grouping. */
  group: TemplateGroupKey;
}

/**
 * Every backend template code mapped to its presentation metadata. Keyed by the
 * raw `TemplateCode` string (the value of each `NotificationTemplateConsts.Codes`
 * constant). The spec guards that this covers {@link KNOWN_TEMPLATE_CODES} fully.
 */
export const EMAIL_TEMPLATE_CATALOG: Readonly<Record<string, TemplateCatalogEntry>> = {
  // ---- Accounts & access ----
  AddInternalUser: {
    name: 'Internal user added',
    whenItFires: 'Legacy notice that an internal staff account was added.',
    group: 'accounts',
  },
  InternalUserCreated: {
    name: 'Internal user welcome (temporary password)',
    whenItFires:
      'Sent to a newly created staff member with their temporary password; they must change it at first sign-in.',
    group: 'accounts',
  },
  InviteExternalUser: {
    name: 'External user invitation',
    whenItFires:
      'Staff-issued invite for a patient, attorney, or other external party to register (carries a one-time invite link).',
    group: 'accounts',
  },
  UserRegistered: {
    name: 'Welcome - account registered',
    whenItFires: 'Greets a user right after they register an account.',
    group: 'accounts',
  },
  PasswordChange: {
    name: 'Password changed',
    whenItFires: "Confirms a user's password was changed.",
    group: 'accounts',
  },
  ResetPassword: {
    name: 'Password reset link',
    whenItFires: 'Sent when a user requests a password reset; carries the reset link.',
    group: 'accounts',
  },
  AccessorAppointmentBooked: {
    name: 'Accessor invited - new account',
    whenItFires:
      'Sent when someone added as an accessor has no account yet; carries a password-setup link.',
    group: 'accounts',
  },
  AccessorAppointmentAdded: {
    name: 'Accessor added - existing account',
    whenItFires:
      'Sent when someone added as an accessor already has an account; tells them to sign in to view the appointment.',
    group: 'accounts',
  },

  // ---- Appointment requests ----
  AppointmentBooked: {
    name: 'Appointment booked',
    whenItFires: 'Confirms a new appointment request was created.',
    group: 'request',
  },
  AppointmentRequestedOffice: {
    name: 'New request - office notice',
    whenItFires: 'Alerts the office mailbox that a new appointment request was submitted.',
    group: 'request',
  },
  AppointmentRequestedRegistered: {
    name: 'Request received - party notice',
    whenItFires:
      'Tells the patient, booker, and parties their request was received and to sign in or register to track it.',
    group: 'request',
  },
  PatientAppointmentPending: {
    name: 'Request pending (patient)',
    whenItFires: 'Notifies the patient their appointment request is pending staff review.',
    group: 'request',
  },

  // ---- Approvals & status changes ----
  AppointmentApproved: {
    name: 'Appointment approved',
    whenItFires: 'Sent when staff approve a pending appointment.',
    group: 'status',
  },
  AppointmentRejected: {
    name: 'Appointment rejected',
    whenItFires: 'Sent when staff reject a pending appointment.',
    group: 'status',
  },
  AppointmentApprovedStakeholderEmails: {
    name: 'Approved - stakeholder notice',
    whenItFires: "Notifies the appointment's stakeholders that it was approved.",
    group: 'status',
  },
  AppointmentCancelledByAdmin: {
    name: 'Cancelled by staff',
    whenItFires: 'Sent when staff cancel an appointment outright.',
    group: 'status',
  },
  AppointmentChangeLogs: {
    name: 'Appointment change summary',
    whenItFires: 'Summarizes edits made to an appointment for the office/intake.',
    group: 'status',
  },
  PatientAppointmentApproveReject: {
    name: 'Decision notice (patient)',
    whenItFires: 'Tells the patient whether their appointment was approved or rejected.',
    group: 'status',
  },
  PatientAppointmentApprovedInternal: {
    name: 'Approved - internal notice',
    whenItFires: 'Internal staff copy that an appointment was approved.',
    group: 'status',
  },
  PatientAppointmentApprovedExt: {
    name: 'Approved - external notice',
    whenItFires: 'External party copy that an appointment was approved.',
    group: 'status',
  },
  PatientAppointmentRejected: {
    name: 'Rejected (patient)',
    whenItFires: 'Tells the patient their appointment was rejected.',
    group: 'status',
  },
  PatientAppointmentCancelledNoBill: {
    name: 'Cancelled - no billing',
    whenItFires: 'Notifies that an appointment was cancelled with no billing impact.',
    group: 'status',
  },
  ClinicalStaffCancellation: {
    name: 'Cancelled by clinical staff',
    whenItFires: 'Sent when clinical staff cancel an appointment.',
    group: 'status',
  },
  AppointmentApproveRejectInternal: {
    name: 'Decision - internal notice',
    whenItFires: 'Internal staff copy of an approve/reject decision.',
    group: 'status',
  },
  AppointmentCancelledDueDate: {
    name: 'Auto-cancelled - deadline passed',
    whenItFires: 'Sent when an appointment is auto-cancelled because its decision deadline passed.',
    group: 'status',
  },
  PatientAppointmentInfoRequested: {
    name: 'More information requested (send-back)',
    whenItFires:
      'Sent to the booker when staff send an appointment back for more information (carries the staff note and a fix-it link).',
    group: 'status',
  },

  // ---- Reschedule & cancellation requests ----
  AppointmentCancelledRequest: {
    name: 'Cancellation requested',
    whenItFires: 'Acknowledges that a cancellation was requested for an appointment.',
    group: 'change',
  },
  AppointmentCancelledRequestApproved: {
    name: 'Cancellation request approved',
    whenItFires: 'Confirms a requested cancellation was approved.',
    group: 'change',
  },
  AppointmentCancelledRequestRejected: {
    name: 'Cancellation request rejected',
    whenItFires: 'Notifies that a requested cancellation was declined.',
    group: 'change',
  },
  AppointmentRescheduleRequest: {
    name: 'Reschedule requested',
    whenItFires: 'Acknowledges that a reschedule was requested for an appointment.',
    group: 'change',
  },
  AppointmentRescheduleRequestApproved: {
    name: 'Reschedule request approved',
    whenItFires: 'Confirms a requested reschedule was approved.',
    group: 'change',
  },
  AppointmentRescheduleRequestRejected: {
    name: 'Reschedule request rejected',
    whenItFires: 'Notifies that a requested reschedule was declined.',
    group: 'change',
  },
  AppointmentRescheduleRequestByAdmin: {
    name: 'Reschedule requested by staff',
    whenItFires: 'Sent when staff request a reschedule of an appointment.',
    group: 'change',
  },
  PatientAppointmentRescheduleReqAdmin: {
    name: 'Reschedule requested by staff (patient)',
    whenItFires: 'Tells the patient that staff have requested a reschedule.',
    group: 'change',
  },
  PatientAppointmentRescheduleReqApproved: {
    name: 'Reschedule approved (patient)',
    whenItFires: 'Tells the patient their reschedule request was approved.',
    group: 'change',
  },
  PatientAppointmentRescheduleReqRejected: {
    name: 'Reschedule rejected (patient)',
    whenItFires: 'Tells the patient their reschedule request was declined.',
    group: 'change',
  },
  PatientAppointmentRescheduleReq: {
    name: 'Reschedule requested (patient)',
    whenItFires: "Acknowledges the patient's reschedule request.",
    group: 'change',
  },
  PatientAppointmentCancellationApproved: {
    name: 'Cancellation approved (patient)',
    whenItFires: 'Tells the patient their cancellation request was approved.',
    group: 'change',
  },
  ChangeRequestConsentRequest: {
    name: 'Consent needed - opposing party',
    whenItFires:
      'Asks the opposing party to agree or decline a cancel/reschedule change request (links to a single-use consent page).',
    group: 'change',
  },

  // ---- Documents & packets ----
  RejectedPackageDocument: {
    name: 'Packet document rejected',
    whenItFires: 'Notifies that a submitted packet document was rejected.',
    group: 'documents',
  },
  RejectedJointDeclarationDocument: {
    name: 'Joint declaration rejected',
    whenItFires: 'Notifies that the joint declaration document was rejected.',
    group: 'documents',
  },
  PatientDocumentAccepted: {
    name: 'Document accepted',
    whenItFires: 'Confirms a submitted document was accepted.',
    group: 'documents',
  },
  PatientDocumentRejected: {
    name: 'Document rejected',
    whenItFires: 'Notifies that a submitted document was rejected.',
    group: 'documents',
  },
  PatientDocumentUploaded: {
    name: 'Document uploaded',
    whenItFires: 'Confirms a document was uploaded.',
    group: 'documents',
  },
  PatientNewDocumentAccepted: {
    name: 'New document accepted',
    whenItFires: 'Confirms a newly requested document was accepted.',
    group: 'documents',
  },
  PatientNewDocumentRejected: {
    name: 'New document rejected',
    whenItFires: 'Notifies that a newly requested document was rejected.',
    group: 'documents',
  },
  PatientNewDocumentUploaded: {
    name: 'New document uploaded',
    whenItFires: 'Confirms a newly requested document was uploaded.',
    group: 'documents',
  },
  PatientDocumentAcceptedAttachment: {
    name: 'Document accepted (with attachment)',
    whenItFires: 'Confirms a document was accepted and includes the file as an attachment.',
    group: 'documents',
  },
  PatientDocumentAcceptedRemainingDocs: {
    name: 'Document accepted - documents still outstanding',
    whenItFires: 'Confirms a document was accepted and lists the documents still outstanding.',
    group: 'documents',
  },
  PatientDocumentRejectedRemainingDocs: {
    name: 'Document rejected - documents still outstanding',
    whenItFires: 'Notifies a document was rejected and lists the documents still outstanding.',
    group: 'documents',
  },
  JointAgreementLetterAccepted: {
    name: 'Joint agreement letter accepted',
    whenItFires: 'Confirms the joint agreement letter was accepted.',
    group: 'documents',
  },
  JointAgreementLetterUploaded: {
    name: 'Joint agreement letter uploaded',
    whenItFires: 'Confirms the joint agreement letter was uploaded.',
    group: 'documents',
  },
  JointAgreementLetterRejected: {
    name: 'Joint agreement letter rejected',
    whenItFires: 'Notifies that the joint agreement letter was rejected.',
    group: 'documents',
  },
  AppointmentDocumentAddWithAttachment: {
    name: 'Appointment document added (with attachment)',
    whenItFires: 'Sent when a document is added to an appointment and included as an attachment.',
    group: 'documents',
  },

  // ---- Reminders & scheduled notices ----
  AppointmentDueDate: {
    name: 'Due-date notice',
    whenItFires: "Reminds recipients of the appointment's due date.",
    group: 'reminders',
  },
  AppointmentDueDateUploadDocumentLeft: {
    name: 'Due date - documents still to upload',
    whenItFires: 'Reminds the booker that documents remain to upload before the due date.',
    group: 'reminders',
  },
  AppointmentDueDateReminder: {
    name: 'Due-date reminder with outstanding documents',
    whenItFires:
      'Single consolidated reminder to the booker: upcoming due date plus any outstanding documents (including the joint declaration).',
    group: 'reminders',
  },
  AppointmentPendingNextDay: {
    name: 'Still pending - next-day reminder',
    whenItFires: 'Reminds staff an appointment request is still pending the next day.',
    group: 'reminders',
  },
  PendingAppointmentDailyNotification: {
    name: 'Daily pending-appointments digest',
    whenItFires: 'Daily digest of appointments still awaiting a decision.',
    group: 'reminders',
  },

  // ---- Other notices ----
  SubmitQuery: {
    name: 'Contact-us query',
    whenItFires: 'Sent when a visitor submits a contact-us / help query.',
    group: 'other',
  },
  UserQuery: {
    name: 'User query',
    whenItFires: 'Sent when a user submits a query through the portal.',
    group: 'other',
  },
  PatientAppointmentCheckedIn: {
    name: 'Patient checked in',
    whenItFires: 'Notifies that the patient checked in for their appointment.',
    group: 'other',
  },
  PatientAppointmentCheckedOut: {
    name: 'Patient checked out',
    whenItFires: 'Notifies that the patient checked out after their appointment.',
    group: 'other',
  },
  PatientAppointmentNoShow: {
    name: 'Patient no-show',
    whenItFires: 'Notifies that the patient did not show for their appointment.',
    group: 'other',
  },
};

/**
 * Mirror of the backend `NotificationTemplateConsts.Codes.All` set (64 codes as
 * of 2026-07-16, incl. `AccessorAppointmentAdded` from issue #3). Used ONLY by
 * the completeness guard in the spec -- runtime grouping reads whatever codes
 * the API returns, so it survives backend additions on its own. Keep this in
 * lockstep with the backend when a template code is added or removed.
 */
export const KNOWN_TEMPLATE_CODES: readonly string[] = [
  'AppointmentBooked',
  'AppointmentApproved',
  'AppointmentRejected',
  'AppointmentCancelledRequest',
  'AppointmentCancelledRequestApproved',
  'AppointmentCancelledRequestRejected',
  'AppointmentRescheduleRequest',
  'AppointmentRescheduleRequestApproved',
  'AppointmentRescheduleRequestRejected',
  'RejectedPackageDocument',
  'RejectedJointDeclarationDocument',
  'AppointmentDueDate',
  'AppointmentDueDateUploadDocumentLeft',
  'SubmitQuery',
  'AppointmentApprovedStakeholderEmails',
  'AppointmentCancelledByAdmin',
  'AddInternalUser',
  'PasswordChange',
  'ResetPassword',
  'UserRegistered',
  'UserQuery',
  'AppointmentRescheduleRequestByAdmin',
  'AppointmentChangeLogs',
  'PatientAppointmentPending',
  'PatientAppointmentApproveReject',
  'PatientAppointmentApprovedInternal',
  'PatientAppointmentApprovedExt',
  'PatientAppointmentRejected',
  'PatientAppointmentCheckedIn',
  'PatientAppointmentCheckedOut',
  'PatientAppointmentNoShow',
  'PatientAppointmentCancelledNoBill',
  'ClinicalStaffCancellation',
  'AccessorAppointmentBooked',
  'AccessorAppointmentAdded',
  'PatientDocumentAccepted',
  'PatientDocumentRejected',
  'PatientDocumentUploaded',
  'PatientNewDocumentAccepted',
  'PatientNewDocumentRejected',
  'PatientNewDocumentUploaded',
  'PatientDocumentAcceptedAttachment',
  'PatientDocumentAcceptedRemainingDocs',
  'PatientDocumentRejectedRemainingDocs',
  'AppointmentApproveRejectInternal',
  'AppointmentDueDateReminder',
  'AppointmentCancelledDueDate',
  'AppointmentPendingNextDay',
  'PatientAppointmentRescheduleReqAdmin',
  'PatientAppointmentRescheduleReqApproved',
  'PatientAppointmentRescheduleReqRejected',
  'PatientAppointmentCancellationApproved',
  'PatientAppointmentRescheduleReq',
  'JointAgreementLetterAccepted',
  'JointAgreementLetterUploaded',
  'JointAgreementLetterRejected',
  'AppointmentDocumentAddWithAttachment',
  'PendingAppointmentDailyNotification',
  'AppointmentRequestedOffice',
  'AppointmentRequestedRegistered',
  'InviteExternalUser',
  'InternalUserCreated',
  'ChangeRequestConsentRequest',
  'PatientAppointmentInfoRequested',
];

/** "AppointmentBooked" -> "Appointment Booked" (fallback label for uncatalogued codes). */
function humanizeCode(code: string): string {
  return (code ?? '').replace(/([a-z0-9])([A-Z])/g, '$1 $2').trim();
}

/**
 * Presentation for a code: the catalog entry, or a graceful fallback (humanized
 * name, empty blurb, "Other notices") so an un-catalogued code is still shown
 * rather than hidden.
 */
export function catalogEntryFor(code: string): TemplateCatalogEntry {
  return (
    EMAIL_TEMPLATE_CATALOG[code] ?? {
      name: humanizeCode(code) || code,
      whenItFires: '',
      group: 'other',
    }
  );
}

/**
 * True when the query matches a template's friendly name, "when it fires"
 * blurb, or raw code (case-insensitive). An empty query matches everything.
 */
export function matchesTemplateQuery(
  entry: TemplateCatalogEntry,
  code: string,
  query: string,
): boolean {
  const q = (query ?? '').trim().toLowerCase();
  if (!q) {
    return true;
  }
  return (
    entry.name.toLowerCase().includes(q) ||
    entry.whenItFires.toLowerCase().includes(q) ||
    (code ?? '').toLowerCase().includes(q)
  );
}

/** A template row paired with its resolved presentation metadata. */
export interface CatalogedTemplate<T> {
  row: T;
  entry: TemplateCatalogEntry;
}

/** One lifecycle group with its matching (name-sorted) templates. */
export interface TemplateGroupView<T> {
  key: TemplateGroupKey;
  title: string;
  items: CatalogedTemplate<T>[];
}

/**
 * Buckets template rows into lifecycle groups in {@link TEMPLATE_GROUPS} order,
 * sorting each group by friendly name and dropping empty groups. Generic over
 * any row carrying a `code`, so the component keeps its own row shape.
 */
export function groupTemplatesByLifecycle<T extends { code: string }>(
  rows: readonly T[],
): TemplateGroupView<T>[] {
  const byGroup = new Map<TemplateGroupKey, CatalogedTemplate<T>[]>();
  for (const row of rows) {
    const entry = catalogEntryFor(row.code);
    const list = byGroup.get(entry.group) ?? [];
    list.push({ row, entry });
    byGroup.set(entry.group, list);
  }
  const views: TemplateGroupView<T>[] = [];
  for (const group of TEMPLATE_GROUPS) {
    const items = byGroup.get(group.key);
    if (!items || items.length === 0) {
      continue;
    }
    items.sort((a, b) => a.entry.name.localeCompare(b.entry.name));
    views.push({ key: group.key, title: group.title, items });
  }
  return views;
}
