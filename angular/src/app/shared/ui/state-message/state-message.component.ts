import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { IconComponent } from '../icon/icon.component';
import { IconName } from '../icon/icon.registry';

/** Tone keys map to the `.pp-ic` color variants in `_pp-shell.scss`. */
export type StateMessageTone = 'blue' | 'amber' | 'red' | 'green';

/**
 * One action button on a state-message screen. Provide EITHER `routerLink`
 * (renders an anchor) OR `click` (renders a button). `kind` picks the ap-btn
 * variant and defaults to primary.
 */
export interface StateMessageAction {
  label: string;
  icon?: IconName;
  routerLink?: string | unknown[];
  click?: () => void;
  kind?: 'primary' | 'ghost';
}

/**
 * Shared presentational primitive for the redesigned message-type state
 * screens (error, 403, 404, session-timeout, offline). Renders the branded
 * centered card (the `pp-*` shell) on a chrome-less layout, so the SAME
 * component serves both the external and internal surfaces.
 *
 * Purely presentational: tone, icon, copy, and actions all arrive as inputs.
 * Brand slots (logo, clinic name) are static placeholders matching
 * AppExternalNavbar until a BrandingAppService lands (see
 * external-navbar.component.ts); the footer support line uses a generic
 * fallback until branding can supply a real contact.
 */
@Component({
  selector: 'app-state-message',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, IconComponent],
  template: `
    <div class="pp">
      <div class="pp-top">
        <img [src]="logoUrl" [alt]="clinicName + ' logo'" />
        <div class="tag">
          <b>{{ clinicName }}</b>
          &middot; {{ tagline }}
        </div>
      </div>

      <div class="pp-main">
        <div class="pp-card">
          <div [class]="'pp-ic ' + tone">
            <app-icon [name]="icon" [size]="30" />
          </div>
          <h1>{{ title }}</h1>
          <p class="lead">{{ lead }}</p>

          @if (actions.length) {
            <div class="pp-actions" [class.pp-actions--single]="actions.length === 1">
              @for (action of actions; track action.label) {
                @if (action.routerLink) {
                  <a
                    class="ap-btn pp-btn-lg"
                    [class.ap-btn--ghost]="action.kind === 'ghost'"
                    [class.ap-btn--primary]="action.kind !== 'ghost'"
                    [routerLink]="action.routerLink"
                  >
                    @if (action.icon) {
                      <app-icon [name]="action.icon" [size]="16" />
                    }
                    {{ action.label }}
                  </a>
                } @else {
                  <button
                    type="button"
                    class="ap-btn pp-btn-lg"
                    [class.ap-btn--ghost]="action.kind === 'ghost'"
                    [class.ap-btn--primary]="action.kind !== 'ghost'"
                    (click)="action.click?.()"
                  >
                    @if (action.icon) {
                      <app-icon [name]="action.icon" [size]="16" />
                    }
                    {{ action.label }}
                  </button>
                }
              }
            </div>
          }
        </div>
      </div>

      <div class="pp-foot">{{ supportText }}</div>
    </div>
  `,
})
export class StateMessageComponent {
  /** Icon-badge tone; selects the `.pp-ic` color variant. */
  @Input() tone: StateMessageTone = 'blue';
  /** Line-icon name rendered in the badge. */
  @Input({ required: true }) icon!: IconName;
  /** Headline. */
  @Input() title = '';
  /** Supporting paragraph under the headline. */
  @Input() lead = '';
  /** Zero or more action buttons; one action centers, two split the row. */
  @Input() actions: StateMessageAction[] = [];

  // Brand slots -- static placeholders matching AppExternalNavbar until a
  // BrandingAppService lands (see external-navbar.component.ts).
  @Input() logoUrl = 'assets/branding/falkinstein-logo.png';
  @Input() clinicName = 'Appointment Portal';
  @Input() tagline = 'patient & case portal';
  @Input() supportText = 'Need help? Contact your clinic.';
}
