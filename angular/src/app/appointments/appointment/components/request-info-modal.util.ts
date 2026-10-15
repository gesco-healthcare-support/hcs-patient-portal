import type {
  FlaggedFieldDto,
  SendBackAppointmentInput,
} from '../../../proxy/appointment-info-requests/models';

/**
 * True when staff may submit a send-back: at least one field flagged AND a
 * non-blank note. Mirrors the prototype's disable rule (count === 0 ||
 * !note.trim()). Pure so the gate is unit-testable without the component/CD.
 */
export function canSendBack(selectedCount: number, note: string): boolean {
  return selectedCount > 0 && note.trim().length > 0;
}

/**
 * Build the send-back payload from the modal's selection state: trims the note,
 * emits one FlaggedFieldDto per selected key, and carries the optional per-field
 * hint (trimmed; null when empty) so the requester sees it on the fix-it page.
 */
export function buildSendBackInput(
  selectedKeys: string[],
  hints: Record<string, string>,
  note: string,
): SendBackAppointmentInput {
  const flaggedFields: FlaggedFieldDto[] = selectedKeys.map((key) => {
    const hint = (hints[key] ?? '').trim();
    return { key, hint: hint === '' ? null : hint };
  });
  return { note: note.trim(), flaggedFields };
}
