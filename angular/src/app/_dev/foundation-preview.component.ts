import { ChangeDetectionStrategy, Component } from '@angular/core';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { ICON_NAMES } from '../shared/ui/icon/icon.registry';
import {
  AppointmentPillStatus,
  StatusPillComponent,
} from '../shared/ui/status-pill/status-pill.component';

/**
 * THROWAWAY foundation preview (slice 1 verification surface).
 *
 * Renders the full token palette, every icon, all status pills, and the ap-
 * utility primitives so the redesign foundation can be eyeballed against
 * design_handoff_appointment_portal/styles/tokens.css. Reached at
 * /foundation-preview (empty layout, no guard). DELETE this component and its
 * route after sign-off -- it is the "old component" this slice retires.
 */
@Component({
  selector: 'app-foundation-preview',
  standalone: true,
  imports: [IconComponent, StatusPillComponent],
  template: `
    <div class="fp">
      <header class="fp__hd">
        <h1>Redesign foundation preview</h1>
        <p>Tokens, icons, status pills, and ap- primitives. Throwaway verification surface.</p>
      </header>

      <section class="fp__sec">
        <h2>Color ramps</h2>
        @for (ramp of ramps; track ramp.name) {
          <h3>{{ ramp.name }}</h3>
          <div class="fp__swatches">
            @for (t of ramp.tokens; track t) {
              <div class="fp__swatch">
                <span class="fp__chip" [style.background]="'var(' + t + ')'"></span>
                <code>{{ t }}</code>
              </div>
            }
          </div>
        }
      </section>

      <section class="fp__sec">
        <h2>Status tones (--st-*)</h2>
        <div class="fp__swatches">
          @for (tone of statusTones; track tone) {
            <div class="fp__swatch">
              <span
                class="fp__chip fp__chip--bordered"
                [style.background]="'var(--st-' + tone + '-bg)'"
              ></span>
              <code>{{ tone }}</code>
            </div>
          }
        </div>
      </section>

      <section class="fp__sec">
        <h2>Status pills</h2>
        <div class="fp__row">
          @for (s of statuses; track s) {
            <app-status-pill [status]="s" />
          }
        </div>
      </section>

      <section class="fp__sec">
        <h2>Icons ({{ icons.length }})</h2>
        <div class="fp__icons">
          @for (name of icons; track name) {
            <div class="fp__icon">
              <app-icon [name]="name" [size]="22" />
              <code>{{ name }}</code>
            </div>
          }
        </div>
      </section>

      <section class="fp__sec">
        <h2>Buttons (.ap-btn)</h2>
        <div class="fp__row">
          <button class="ap-btn ap-btn--primary">Primary</button>
          <button class="ap-btn ap-btn--ghost">Ghost</button>
          <button class="ap-btn ap-btn--accent">Accent</button>
          <button class="ap-btn ap-btn--danger">Danger</button>
          <button class="ap-btn ap-btn--primary" disabled>Disabled</button>
        </div>
      </section>

      <section class="fp__sec">
        <h2>Card + field (.ap-card / .ap-field)</h2>
        <div class="ap-card" style="max-width: 420px">
          <div class="ap-card__head">
            <h3 class="ap-card__title">
              <app-icon name="user" [size]="18" />
              Card title
            </h3>
            <app-status-pill status="Approved" />
          </div>
          <div class="ap-card__body">
            <div class="ap-field">
              <label for="fp-name">Full name</label>
              <input id="fp-name" class="ap-input" placeholder="Jane Doe" />
            </div>
            <div class="ap-field" style="margin-top: 14px">
              <label for="fp-type">Appointment type</label>
              <select id="fp-type" class="ap-select">
                <option>Panel QME</option>
                <option>AME Evaluation</option>
              </select>
            </div>
          </div>
        </div>
      </section>
    </div>
  `,
  styles: `
    .fp {
      min-height: 100vh;
      padding: 32px 40px 80px;
      font-family: var(--font);
      color: var(--n-800);
      background: var(--surface-app);
    }
    .fp__hd h1 {
      margin: 0;
      font-size: 24px;
      font-weight: 800;
      letter-spacing: -0.02em;
      color: var(--n-900);
    }
    .fp__hd p {
      margin: 4px 0 0;
      color: var(--n-500);
      font-size: 14px;
    }
    .fp__sec {
      margin-top: 36px;
    }
    .fp__sec h2 {
      font-size: 14px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.06em;
      color: var(--n-500);
      border-bottom: 1px solid var(--border);
      padding-bottom: 8px;
    }
    .fp__sec h3 {
      font-size: 13px;
      font-weight: 700;
      color: var(--n-700);
      margin: 16px 0 8px;
    }
    .fp__swatches {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
    }
    .fp__swatch {
      display: flex;
      flex-direction: column;
      gap: 6px;
      align-items: center;
    }
    .fp__chip {
      width: 64px;
      height: 44px;
      border-radius: var(--r-md);
      box-shadow: var(--sh-xs);
    }
    .fp__chip--bordered {
      border: 1px solid var(--border-strong);
    }
    .fp__swatch code,
    .fp__icon code {
      font-size: 11px;
      color: var(--n-500);
    }
    .fp__row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 12px;
    }
    .fp__icons {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(96px, 1fr));
      gap: 14px;
    }
    .fp__icon {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
      padding: 16px 8px;
      background: var(--surface-card);
      border: 1px solid var(--border);
      border-radius: var(--r-md);
      color: var(--n-700);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FoundationPreviewComponent {
  protected readonly icons = ICON_NAMES;

  protected readonly statuses: AppointmentPillStatus[] = [
    'Pending',
    'InfoRequested',
    'Approved',
    'Rejected',
    'Cancelled',
    'Rescheduled',
  ];

  protected readonly statusTones = [
    'pending',
    'approved',
    'rejected',
    'neutral',
    'info',
    'teal',
    'purple',
  ];

  protected readonly ramps = [
    {
      name: 'Blue',
      tokens: [
        '--blue-50',
        '--blue-100',
        '--blue-200',
        '--blue-300',
        '--blue-400',
        '--blue-500',
        '--blue-600',
        '--blue-700',
        '--blue-800',
        '--blue-900',
      ],
    },
    {
      name: 'Green',
      tokens: [
        '--green-50',
        '--green-100',
        '--green-300',
        '--green-500',
        '--green-600',
        '--green-700',
      ],
    },
    {
      name: 'Neutral',
      tokens: [
        '--n-0',
        '--n-25',
        '--n-50',
        '--n-100',
        '--n-150',
        '--n-200',
        '--n-300',
        '--n-400',
        '--n-500',
        '--n-600',
        '--n-700',
        '--n-800',
        '--n-900',
      ],
    },
  ];
}
