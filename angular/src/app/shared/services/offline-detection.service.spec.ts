import { TestBed } from '@angular/core/testing';
import { OfflineDetectionService } from './offline-detection.service';

describe('OfflineDetectionService', () => {
  let service: OfflineDetectionService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(OfflineDetectionService);
    service.start();
  });

  afterEach(() => {
    service.ngOnDestroy();
  });

  it('flips offline() on window offline/online events', () => {
    window.dispatchEvent(new Event('offline'));
    expect(service.offline()).toBe(true);

    window.dispatchEvent(new Event('online'));
    expect(service.offline()).toBe(false);
  });

  it('refresh() re-reads navigator connectivity', () => {
    window.dispatchEvent(new Event('offline'));
    expect(service.offline()).toBe(true);

    // refresh() resets the signal from navigator.onLine, which is true in the
    // headless test browser, so the stale offline state clears.
    service.refresh();
    expect(service.offline()).toBe(navigator.onLine === false);
  });
});
