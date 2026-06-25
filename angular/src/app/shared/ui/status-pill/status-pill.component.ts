import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

/** The 6-status model surfaced on lists/detail (semantic keys, not numeric ids). */
export type AppointmentPillStatus =
  | 'Pending'
  | 'InfoRequested'
  | 'Approved'
  | 'Rejected'
  | 'Cancelled'
  | 'Rescheduled';

type PillTone = 'pending' | 'purple' | 'approved' | 'rejected' | 'neutral' | 'info';

/**
 * Status -> { tone, default label } map.
 *
 * Tones come from the handoff data layer (data.js / ad-data.js), NOT the detail
 * banner: in list/pill context Cancelled is `neutral` (grey), while the detail
 * status banner paints Cancelled red -- a deliberate, context-specific choice.
 * Rescheduled is `info` (blue); InfoRequested is `purple`.
 */
const PILL_META: Record<AppointmentPillStatus, { tone: PillTone; label: string }> = {
  Pending: { tone: 'pending', label: 'Pending' },
  InfoRequested: { tone: 'purple', label: 'Info Requested' },
  Approved: { tone: 'approved', label: 'Approved' },
  Rejected: { tone: 'rejected', label: 'Rejected' },
  Cancelled: { tone: 'neutral', label: 'Cancelled' },
  Rescheduled: { tone: 'info', label: 'Rescheduled' },
};

/**
 * Status pill: a tinted, text-bearing chip with a leading dot. Always renders
 * both the dot AND a text label -- never color-alone (accessibility).
 *
 * Usage: `<app-status-pill status="InfoRequested" />` or, to override the copy,
 * `<app-status-pill status="Rescheduled" [label]="localizedName" />`.
 */
@Component({
  selector: 'app-status-pill',
  standalone: true,
  template: `
    <span [class]="pillClass">
      <span class="app-status-pill__dot" aria-hidden="true"></span>
      <span class="app-status-pill__label">{{ displayLabel }}</span>
    </span>
  `,
  styles: `
    .app-status-pill {
      display: inline-flex;
      align-items: center;
      gap: 7px;
      padding: 4px 11px 4px 9px;
      border-radius: var(--r-pill);
      font-family: var(--font);
      font-size: 12px;
      font-weight: 700;
      line-height: 1.2;
      white-space: nowrap;
    }
    .app-status-pill__dot {
      width: 7px;
      height: 7px;
      border-radius: 50%;
      flex: none;
    }
    .app-status-pill--pending {
      background: var(--st-pending-bg);
      color: var(--st-pending-fg);
    }
    .app-status-pill--pending .app-status-pill__dot {
      background: var(--st-pending-dot);
    }
    .app-status-pill--approved {
      background: var(--st-approved-bg);
      color: var(--st-approved-fg);
    }
    .app-status-pill--approved .app-status-pill__dot {
      background: var(--st-approved-dot);
    }
    .app-status-pill--rejected {
      background: var(--st-rejected-bg);
      color: var(--st-rejected-fg);
    }
    .app-status-pill--rejected .app-status-pill__dot {
      background: var(--st-rejected-dot);
    }
    .app-status-pill--neutral {
      background: var(--st-neutral-bg);
      color: var(--st-neutral-fg);
    }
    .app-status-pill--neutral .app-status-pill__dot {
      background: var(--st-neutral-dot);
    }
    .app-status-pill--info {
      background: var(--st-info-bg);
      color: var(--st-info-fg);
    }
    .app-status-pill--info .app-status-pill__dot {
      background: var(--st-info-dot);
    }
    .app-status-pill--purple {
      background: var(--st-purple-bg);
      color: var(--st-purple-fg);
    }
    .app-status-pill--purple .app-status-pill__dot {
      background: var(--st-purple-dot);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusPillComponent {
  /** Semantic status key. */
  @Input({ required: true }) status!: AppointmentPillStatus;

  /** Optional label override (e.g. a localized status name). */
  @Input() label?: string;

  protected get pillClass(): string {
    return `app-status-pill app-status-pill--${PILL_META[this.status].tone}`;
  }

  protected get displayLabel(): string {
    return this.label ?? PILL_META[this.status].label;
  }
}
