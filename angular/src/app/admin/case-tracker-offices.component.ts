import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RestService } from '@abp/ng.core';
import { firstValueFrom } from 'rxjs';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { PacificDatePipe } from '../shared/pipes/pacific-date.pipe';

/** One office's push and feed state, mirroring `CaseTrackerOfficePushStateDto`. */
export interface OfficePushState {
  officeId: string;
  officeName: string;
  pushEnabled: boolean;
  pendingCount: number;
  /** #927: true while the office is delivered by the changes feed rather than by push. */
  feedActive?: boolean;
  feedStartedAt?: string | null;
  lastRequestAt?: string | null;
  lastAdvancedAt?: string | null;
  /** While on the feed: changes waiting beyond the Case Tracker's acknowledged position. */
  outstandingCount?: number | null;
}

/** The feed action an operator is being asked to confirm, and for which office. */
export interface FeedConfirmation {
  officeId: string;
  action: 'start' | 'return';
}

/**
 * Per-office on/off control for the Case Tracker push, and since #927 the cutover between push and the
 * changes feed the Case Tracker pulls.
 *
 * <p>A sibling of `IntegrationFailuresComponent` rather than a block inside it: both render under the same
 * admin-hub section, so this needed no new section key -- which also avoided the hub's else-terminated
 * section dispatch, where adding a key without extending the chain silently routes the new screen into the
 * audit branch and 403s. Template and styles live in their own files (#927), as the failures screen's do,
 * to keep this under the 250-line ceiling.</p>
 *
 * <p>The pending count is the point of this screen as much as the switch is. While the push is off the
 * drain claims nothing, so due rows accumulate; enabling an office flushes ALL of them on the next drain.
 * Under the feed the count to watch is the OUTSTANDING one -- every feed-era row stays Pending, so the
 * pending count only grows -- together with when the Case Tracker last asked.</p>
 *
 * <p>Starting the feed and returning to push each need an inline confirm: both change how an office's
 * changes reach the Case Tracker, and returning to push re-sends changes the feed already delivered.</p>
 */
@Component({
  selector: 'app-case-tracker-offices',
  standalone: true,
  imports: [CommonModule, IconComponent, PacificDatePipe],
  templateUrl: './case-tracker-offices.component.html',
  styleUrl: './case-tracker-offices.component.scss',
})
export class CaseTrackerOfficesComponent implements OnInit {
  private readonly rest = inject(RestService);

  protected readonly offices = signal<OfficePushState[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Office whose toggle or feed action is in flight, so only that row's buttons disable. */
  protected readonly saving = signal<string | null>(null);

  /** The feed action awaiting an inline confirm, if any. */
  protected readonly confirming = signal<FeedConfirmation | null>(null);

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
      this.replace(office, updated);
    } catch {
      this.error.set(
        `Could not change the push setting for ${office.officeName || office.officeId}.`,
      );
    } finally {
      this.saving.set(null);
    }
  }

  /** Asks for the inline confirm before a feed action. */
  protected askFeed(office: OfficePushState, action: FeedConfirmation['action']): void {
    this.error.set(null);
    this.confirming.set({ officeId: office.officeId, action });
  }

  protected isConfirming(office: OfficePushState, action: FeedConfirmation['action']): boolean {
    const pending = this.confirming();
    return pending?.officeId === office.officeId && pending.action === action;
  }

  /**
   * Runs the confirmed feed action and replaces the row from the response, as the toggle does: the
   * response carries the new mode and the outstanding count the operator checks next.
   */
  protected async confirmFeed(office: OfficePushState): Promise<void> {
    const pending = this.confirming();
    if (pending?.officeId !== office.officeId) {
      return;
    }

    const path = pending.action === 'start' ? 'start' : 'return-to-push';
    this.saving.set(office.officeId);
    this.error.set(null);
    try {
      const updated = await firstValueFrom(
        this.rest.request<null, OfficePushState>(
          {
            method: 'POST',
            url: `/api/app/case-tracker/offices/${office.officeId}/feed/${path}`,
          },
          { apiName: 'Default' },
        ),
      );
      this.replace(office, updated);
    } catch {
      const name = office.officeName || office.officeId;
      this.error.set(
        pending.action === 'start'
          ? `Could not start the feed for ${name}.`
          : `Could not return ${name} to push.`,
      );
    } finally {
      this.confirming.set(null);
      this.saving.set(null);
    }
  }

  private replace(office: OfficePushState, updated: OfficePushState | null | undefined): void {
    this.offices.update((current) =>
      current.map((o) => (o.officeId === office.officeId ? (updated ?? o) : o)),
    );
  }
}
