import { Injectable, OnDestroy, signal } from '@angular/core';

/**
 * Tracks browser connectivity via the window `online`/`offline` events and
 * exposes it as a signal. App-wide: AppComponent starts it once and renders the
 * offline overlay while `offline()` is true (both external and internal users).
 *
 * `navigator.onLine` is a best-effort hint (false === definitely offline; true
 * does not guarantee reachability), which is why a failed API call still falls
 * through to the HTTP error screen. This service only covers the clear-cut
 * "the device reports no network" case.
 */
@Injectable({ providedIn: 'root' })
export class OfflineDetectionService implements OnDestroy {
  private readonly _offline = signal(
    typeof navigator !== 'undefined' && navigator.onLine === false,
  );

  /** True when the browser reports no network connectivity. */
  readonly offline = this._offline.asReadonly();

  private started = false;
  private readonly onOffline = (): void => this._offline.set(true);
  private readonly onOnline = (): void => this._offline.set(false);

  /** Begin listening for connectivity changes. Idempotent; call once at startup. */
  start(): void {
    if (this.started || typeof window === 'undefined') {
      return;
    }
    this.started = true;
    window.addEventListener('offline', this.onOffline);
    window.addEventListener('online', this.onOnline);
  }

  /** Re-read connectivity now; backs the offline overlay's Retry button. */
  refresh(): void {
    this._offline.set(typeof navigator !== 'undefined' && navigator.onLine === false);
  }

  ngOnDestroy(): void {
    if (typeof window === 'undefined') {
      return;
    }
    window.removeEventListener('offline', this.onOffline);
    window.removeEventListener('online', this.onOnline);
  }
}
