import type { AppointmentChangeLogDto } from '../proxy/appointment-change-logs/models';

/**
 * Pure helpers for the change-log timeline (Prompt 13). The service returns
 * PHI-redacted per-field rows; the timeline groups them into one entry per save
 * (an ABP EntityChange explodes to several property rows that share the
 * appointment + entity + change type + timestamp). Standalone functions so they
 * unit-test without a TestBed.
 */

/**
 * A change-log row as the server now sends it. The generated proxy model (never edited by hand) predates
 * the who / action fields, so they are declared here as optional extras; a proxy regeneration will
 * make them redundant and this type keeps compiling because it only widens the proxy type.
 */
export interface ChangeLogRow extends AppointmentChangeLogDto {
  /** Readable action for the whole change: "Booked", "Approved", "Document rejected", ... */
  actionLabel?: string | null;
  /** The person (or system) that made the change. */
  changedByName?: string | null;
  /** Staff | HostStaff | Outside | System | CaseTracker | Party. */
  changedByKind?: string | null;
}

export interface ClgEntry {
  key: string;
  entityType: string;
  changeType: string;
  changeTime: string | null | undefined;
  appointmentId: string | null | undefined;
  actionLabel: string;
  changedByName: string;
  changedByKind: string;
  /** Rows with a field name; an event with no recorded fields has none. */
  fieldCount: number;
  diffs: ChangeLogRow[];
}

/** Small hint shown after a name; staff need none, they are the expected case. */
export function whoHint(kind: string | null | undefined): string {
  switch (kind) {
    case 'HostStaff':
      return 'host staff';
    case 'Outside':
      return 'outside';
    default:
      return '';
  }
}

/** System-like actors render muted: they are not a person. */
export function whoIsMuted(kind: string | null | undefined): boolean {
  return kind === 'System' || kind === 'CaseTracker' || kind === 'Party';
}

export interface ClgKindMeta {
  icon: string;
  tint: string;
  verb: string;
}

/** Icon + tint + verb for a change type (Created/Updated/Deleted, case-insensitive). */
export function changeTypeMeta(changeType: string | null | undefined): ClgKindMeta {
  switch ((changeType ?? '').toLowerCase()) {
    case 'created':
    case 'add':
      return { icon: 'plus', tint: 'tint-green', verb: 'added' };
    case 'deleted':
    case 'delete':
      return { icon: 'x', tint: 'tint-red', verb: 'removed' };
    default:
      return { icon: 'refresh', tint: 'tint-blue', verb: 'updated' };
  }
}

/**
 * Group per-field rows into one entry per save, keyed by
 * appointment + entity + change type + timestamp. First-seen order is preserved
 * (the server returns newest-first), so the timeline stays chronological.
 */
export function groupChangeLogEntries(rows: readonly ChangeLogRow[]): ClgEntry[] {
  const byKey = new Map<string, ClgEntry>();
  for (const row of rows) {
    const time = row.changeTime ?? '';
    const key = [
      row.appointmentId ?? '',
      row.entityType ?? '',
      row.changeType ?? '',
      row.actionLabel ?? '',
      time,
    ].join('|');
    let entry = byKey.get(key);
    if (!entry) {
      entry = {
        key,
        entityType: row.entityType ?? '',
        changeType: row.changeType ?? '',
        changeTime: row.changeTime,
        appointmentId: row.appointmentId,
        actionLabel: row.actionLabel ?? '',
        changedByName: row.changedByName ?? '',
        changedByKind: row.changedByKind ?? '',
        fieldCount: 0,
        diffs: [],
      };
      byKey.set(key, entry);
    }
    entry.diffs.push(row);
    if (row.propertyName) {
      entry.fieldCount += 1;
    }
  }
  return [...byKey.values()];
}
