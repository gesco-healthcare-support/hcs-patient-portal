import { TestBed } from '@angular/core/testing';

import { OfflineOverlayComponent } from './offline-overlay.component';
import { OfflineDetectionService } from '../../services/offline-detection.service';

/**
 * The offline overlay's one action: Retry asks the detection service to re-check.
 *
 * <p>It had no spec. It is built in an injection context with a stub service, and never
 * rendered.</p>
 */
describe('OfflineOverlayComponent', () => {
  it('offers a single Retry action that re-checks the connection', () => {
    const refresh = jasmine.createSpy('refresh');
    TestBed.configureTestingModule({
      providers: [{ provide: OfflineDetectionService, useValue: { refresh } }],
    });
    const c = TestBed.runInInjectionContext(() => new OfflineOverlayComponent()) as unknown as {
      actions: { label: string; icon: string; click: () => void }[];
    };

    expect(c.actions.map((a) => [a.label, a.icon])).toEqual([['Retry', 'refresh']]);

    c.actions[0].click();

    expect(refresh).toHaveBeenCalledTimes(1);
    TestBed.resetTestingModule();
  });
});
