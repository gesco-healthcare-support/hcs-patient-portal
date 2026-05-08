import { Injectable, OnDestroy, inject } from '@angular/core';
import { ConfigStateService, PermissionService, RestService, RoutesService } from '@abp/ng.core';
import { Subscription, timer } from 'rxjs';
import { switchMap } from 'rxjs/operators';

/**
 * Wave 4 / #6 (NEW-only enhancement, PARITY-FLAG-NEW-003) -- polls the
 * pending-appointments count for admin / staff supervisor / clinic
 * staff users and patches the Appointments sidebar entry to show a
 * count badge.
 *
 * <p><b>Why a service, not inline in `app.component.ts`:</b> the badge
 * needs to start polling whenever the auth state changes (e.g. a user
 * logs in mid-session) and stop when the user logs out -- driving that
 * lifecycle from `ConfigStateService` updates is cleaner in a
 * dedicated service than as a couple of subscriptions inside the root
 * component. Provided in `root` so it stays a singleton; instantiated
 * lazily by `app.component.ts` calling `start()` from `ngOnInit`.</p>
 *
 * <p><b>Permission gate:</b> the backend method requires
 * <c>CaseEvaluation.Appointments.Edit</c>. We mirror that gate on the
 * client to avoid firing the request at all when it would 403, and to
 * silently keep the badge hidden for external users (Patient / AA /
 * DA / CE) who would otherwise never see a non-zero count anyway. The
 * backend remains authoritative.</p>
 *
 * <p><b>Patch strategy:</b> ABP's `RoutesService.patch(identifier,
 * props)` merges `props` into the existing route record. We patch the
 * `name` field with a literal "Appointments (N)" string when count
 * > 0 and restore the original `::Menu:Appointments` localization key
 * when count = 0 so the sidebar reverts to the localized label. This
 * sidesteps the LeptonX nav template's lack of a built-in badge slot.
 * Localization for non-English locales is parked for a future wave;
 * the literal "Appointments" stays as a Phase 1A acceptable trade.</p>
 */
@Injectable({ providedIn: 'root' })
export class AppointmentPendingCountService implements OnDestroy {
  private static readonly RouteIdentifier = '::Menu:Appointments';
  private static readonly RouteOriginalName = '::Menu:Appointments';
  private static readonly EditPermission = 'CaseEvaluation.Appointments.Edit';
  private static readonly PollIntervalMs = 60_000;

  private readonly configState = inject(ConfigStateService);
  private readonly permission = inject(PermissionService);
  private readonly restService = inject(RestService);
  private readonly routesService = inject(RoutesService);

  private pollSubscription: Subscription | null = null;
  private configStateSubscription: Subscription | null = null;
  private started = false;
  private currentCount = 0;

  /**
   * Idempotent start hook. Call once from the root component's
   * `ngOnInit`. Subscribes to `ConfigStateService` updates so a login
   * mid-session begins polling and a logout stops it.
   */
  start(): void {
    if (this.started) {
      return;
    }
    this.started = true;
    this.configStateSubscription = this.configState
      .createOnUpdateStream((state) => state)
      .subscribe(() => this.handleAuthStateChange());
    // Run once on app boot for the initial auth state.
    this.handleAuthStateChange();
  }

  ngOnDestroy(): void {
    this.stopPolling();
    this.configStateSubscription?.unsubscribe();
    this.configStateSubscription = null;
    this.started = false;
  }

  private handleAuthStateChange(): void {
    if (this.permission.getGrantedPolicy(AppointmentPendingCountService.EditPermission)) {
      this.startPolling();
    } else {
      this.stopPolling();
      this.resetBadge();
    }
  }

  private startPolling(): void {
    if (this.pollSubscription) {
      return;
    }
    // `timer(0, interval)` fires immediately then every interval ms.
    // `switchMap` cancels any in-flight request when a new tick fires.
    this.pollSubscription = timer(0, AppointmentPendingCountService.PollIntervalMs)
      .pipe(switchMap(() => this.fetchPendingCount()))
      .subscribe({
        next: (count) => this.applyBadge(count),
        error: () => {
          // Network / permission error -- swallow. Next tick retries.
        },
      });
  }

  private stopPolling(): void {
    this.pollSubscription?.unsubscribe();
    this.pollSubscription = null;
  }

  private fetchPendingCount() {
    return this.restService.request<void, number>(
      {
        method: 'GET',
        url: '/api/app/appointments/pending-count',
      },
      { apiName: 'Default' },
    );
  }

  private applyBadge(count: number): void {
    const safeCount = Math.max(0, Math.floor(Number(count) || 0));
    if (safeCount === this.currentCount) {
      return;
    }
    this.currentCount = safeCount;
    if (safeCount > 0) {
      this.routesService.patch(AppointmentPendingCountService.RouteIdentifier, {
        name: `Appointments (${safeCount})`,
      });
    } else {
      this.resetBadge();
    }
  }

  private resetBadge(): void {
    if (this.currentCount === 0) {
      return;
    }
    this.currentCount = 0;
    this.routesService.patch(AppointmentPendingCountService.RouteIdentifier, {
      name: AppointmentPendingCountService.RouteOriginalName,
    });
  }
}
