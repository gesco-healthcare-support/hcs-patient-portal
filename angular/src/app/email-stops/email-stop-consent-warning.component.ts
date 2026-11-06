import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { LocalizationPipe } from '@abp/ng.core';
import type { EmailStopConsentImpactDto } from '../proxy/email-stops';
import { distinctSides, otherCaseNumbers, sideKey } from './email-stops.util';

/**
 * The "this person is the only one who can answer" warning. It only warns; it never blocks the
 * stop. Shown by the case dialog and by the Stopped emails page.
 */
@Component({
  selector: 'app-email-stop-consent-warning',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizationPipe],
  template: `
    @if (impact(); as imp) {
      @if (imp.hasImpact) {
        <div class="es-warn" role="alert" data-testid="consent-warning">
          @for (side of sides(); track side) {
            <p>
              {{
                '::EmailStops:Warn:ThisCase'
                  | abpLocalization: email() : (sideLabelKey(side) | abpLocalization)
              }}
            </p>
          }
          @if (imp.otherCases.length > 0) {
            <p>
              {{
                '::EmailStops:Warn:OtherCases'
                  | abpLocalization: imp.otherCases.length.toString() : caseNumbers()
              }}
            </p>
          }
        </div>
      }
    }
  `,
  styles: [
    `
      .es-warn {
        background: #fff7e0;
        border: 1px solid #e0b84a;
        border-radius: 8px;
        padding: 10px 14px;
        font-size: 13px;
        line-height: 19px;
        margin-top: 12px;
      }
      .es-warn p {
        margin: 0 0 6px;
      }
      .es-warn p:last-child {
        margin-bottom: 0;
      }
    `,
  ],
})
export class EmailStopConsentWarningComponent {
  readonly impact = input<EmailStopConsentImpactDto | null>(null);
  readonly email = input('');

  protected readonly sides = computed(() => {
    const imp = this.impact();
    return imp ? distinctSides(imp) : [];
  });
  protected readonly caseNumbers = computed(() => {
    const imp = this.impact();
    return imp ? otherCaseNumbers(imp) : '';
  });
  protected readonly sideLabelKey = sideKey;
}
