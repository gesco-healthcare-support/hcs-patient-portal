import { FLAGGABLE_FIELDS } from '../send-back-fields';
import type {
  AppointmentInfoRequestRoundDto,
  InfoRequestFieldDiffDto,
} from '../../../proxy/appointment-info-requests/models';

/**
 * Pure helpers for the staff Send Back review (Branch 2): turning the history
 * rounds returned by the API into the "What changed since your request" diff rows
 * and the "Request history" timeline. Kept out of the component so the label
 * mapping + summaries are unit-testable without Angular's DI graph.
 *
 * The backend diff carries only the field KEY (lean DTO); labels live in the
 * shared send-back-fields registry, so the mapping happens here. SSN values are
 * already masked in the snapshot; documents are excluded server-side.
 */

const LABEL_BY_KEY = new Map(FLAGGABLE_FIELDS.map((f) => [f.key, f.label]));

/** One rendered diff row: field label + masked/formatted old and new values. */
export interface DiffRow {
  label: string;
  oldValue: string;
  newValue: string;
}

/** Human label for a diff key, falling back to the raw key when unknown. */
export function fieldLabel(key: string | undefined | null): string {
  return (key && LABEL_BY_KEY.get(key)) || key || '';
}

/** Rounds arrive newest-first; the most recent round (or null when none). */
export function latestRound(
  rounds: AppointmentInfoRequestRoundDto[] | null | undefined,
): AppointmentInfoRequestRoundDto | null {
  return rounds && rounds.length > 0 ? rounds[0] : null;
}

/** True when the latest round was resubmitted (drives the "Resubmitted" badge). */
export function wasResubmitted(
  rounds: AppointmentInfoRequestRoundDto[] | null | undefined,
): boolean {
  return !!latestRound(rounds)?.isResolved;
}

/** The CHANGED diff rows for a round (the "What changed" card shows only these). */
export function changedRows(round: AppointmentInfoRequestRoundDto | null | undefined): DiffRow[] {
  const diffs: InfoRequestFieldDiffDto[] = round?.diffs ?? [];
  return diffs
    .filter((d) => d.changed)
    .map((d) => ({
      label: fieldLabel(d.key),
      oldValue: d.oldValue ?? '',
      newValue: d.newValue ?? '',
    }));
}

/** "N of M flagged item(s) fixed" summary for a resolved round. */
export function fixedSummary(round: AppointmentInfoRequestRoundDto): string {
  const fixed = round.fixedCount ?? 0;
  const flagged = round.flaggedCount ?? 0;
  return `${fixed} of ${flagged} flagged item${flagged === 1 ? '' : 's'} fixed`;
}

/** "N field(s) flagged" summary for the info-requested line. */
export function flaggedSummary(round: AppointmentInfoRequestRoundDto): string {
  const flagged = round.flaggedCount ?? 0;
  return `${flagged} field${flagged === 1 ? '' : 's'} flagged`;
}

/** Truncated note preview for the history timeline. */
export function notePreview(note: string | undefined | null, max = 80): string {
  if (!note) {
    return '';
  }
  return note.length > max ? `${note.slice(0, max)}...` : note;
}
