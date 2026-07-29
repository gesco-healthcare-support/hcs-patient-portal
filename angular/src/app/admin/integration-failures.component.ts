import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RestService } from '@abp/ng.core';
import { firstValueFrom } from 'rxjs';
import { IconComponent } from '../shared/ui/icon/icon.component';

/**
 * One outstanding dead letter, mirroring `CaseTrackerDeadLetterDto`.
 *
 * Carries no patient field by design: the confirmation number identifies the appointment for a human,
 * and section I2 of the integration contract requires this screen not to render PHI.
 */
export interface DeadLetterRow {
  id: string;
  officeId: string;
  officeName: string;
  appointmentId: string;
  confirmationNumber: string;
  messageType: string;
  targetPath: string;
  attemptCount: number;
  lastError?: string | null;
  failedAt: string;
  alertedAt?: string | null;
}

/**
 * Admin screen listing Case Tracker pushes that failed permanently, with a per-row Retry.
 *
 * <p>A standalone component rather than another branch inside the admin hub: that component is already
 * 662 lines against the repo's 250-line ceiling for an Angular component, so adding a fifth section
 * inline would make an existing problem materially worse. The hub renders this one instead.</p>
 *
 * <p>Calls the API through `RestService` with literal URLs -- the pattern used elsewhere in this app
 * (see `appointment-documents.component.ts`) -- against the explicit route on
 * `CaseTrackerDeadLetterController`. No generated proxy is involved, so front end and back end agree by
 * construction rather than by whatever ABP's route convention derives.</p>
 */
@Component({
  selector: 'app-integration-failures',
  standalone: true,
  imports: [CommonModule, IconComponent],
  template: `
    <div class="if-head">
      <div>
        <h2>Case Tracker failures</h2>
        <p class="if-sub">
          Pushes that failed permanently and will not retry on their own. Each one means a case has
          not reached the Case Tracker. Retry re-sends the appointment's current details.
        </p>
      </div>
      <button type="button" class="if-btn" (click)="load()" [disabled]="loading()">
        <app-icon name="refresh" [size]="14" />
        Refresh
      </button>
    </div>

    @if (loading()) {
      <div class="if-empty">
        <app-icon name="clock" [size]="26" />
        Loading failures...
      </div>
    } @else if (error()) {
      <div class="if-empty if-error">
        <app-icon name="alert" [size]="26" />
        <b>Could not load failures</b>
        {{ error() }}
      </div>
    } @else if (!rows().length) {
      <!-- Explicit empty state: a blank table reads as "broken", not as "nothing wrong". -->
      <div class="if-empty">
        <app-icon name="check" [size]="26" />
        <b>No failed pushes</b>
        Every appointment has reached the Case Tracker, or is still queued to.
      </div>
    } @else {
      <table class="if-table">
        <thead>
          <tr>
            <th>Confirmation</th>
            <th>Clinic</th>
            <th>Type</th>
            <th>Attempts</th>
            <th>Failed</th>
            <th>Error</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (r of rows(); track r.id) {
            <tr>
              <td>
                <strong>{{ r.confirmationNumber || '(unknown)' }}</strong>
                <div class="if-dim">{{ r.appointmentId }}</div>
              </td>
              <td>{{ r.officeName }}</td>
              <td>{{ r.messageType }}</td>
              <td>{{ r.attemptCount }}</td>
              <td>{{ r.failedAt | date: 'short' }}</td>
              <td class="if-err">{{ r.lastError || '--' }}</td>
              <td>
                <button
                  type="button"
                  class="if-btn"
                  (click)="retry(r)"
                  [disabled]="retrying() === r.id"
                >
                  {{ retrying() === r.id ? 'Retrying...' : 'Retry' }}
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
      .if-head {
        display: flex;
        align-items: flex-start;
        justify-content: space-between;
        gap: 16px;
        margin-bottom: 16px;
      }
      .if-head h2 {
        margin: 0 0 4px;
        font-size: 18px;
      }
      .if-sub {
        margin: 0;
        color: #555;
        font-size: 13px;
        max-width: 70ch;
      }
      .if-btn {
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
      .if-btn:disabled {
        opacity: 0.55;
        cursor: default;
      }
      .if-empty {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 8px;
        padding: 40px 20px;
        color: #555;
        text-align: center;
      }
      .if-error {
        color: #a11;
      }
      .if-table {
        width: 100%;
        border-collapse: collapse;
        font-size: 13px;
      }
      .if-table th,
      .if-table td {
        text-align: left;
        padding: 8px 10px;
        border-bottom: 1px solid #eee;
        vertical-align: top;
      }
      .if-dim {
        color: #888;
        font-size: 11px;
        font-family: Consolas, Menlo, monospace;
      }
      .if-err {
        max-width: 40ch;
        word-break: break-word;
        color: #a11;
      }
    `,
  ],
})
export class IntegrationFailuresComponent {
  private readonly rest = inject(RestService);

  protected readonly rows = signal<DeadLetterRow[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Id of the row whose Retry is in flight, so only that button disables. */
  protected readonly retrying = signal<string | null>(null);

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const rows = await firstValueFrom(
        this.rest.request<null, DeadLetterRow[]>(
          { method: 'GET', url: '/api/app/case-tracker/dead-letters' },
          { apiName: 'Default' },
        ),
      );
      this.rows.set(rows ?? []);
    } catch {
      this.error.set('The failure list could not be loaded. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  /**
   * Retries one dead letter, then removes it from the list without a full reload -- the server has
   * marked it resolved, so it would not come back anyway, and dropping it in place keeps the operator's
   * position in a long list.
   */
  protected async retry(row: DeadLetterRow): Promise<void> {
    this.retrying.set(row.id);
    this.error.set(null);
    try {
      await firstValueFrom(
        this.rest.request<null, unknown>(
          {
            method: 'POST',
            url: `/api/app/case-tracker/offices/${row.officeId}/dead-letters/${row.id}/retry`,
          },
          { apiName: 'Default' },
        ),
      );
      this.rows.update((current) => current.filter((r) => r.id !== row.id));
    } catch {
      this.error.set(
        `Retry failed for ${row.confirmationNumber || row.appointmentId}. It may already have been retried.`,
      );
    } finally {
      this.retrying.set(null);
    }
  }
}
