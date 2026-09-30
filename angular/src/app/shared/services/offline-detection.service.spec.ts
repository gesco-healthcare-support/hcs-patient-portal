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

  it('attaches its window listeners only once however often it is started', () => {
    // beforeEach has already started it; a second start must not stack a second pair of
    // listeners, which ngOnDestroy would then only half remove.
    const add = spyOn(window, 'addEventListener').and.callThrough();

    service.start();

    expect(add).not.toHaveBeenCalled();
  });
});
