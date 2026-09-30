import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';

import { AppComponent } from './app.component';
import { AppointmentPendingCountService } from './appointments/services/appointment-pending-count.service';
import { SessionIdentityWatcherService } from './shared/auth/session-identity-watcher.service';
import { OfflineDetectionService } from './shared/services/offline-detection.service';

/**
 * The root component wires up the always-on background services on init. This
 * asserts ngOnInit starts each one exactly once (services are singletons and
 * self-stop, so a single start() call each is the contract). Rendering is not
 * exercised (no detectChanges) to avoid instantiating the LeptonX/GDPR children.
 */
describe('AppComponent', () => {
  it('starts the always-on background services on init', () => {
    const pending = { start: jasmine.createSpy('start') };
    const identity = { start: jasmine.createSpy('start') };
    const offlineDetection = { start: jasmine.createSpy('start'), offline: signal(false) };

    TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        { provide: AppointmentPendingCountService, useValue: pending },
        { provide: SessionIdentityWatcherService, useValue: identity },
        { provide: OfflineDetectionService, useValue: offlineDetection },
      ],
    });

    const component = TestBed.createComponent(AppComponent).componentInstance;
    component.ngOnInit();

    expect(pending.start).toHaveBeenCalledTimes(1);
    expect(identity.start).toHaveBeenCalledTimes(1);
    expect(offlineDetection.start).toHaveBeenCalledTimes(1);
  });
});
