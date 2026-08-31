import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { IconComponent } from '../../../shared/ui/icon/icon.component';
import { PacificDatePipe } from '../../../shared/pipes/pacific-date.pipe';
import type { RescheduleChainStep, RescheduleChainStepKind } from './reschedule-chain.util';

/**
 * The "Rescheduled from A000xx" block (phase 4d, 2026-08-05), shared by the internal and external
 * appointment detail pages.
 *
 * <p>Extracted 2026-08-07: the block was originally pasted into both templates, which SonarCloud
 * correctly flagged as ~90 duplicated lines. Two identical blocks that must be kept in step is the
 * same failure mode as bug F18 -- change one, forget the other -- so the markup belongs in one
 * place, as the derivation already does (reschedule-chain.util.ts).</p>
 *
 * <p>Presentational only: it owns the open/closed state of its own disclosure and the caption
 * wording, and reports the "open the source" click upward. It performs no lookups.</p>
 */
@Component({
  selector: 'app-reschedule-chain-note',
  standalone: true,
  imports: [CommonModule, IconComponent, PacificDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="ad-note ad-note--reschedule">
      <span class="ic"><app-icon name="calendar" [size]="18" /></span>
      <div class="ad-note__body">
        <b>Rescheduled from {{ sourceLabel }}</b>
        <p>Both sides agreed to this date before it was booked.</p>
        <div class="ad-note__actions">
          @if (canOpenSource) {
            <button type="button" class="ad-note__link" (click)="openSource.emit()">
              View the original appointment
            </button>
          }
          @if (steps.length) {
            <button
              type="button"
              class="ad-note__toggle"
              [attr.aria-expanded]="historyOpen"
              (click)="toggleHistory()"
            >
              Reschedule history
              <app-icon name="chevDown" [size]="14" class="ad-chev" [class.is-open]="historyOpen" />
            </button>
          }
        </div>
        @if (historyOpen) {
          <dl class="ad-note__steps">
            @for (step of steps; track step.kind) {
              <dt>{{ stepLabel(step.kind) }}</dt>
              <dd>
                {{ step.at | pacificDate: 'MMM d, y' }} &middot;
                {{ step.at | pacificDate: 'h:mm a' }}
              </dd>
            }
          </dl>
        }
      </div>
    </div>
  `,
})
export class RescheduleChainNoteComponent {
  /** The replaced appointment's confirmation number, e.g. `A00003`. */
  @Input({ required: true }) sourceLabel!: string | null;

  /** Whether the source can be navigated to. */
  @Input() canOpenSource = false;

  /**
   * Already-derived steps. The caller MUST pass a stable array reference -- see the memoization on
   * `AppointmentViewComponent.rescheduleChainSteps`. A getter that allocates per change-detection
   * pass is what hung the tab for two hours in phase 4b.
   */
  @Input({ required: true }) steps!: RescheduleChainStep[];

  @Output() readonly openSource = new EventEmitter<void>();

  /** Collapsed by default: the timestamps are audit detail, not what the page leads with. */
  protected historyOpen = false;

  protected toggleHistory(): void {
    this.historyOpen = !this.historyOpen;
  }

  protected stepLabel(kind: RescheduleChainStepKind): string {
    switch (kind) {
      case 'side-a-agreed':
        return 'Patient side agreed';
      case 'side-b-agreed':
        return 'Defense side agreed';
      default:
        return 'Finalized by staff';
    }
  }
}
