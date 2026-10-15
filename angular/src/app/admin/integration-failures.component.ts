import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RestService } from '@abp/ng.core';
import { firstValueFrom } from 'rxjs';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { PacificDatePipe } from '../shared/pipes/pacific-date.pipe';

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

/** Counts from a retry-all, mirroring `CaseTrackerDeadLetterRetryAllResultDto`. */
export interface RetryAllResult {
  requeued: number;
  alreadyDelivered: number;
  notRetried: number;
  remaining: number;
}

/** One office that has failures, for the filter. */
export interface FailureOffice {
  id: string;
  name: string;
}

/**
 * Admin screen listing Case Tracker pushes that failed permanently, with a per-row Retry, an office
 * filter and a Retry all for the filtered office (#917).
 *
 * <p>A standalone component rather than another branch inside the admin hub: that component is already
 * 662 lines against the repo's 250-line ceiling for an Angular component, so adding a fifth section
 * inline would make an existing problem materially worse. The hub renders this one instead.</p>
 *
 * <p>Template and styles live in their own files since #917 added the filter and bulk retry: inline,
 * the component passed the same 250-line ceiling.</p>
 *
 * <p>Calls the API through `RestService` with literal URLs -- the pattern used elsewhere in this app
 * (see `appointment-documents.component.ts`) -- against the explicit route on
 * `CaseTrackerDeadLetterController`. No generated proxy is involved, so front end and back end agree by
 * construction rather than by whatever ABP's route convention derives.</p>
 */
@Component({
  selector: 'app-integration-failures',
  standalone: true,
  imports: [CommonModule, IconComponent, PacificDatePipe],
  templateUrl: './integration-failures.component.html',
  styleUrl: './integration-failures.component.scss',
})
export class IntegrationFailuresComponent implements OnInit {
  private readonly rest = inject(RestService);

  protected readonly rows = signal<DeadLetterRow[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Id of the row whose Retry is in flight, so only that button disables. */
  protected readonly retrying = signal<string | null>(null);

  /** Office id the list is narrowed to; empty string for all offices. */
  protected readonly officeFilter = signal('');

  /** True between pressing "Retry all for this office" and confirming or cancelling. */
  protected readonly confirmingRetryAll = signal(false);

  protected readonly retryingAll = signal(false);

  /** Outcome of the last retry-all, stated as counts. */
  protected readonly notice = signal<string | null>(null);

  /** Offices that currently have failures, by name. Derived from the list, so it is never stale. */
  protected readonly offices = computed<FailureOffice[]>(() => {
    const byId = new Map<string, string>();
    for (const r of this.rows()) {
      byId.set(r.officeId, r.officeName);
    }
    return [...byId.entries()]
      .map(([id, name]) => ({ id, name }))
      .sort((a, b) => a.name.localeCompare(b.name));
  });

  protected readonly visibleRows = computed(() => {
    const office = this.officeFilter();
    return office ? this.rows().filter((r) => r.officeId === office) : this.rows();
  });

  protected readonly selectedOfficeName = computed(
    () => this.offices().find((o) => o.id === this.officeFilter())?.name ?? '',
  );

  /**
   * Sweep #644 (S7059): the load used to run from the constructor. Angular constructs a
   * component before it is part of the view, so a fetch started there races the first
   * render and cannot be stopped by a test that has not called detectChanges yet.
   */
  ngOnInit(): void {
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
      this.dropFilterIfEmpty();
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
      this.dropFilterIfEmpty();
    } catch {
      this.error.set(
        `Retry failed for ${row.confirmationNumber || row.appointmentId}. It may already have been retried.`,
      );
    } finally {
      this.retrying.set(null);
    }
  }

  protected selectOffice(officeId: string): void {
    this.officeFilter.set(officeId);
    this.confirmingRetryAll.set(false);
    this.notice.set(null);
  }

  /**
   * Retries every failure in the filtered office, then reloads: unlike a single retry, some rows may
   * stay (not retried, or past the per-call limit), so only the server knows what is left.
   */
  protected async retryAll(): Promise<void> {
    const officeId = this.officeFilter();
    if (!officeId) {
      return;
    }
    this.retryingAll.set(true);
    this.error.set(null);
    this.notice.set(null);
    try {
      const result = await firstValueFrom(
        this.rest.request<null, RetryAllResult>(
          {
            method: 'POST',
            url: `/api/app/case-tracker/offices/${officeId}/dead-letters/retry-all`,
          },
          { apiName: 'Default' },
        ),
      );
      this.notice.set(describeRetryAll(result));
    } catch {
      // A notice, not the error state: the reload below must still show the table, because some rows
      // may have been retried before the call failed.
      this.notice.set('Retry all did not finish. The list below shows what is still outstanding.');
    } finally {
      this.retryingAll.set(false);
      this.confirmingRetryAll.set(false);
    }
    await this.load();
  }

  /** An office with nothing left drops out of the filter, so the filter falls back to all offices. */
  private dropFilterIfEmpty(): void {
    const office = this.officeFilter();
    if (office && !this.rows().some((r) => r.officeId === office)) {
      this.officeFilter.set('');
      this.confirmingRetryAll.set(false);
    }
  }
}

/** The retry-all outcome as one line of counts, naming only the parts that happened. */
export function describeRetryAll(result: RetryAllResult | null | undefined): string {
  const r = result ?? { requeued: 0, alreadyDelivered: 0, notRetried: 0, remaining: 0 };
  const parts = [`${r.requeued} queued to send again.`];
  if (r.alreadyDelivered) {
    parts.push(`${r.alreadyDelivered} already delivered.`);
  }
  if (r.notRetried) {
    parts.push(`${r.notRetried} could not be retried and are still listed.`);
  }
  if (r.remaining) {
    parts.push(`${r.remaining} not yet retried; select Retry all again.`);
  }
  return parts.join(' ');
}
