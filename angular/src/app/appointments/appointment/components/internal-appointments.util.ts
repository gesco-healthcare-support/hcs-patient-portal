import { AppointmentStatusType } from '../../../proxy/enums/appointment-status-type.enum';
import type { AppointmentStatusCountDto } from '../../../proxy/appointments/models';
import type { AppointmentPillStatus } from '../../../shared/ui/status-pill/status-pill.component';
import {
  appointmentStatusToPill,
  appointmentStatusToSegment,
  type ExternalStatusSegment,
} from '../../../shared/ui/status-pill/appointment-status.util';

/**
 * Pure helpers for the redesigned internal appointments list (Prompt 10). Kept
 * out of the component so the row-action gating + decide-by urgency mapping are
 * unit-testable without Angular's DI graph, and so all pill bucketing flows
 * through the single source of truth (appointment-status.util) -- the same map
 * the backend status-counts endpoint is aggregated against.
 */

/**
 * Staff decision SLA in days: a Pending request must be decided within this
 * window of its creation. Mirrors the legacy AppointmentComponent.decisionDueDays
 * and the server default SystemParameter.PendingAppointmentOverDueNotificationDays
 * (3); the dashboard deadline tile is the server-authoritative surface.
 */
export const DECISION_DUE_DAYS = 3;

/**
 * Pills whose rows expose Reschedule / Cancel in the kebab. Matches the
 * prototype's `actionable` set (Pending / Info Requested / Approved /
 * Rescheduled); terminal Cancelled / Rejected rows are not actionable.
 */
const ACTIONABLE_PILLS: ReadonlySet<AppointmentPillStatus> = new Set<AppointmentPillStatus>([
  'Pending',
  'InfoRequested',
  'Approved',
  'Rescheduled',
]);

/** True when a row (by its pill) can be rescheduled / cancelled from the kebab. */
export function isRowActionable(pill: AppointmentPillStatus): boolean {
  return ACTIONABLE_PILLS.has(pill);
}

/**
 * Raw status -> chip segment, derived once from appointmentStatusToSegment so a
 * single UI pill's full status set (e.g. Cancelled spans NoBill / Late /
 * Requested / NoShow) can be sent to the list's AppointmentStatuses filter.
 */
const SEGMENT_STATUSES: Record<
  Exclude<ExternalStatusSegment, 'all'>,
  AppointmentStatusType[]
> = (() => {
  const map: Record<Exclude<ExternalStatusSegment, 'all'>, AppointmentStatusType[]> = {
    pending: [],
    info: [],
    approved: [],
    rescheduled: [],
    cancelled: [],
    rejected: [],
  };
  for (const value of Object.values(AppointmentStatusType)) {
    if (typeof value === 'number') {
      map[appointmentStatusToSegment(value)].push(value);
    }
  }
  return map;
})();

/**
 * The raw statuses a chip filters by. 'all' returns an empty array (no status
 * filter -> the list shows every row).
 */
export function segmentStatuses(segment: ExternalStatusSegment): AppointmentStatusType[] {
  return segment === 'all' ? [] : SEGMENT_STATUSES[segment];
}

/** Per-chip counts (incl. 'all'), bucketing the backend's raw per-status counts. */
export function bucketChipCounts(
  rawCounts: ReadonlyArray<AppointmentStatusCountDto>,
): Record<ExternalStatusSegment, number> {
  const out: Record<ExternalStatusSegment, number> = {
    all: 0,
    pending: 0,
    info: 0,
    approved: 0,
    rescheduled: 0,
    cancelled: 0,
    rejected: 0,
  };
  for (const row of rawCounts) {
    const count = row.count ?? 0;
    if (row.status == null) {
      continue;
    }
    out.all += count;
    out[appointmentStatusToSegment(row.status)] += count;
  }
  return out;
}

/** The pill for a row's status (Pending fallback for an absent status). */
export function pillOfStatus(status?: AppointmentStatusType): AppointmentPillStatus {
  return appointmentStatusToPill(status ?? AppointmentStatusType.Pending);
}

/** Resolved decide-by deadline for a Pending row: due date + urgency label. */
export interface DecideBy {
  due: Date;
  daysLeft: number;
  /** Short countdown chip: 'past' | 'today' | 'Nd'. */
  label: string;
  /** Urgency level driving the chip color. */
  level: 'ok' | 'warn' | 'crit';
}

/**
 * The decision deadline for a Pending request, or null when there is no
 * creation timestamp. Due = creation date (midnight) + DECISION_DUE_DAYS;
 * daysLeft counts whole days from today's midnight. Urgency matches the
 * prototype: <= 1 day crit, <= 2 warn, else ok.
 */
export function decideByInfo(
  creationTime: string | Date | null | undefined,
  now: Date,
  decisionDueDays: number = DECISION_DUE_DAYS,
): DecideBy | null {
  if (!creationTime) {
    return null;
  }
  const created = creationTime instanceof Date ? creationTime : new Date(creationTime);
  if (Number.isNaN(created.getTime())) {
    return null;
  }
  const due = new Date(
    created.getFullYear(),
    created.getMonth(),
    created.getDate() + decisionDueDays,
  );
  const todayMidnight = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const daysLeft = Math.round((due.getTime() - todayMidnight.getTime()) / 86_400_000);
  const level = daysLeft <= 1 ? 'crit' : daysLeft <= 2 ? 'warn' : 'ok';
  const label = daysLeft < 0 ? 'past' : daysLeft === 0 ? 'today' : `${daysLeft}d`;
  return { due, daysLeft, label, level };
}

/** Two-letter avatar initials from a name (first + last initial, uppercased). */
export function avatarInitials(firstName?: string | null, lastName?: string | null): string {
  const first = (firstName ?? '').trim();
  const last = (lastName ?? '').trim();
  const initials = `${first.charAt(0)}${last.charAt(0)}`.toUpperCase();
  return initials || '?';
}

/** Deterministic avatar background color from a seed (stable per name). */
export function avatarColor(seed: string): string {
  let hash = 0;
  for (let i = 0; i < seed.length; i++) {
    hash = (hash * 31 + seed.charCodeAt(i)) >>> 0;
  }
  return `hsl(${hash % 360} 52% 42%)`;
}

/**
 * Serializes a header row + body matrix to RFC-4180 CSV text (fields quoted,
 * embedded quotes doubled, CRLF line breaks). ASCII output; callers pre-strip
 * any non-ASCII from cell values.
 */
export function toCsvContent(
  headers: ReadonlyArray<string>,
  rows: ReadonlyArray<ReadonlyArray<string>>,
): string {
  const escapeCell = (value: string): string => `"${(value ?? '').replace(/"/g, '""')}"`;
  const lines = [headers, ...rows].map((cells) => cells.map(escapeCell).join(','));
  return lines.join('\r\n');
}
