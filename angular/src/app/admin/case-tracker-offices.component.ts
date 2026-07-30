import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RestService } from '@abp/ng.core';
import { firstValueFrom } from 'rxjs';
import { IconComponent } from '../shared/ui/icon/icon.component';

/** One office's push state, mirroring `CaseTrackerOfficePushStateDto`. */
export interface OfficePushState {
  officeId: string;
  officeName: string;
  pushEnabled: boolean;
  pendingCount: number;
}

/**
 * Per-office on/off control for the Case Tracker push.
 *
 * <p>A sibling of `IntegrationFailuresComponent` rather than a block inside it: that file already sits
 * at the repo's 250-line ceiling for an Angular component. Both render under the same admin-hub
 * section, so this needed no new section key -- which also avoided the hub's else-terminated section
 * dispatch, where adding a key without extending the chain silently routes the new screen into the
 * audit branch and 403s.</p>
 *
 * <p>The pending count is the point of this screen as much as the switch is. While the push is off the
 * drain claims nothing, so due rows accumulate; enabling an office flushes ALL of them on the next
 * drain. Without the count an operator cannot tell whether flipping the switch sends one message or
 * several hundred.</p>
 */
@Component({
  selector: 'app-case-tracker-offices',
  standalone: true,
  imports: [CommonModule, IconComponent],
  template: `
    <div class="cto-head">
      <div>
        <h2>Case Tracker push</h2>
        <p class="cto-sub">
          Off by default, per clinic. Enabling one sends its approved appointments to the Case
          Tracker, including anything already queued while it was off.
        </p>
      </div>
      <button type="button" class="cto-btn" (click)="load()" [disabled]="loading()">
        <app-icon name="refresh" [size]="14" />
        Refresh
      </button>
    </div>

    @if (loading()) {
      <div class="cto-empty">
        <app-icon name="clock" [size]="26" />
        Loading clinics...
      </div>
    } @else if (error()) {
      <div class="cto-empty cto-error">
        <app-icon name="alert" [size]="26" />
        <b>Could not load clinics</b>
        {{ error() }}
      </div>
    } @else if (!offices().length) {
      <div class="cto-empty">
        <app-icon name="alert" [size]="26" />
        <b>No clinics found</b>
        Nothing to enable yet.
      </div>
    } @else {
      <table class="cto-table">
        <thead>
          <tr>
            <th>Clinic</th>
            <th>Push</th>
            <th>Queued</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (o of offices(); track o.officeId) {
            <tr>
              <td>
                <strong>{{ o.officeName || '(unnamed)' }}</strong>
                <div class="cto-dim">{{ o.officeId }}</div>
              </td>
              <td>
                <span class="cto-pill" [class.cto-on]="o.pushEnabled">
                  {{ o.pushEnabled ? 'Enabled' : 'Off' }}
                </span>
              </td>
              <td>
                <!-- Warned only when it matters: a backlog is harmless while off and a flood when enabled. -->
                @if (o.pendingCount > 0 && !o.pushEnabled) {
                  <span class="cto-warn">{{ o.pendingCount }} waiting -- all sent on enable</span>
                } @else {
                  <span class="cto-dim">{{ o.pendingCount }}</span>
                }
              </td>
              <td>
                <button
                  type="button"
                  class="cto-btn"
                  (click)="toggle(o)"
                  [disabled]="saving() === o.officeId"
                >
                  {{
                    saving() === o.officeId ? 'Saving...' : o.pushEnabled ? 'Turn off' : 'Turn on'
                  }}
                </button>
              </td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: [
    `
      .cto-head {
        display: flex;
        align-items: flex-start;
        justify-content: space-between;
        gap: 16px;
        margin-bottom: 16px;
      }
      .cto-head h2 {
        margin: 0 0 4px;
        font-size: 18px;
      }
      .cto-sub {
        margin: 0;
        color: #555;
        font-size: 13px;
        max-width: 70ch;
      }
      .cto-btn {
        display: inline-flex;
        align-items: center;
        gap: 6px;
        padding: 6px 12px;
        border: 1px solid #ccc;
        border-radius: 4px;
        background: #fff;
        cursor: pointer;
        font-size: 13px;
        white-space: nowrap;
      }
      .cto-btn:disabled {
        opacity: 0.55;
        cursor: default;
      }
      .cto-empty {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 8px;
        padding: 28px 20px;
        color: #555;
        text-align: center;
      }
      .cto-error {
        color: #a11;
      }
      .cto-table {
        width: 100%;
        border-collapse: collapse;
        font-size: 13px;
        margin-bottom: 28px;
      }
      .cto-table th,
      .cto-table td {
        text-align: left;
        padding: 8px 10px;
        border-bottom: 1px solid #eee;
        vertical-align: top;
      }
      .cto-dim {
        color: #888;
        font-size: 11px;
        font-family: Consolas, Menlo, monospace;
      }
      .cto-pill {
        display: inline-block;
        padding: 2px 8px;
        border-radius: 10px;
        background: #eee;
        color: #555;
        font-size: 12px;
      }
      .cto-on {
        background: #e6f4ea;
        color: #1b5e20;
      }
      .cto-warn {
        color: #8a5a00;
      }
    `,
  ],
})
export class CaseTrackerOfficesComponent {
  private readonly rest = inject(RestService);

  protected readonly offices = signal<OfficePushState[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Office whose toggle is in flight, so only that row's button disables. */
  protected readonly saving = signal<string | null>(null);

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const offices = await firstValueFrom(
        this.rest.request<null, OfficePushState[]>(
          { method: 'GET', url: '/api/app/case-tracker/offices' },
          { apiName: 'Default' },
        ),
      );
      this.offices.set(offices ?? []);
    } catch {
      this.error.set('The clinic list could not be loaded. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  /**
   * Flips one office and replaces that row from the server's response rather than assuming the new
   * state -- the response also carries a refreshed pending count, which is the number the operator
   * needs immediately after enabling.
   */
  protected async toggle(office: OfficePushState): Promise<void> {
    this.saving.set(office.officeId);
    this.error.set(null);
    try {
      const updated = await firstValueFrom(
        this.rest.request<{ enabled: boolean }, OfficePushState>(
          {
            method: 'PUT',
            url: `/api/app/case-tracker/offices/${office.officeId}/push`,
            body: { enabled: !office.pushEnabled },
          },
          { apiName: 'Default' },
        ),
      );
      this.offices.update((current) =>
        current.map((o) => (o.officeId === office.officeId ? (updated ?? o) : o)),
      );
    } catch {
      this.error.set(
        `Could not change the push setting for ${office.officeName || office.officeId}.`,
      );
    } finally {
      this.saving.set(null);
    }
  }
}
