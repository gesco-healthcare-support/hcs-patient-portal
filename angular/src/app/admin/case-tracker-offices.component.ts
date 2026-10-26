import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PermissionService, RestService } from '@abp/ng.core';
import { firstValueFrom } from 'rxjs';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { PacificDatePipe } from '../shared/pipes/pacific-date.pipe';

/** One office's push and feed state, mirroring `CaseTrackerOfficePushStateDto`. */
export interface OfficePushState {
  officeId: string;
  officeName: string;
  /** The office's switch: on means it may send to, and take answers from, the Case Tracker. */
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

export type ConnectionAction = 'connect' | 'disconnect';

/** The connection change an operator is being asked to confirm, and for which office. */
export interface ConnectionConfirmation {
  officeId: string;
  action: ConnectionAction;
}

/** Held by the System owner and IT Admin only; the host Supervisor sees the offices without the buttons. */
export const MANAGE_CONNECTION_POLICY = 'CaseEvaluation.CaseTrackerIntegration.ManageConnection';

/**
 * Per-office connection to the Case Tracker.
 *
 * <p>A sibling of `IntegrationFailuresComponent` rather than a block inside it: both render under the same
 * admin-hub section, so this needed no new section key -- which also avoided the hub's else-terminated
 * section dispatch, where adding a key without extending the chain silently routes the new screen into the
 * audit branch and 403s. Template and styles live in their own files (#927), as the failures screen's do,
 * to keep this under the 250-line ceiling.</p>
 *
 * <p>2026-10-26: one action per office. "Connect" starts the office's feed (if it is not on) and turns its
 * switch on, in one server call; "Disconnect" turns the switch off and leaves the feed as it is. They
 * replace "Turn on", "Start feed" and "Return to push": an office left between the first two sat in push
 * mode, which has nowhere to push on Azure, and returning to push re-sent everything. Both ask for an
 * inline confirm, and both are shown only to a caller holding {@link MANAGE_CONNECTION_POLICY} -- the
 * server refuses everyone else regardless.</p>
 *
 * <p>The queued count still matters: while an office is off its changes wait, and connecting it delivers
 * all of them. Under the feed the count to watch is the OUTSTANDING one -- every feed-era row stays
 * Pending, so the pending count only grows -- together with when the Case Tracker last asked.</p>
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
  private readonly permissions = inject(PermissionService);

  protected readonly offices = signal<OfficePushState[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Office whose connection change is in flight, so only that row's buttons disable. */
  protected readonly saving = signal<string | null>(null);

  /** The connection change awaiting an inline confirm, if any. */
  protected readonly confirming = signal<ConnectionConfirmation | null>(null);

  /** Whether this caller may connect or disconnect an office; without it the screen is read-only. */
  protected readonly canManage = this.permissions.getGrantedPolicy(MANAGE_CONNECTION_POLICY);

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

  /** Connected means both halves: the switch on AND the Case Tracker reading the office's feed. */
  protected isConnected(office: OfficePushState): boolean {
    return office.pushEnabled && !!office.feedActive;
  }

  /** The one action a row offers. A switch left on without the feed offers Connect, which starts it. */
  protected actionFor(office: OfficePushState): ConnectionAction {
    return this.isConnected(office) ? 'disconnect' : 'connect';
  }

  /** Asks for the inline confirm before a connection change. */
  protected askConnection(office: OfficePushState): void {
    this.error.set(null);
    this.confirming.set({ officeId: office.officeId, action: this.actionFor(office) });
  }

  protected isConfirming(office: OfficePushState): boolean {
    return this.confirming()?.officeId === office.officeId;
  }

  /**
   * Runs the confirmed change and replaces the row from the server's response rather than assuming the
   * new state: the response carries the refreshed counts the operator checks next.
   */
  protected async confirmConnection(office: OfficePushState): Promise<void> {
    const pending = this.confirming();
    if (pending?.officeId !== office.officeId) {
      return;
    }

    this.saving.set(office.officeId);
    this.error.set(null);
    try {
      const updated = await firstValueFrom(
        this.rest.request<null, OfficePushState>(
          {
            method: 'POST',
            url: `/api/app/case-tracker/offices/${office.officeId}/${pending.action}`,
          },
          { apiName: 'Default' },
        ),
      );
      this.replace(office, updated);
    } catch {
      this.error.set(
        `Could not ${pending.action} ${office.officeName || office.officeId}. Please try again.`,
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
