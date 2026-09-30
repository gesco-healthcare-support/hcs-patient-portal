import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { IconComponent } from '../icon/icon.component';
import { IconName } from '../icon/icon.registry';

/**
 * In-context empty state (the `.st-empty` block from the design handoff): a
 * centered icon, headline, body, and an optional call-to-action button. Shared
 * across list pages; the host page supplies copy and handles `ctaClick`.
 */
@Component({
  selector: 'app-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [IconComponent],
  template: `
    <div class="st-empty">
      <div class="st-empty__ic"><app-icon [name]="icon" [size]="36" /></div>
      <h2>{{ title }}</h2>
      <p>{{ body }}</p>
      @if (ctaLabel) {
        <button type="button" class="ap-btn ap-btn--primary" (click)="ctaClick.emit()">
          @if (ctaIcon) {
            <app-icon [name]="ctaIcon" [size]="16" />
          }
          {{ ctaLabel }}
        </button>
      }
    </div>
  `,
  styles: `
    .st-empty {
      text-align: center;
      padding: 56px 24px;
      max-width: 460px;
      margin: 0 auto;
    }
    .st-empty__ic {
      width: 80px;
      height: 80px;
      border-radius: 24px;
      background: var(--blue-50);
      color: var(--blue-600);
      display: flex;
      align-items: center;
      justify-content: center;
      margin: 0 auto 20px;
    }
    .st-empty h2 {
      font-size: 20px;
      font-weight: 800;
      letter-spacing: -0.01em;
      color: var(--n-900);
      margin: 0 0 8px;
    }
    .st-empty p {
      font-size: 14px;
      color: var(--n-500);
      line-height: 1.55;
      margin: 0 0 22px;
    }
  `,
})
export class EmptyStateComponent {
  /** Centered icon name. */
  @Input({ required: true }) icon!: IconName;
  @Input() title = '';
  @Input() body = '';
  /** Optional CTA button label; the button renders only when set. */
  @Input() ctaLabel?: string;
  @Input() ctaIcon?: IconName;
  @Output() ctaClick = new EventEmitter<void>();
}
