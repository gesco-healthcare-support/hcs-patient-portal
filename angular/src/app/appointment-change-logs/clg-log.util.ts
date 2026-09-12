import type { AppointmentChangeLogDto } from '../proxy/appointment-change-logs/models';

/**
 * Pure helpers for the change-log timeline (Prompt 13). The service returns
 * PHI-redacted per-field rows; the timeline groups them into one entry per save
 * (an ABP EntityChange explodes to several property rows that share the
 * appointment + entity + change type + timestamp). Standalone functions so they
 * unit-test without a TestBed.
 */

export interface ClgEntry {
  key: string;
  entityType: string;
  changeType: string;
  changeTime: string | null | undefined;
  appointmentId: string | null | undefined;
  diffs: AppointmentChangeLogDto[];
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
export function groupChangeLogEntries(rows: readonly AppointmentChangeLogDto[]): ClgEntry[] {
  const byKey = new Map<string, ClgEntry>();
  for (const row of rows) {
    const time = row.changeTime ?? '';
    const key = [row.appointmentId ?? '', row.entityType ?? '', row.changeType ?? '', time].join(
      '|',
    );
    let entry = byKey.get(key);
    if (!entry) {
      entry = {
        key,
        entityType: row.entityType ?? '',
        changeType: row.changeType ?? '',
        changeTime: row.changeTime,
        appointmentId: row.appointmentId,
        diffs: [],
      };
      byKey.set(key, entry);
    }
    entry.diffs.push(row);
  }
  return [...byKey.values()];
}
