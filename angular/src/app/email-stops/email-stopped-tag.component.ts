import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { LocalizationPipe } from '@abp/ng.core';
import { isStoppedIn } from './email-stops.util';

/** Small "Emails stopped" tag shown beside a party's or Authorized User's email when it is stopped. */
@Component({
  selector: 'app-email-stopped-tag',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizationPipe],
  template: `
    @if (stopped()) {
      <span class="es-tag" data-testid="emails-stopped-tag">
        {{ '::EmailStops:Tag:Stopped' | abpLocalization }}
      </span>
    }
  `,
  styles: [
    `
      .es-tag {
        display: inline-block;
        margin-left: 6px;
        padding: 1px 8px;
        border-radius: 999px;
        background: #f9d6d6;
        color: #8a2222;
        font-size: 11px;
        font-weight: 600;
      }
    `,
  ],
})
export class EmailStoppedTagComponent {
  readonly email = input<string | null | undefined>(null);
  readonly stoppedSet = input<ReadonlySet<string>>(new Set());

  protected readonly stopped = computed(() => isStoppedIn(this.stoppedSet(), this.email()));
}
