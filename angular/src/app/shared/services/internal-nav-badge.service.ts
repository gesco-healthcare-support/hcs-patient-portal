import { Injectable, OnDestroy, Signal, inject, signal } from '@angular/core';
import { ConfigStateService, PermissionService } from '@abp/ng.core';
import { Subscription, timer } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { AppointmentPendingCountService } from '../../appointments/services/appointment-pending-count.service';
import { DashboardService } from '../../proxy/dashboards/dashboard.service';

/**
 * Internal-shell nav badge counts (plan Task 3). Exposes two signals the
 * sidebar binds to its nav items:
 *
 *   - `pendingAppointments` -- re-exposes {@link AppointmentPendingCountService}'s
 *     count (already polled app-wide and started from app.component). No second
 *     request for the same number.
 *   - `pendingChangeRequests` -- the supervisor change-request queue size, read
 *     from {@link DashboardService} on a poll started by the shell.
 *
 * The change-request poll is gated by the AppointmentChangeRequests read
 * permission so it never fires for users who could not see the queue (the
 * backend stays authoritative); the badge simply stays at zero for them.
 */
@Injectable({ providedIn: 'root' })
export class InternalNavBadgeService implements OnDestroy {
  private static readonly ChangeRequestPermission = 'CaseEvaluation.AppointmentChangeRequests';
  private static readonly PollIntervalMs = 60_000;

  private readonly appointmentPendingCount = inject(AppointmentPendingCountService);
  private readonly dashboard = inject(DashboardService);
  private readonly configState = inject(ConfigStateService);
  private readonly permission = inject(PermissionService);

  /** Appointments-awaiting-action count (passthrough; see service above). */
  readonly pendingAppointments: Signal<number> = this.appointmentPendingCount.pendingCount;

  private readonly _pendingChangeRequests = signal(0);
  /** Outstanding reschedule/cancel change requests for supervisors. */
  readonly pendingChangeRequests = this._pendingChangeRequests.asReadonly();

  private pollSubscription: Subscription | null = null;
  private configStateSubscription: Subscription | null = null;
  private started = false;

  /**
   * Idempotent start hook; call once from the shell's ngOnInit. Mirrors the
   * AppointmentPendingCountService lifecycle: (re)starts the change-request
   * poll on every auth-state change and stops it when the permission drops.
   */
  start(): void {
    if (this.started) {
      return;
    }
    this.started = true;
    this.configStateSubscription = this.configState
      .createOnUpdateStream((state) => state)
      .subscribe(() => this.handleAuthStateChange());
    this.handleAuthStateChange();
  }

  ngOnDestroy(): void {
    this.stopPolling();
    this.configStateSubscription?.unsubscribe();
    this.configStateSubscription = null;
    this.started = false;
  }

  private handleAuthStateChange(): void {
    if (this.permission.getGrantedPolicy(InternalNavBadgeService.ChangeRequestPermission)) {
      this.startPolling();
    } else {
      this.stopPolling();
      this._pendingChangeRequests.set(0);
    }
  }

  private startPolling(): void {
    if (this.pollSubscription) {
      return;
    }
    this.pollSubscription = timer(0, InternalNavBadgeService.PollIntervalMs)
      .pipe(switchMap(() => this.dashboard.get()))
      .subscribe({
        next: (counters) => {
          const raw = counters?.pendingChangeRequests ?? 0;
          this._pendingChangeRequests.set(Math.max(0, Math.floor(Number(raw) || 0)));
        },
        error: () => {
          // Network / permission error -- leave the last value; next tick retries.
        },
      });
  }

  private stopPolling(): void {
    this.pollSubscription?.unsubscribe();
    this.pollSubscription = null;
  }
}
