import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

import { IconComponent } from '../../../shared/ui/icon/icon.component';
import type { AppointmentInfoRequestRoundDto } from '../../../proxy/appointment-info-requests/models';
import {
  changedRows,
  fixedSummary,
  flaggedSummary,
  notePreview,
  type DiffRow,
} from './send-back-history.util';

/**
 * Presentational list of Send Back / request-info rounds for one appointment.
 * Shared by the internal Change-log page (full per-field diffs) and the external
 * appointment-detail page (lighter summary: counts only, no field values). Rounds
 * arrive newest-first from GetHistoryAsync; the host page loads them and passes
 * them in, so this component has no DI and is unit-testable in isolation.
 *
 * `externalView` is the only behavioural switch:
 *  - false (internal): renders each round's CHANGED field diffs (old -&gt; new).
 *  - true (external): omits diffs and renders the staff requester as "HCS staff",
 *    so no field values or staff identity leak to external parties. SSN values are
 *    already masked in the snapshot, and documents are excluded server-side.
 */
@Component({
  selector: 'app-info-request-history',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, IconComponent],
  templateUrl: './info-request-history.component.html',
  styleUrl: './info-request-history.component.scss',
})
export class InfoRequestHistoryComponent {
  /** Rounds for one appointment, newest-first (as returned by GetHistoryAsync). */
  @Input() rounds: AppointmentInfoRequestRoundDto[] = [];

  /** External chrome: hide field-level diffs and genericise the staff requester. */
  @Input() externalView = false;

  /** Requester label: real staff name internally, genericised externally. */
  protected requesterName(round: AppointmentInfoRequestRoundDto): string {
    return this.externalView ? 'HCS staff' : round.requestedByName || 'staff';
  }

  /** Resubmitter is the external party themselves -- shown as-is on both chromes. */
  protected resubmitterName(round: AppointmentInfoRequestRoundDto): string {
    return round.resubmittedByName || 'the requester';
  }

  protected fixedSummary(round: AppointmentInfoRequestRoundDto): string {
    return fixedSummary(round);
  }

  protected flaggedSummary(round: AppointmentInfoRequestRoundDto): string {
    return flaggedSummary(round);
  }

  protected notePreview(note?: string | null): string {
    return notePreview(note);
  }

  /** Changed field diffs for a round; empty in external view (never rendered). */
  protected diffRows(round: AppointmentInfoRequestRoundDto): DiffRow[] {
    return this.externalView ? [] : changedRows(round);
  }
}
