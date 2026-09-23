import { TestBed } from '@angular/core/testing';

import { AppointmentDetailModalComponent } from './appointment-detail.component';
import { AppointmentDetailViewService } from '../services/appointment-detail.service';

/**
 * The generated appointment detail modal is a template over its view service; the one thing
 * the class does is expose that service to the template.
 *
 * <p>It had no spec. It is built in an injection context with a stub service, and never
 * rendered.</p>
 */
describe('AppointmentDetailModalComponent', () => {
  it('exposes the injected detail view service to its template', () => {
    const service = { isVisible: false };
    TestBed.configureTestingModule({
      providers: [{ provide: AppointmentDetailViewService, useValue: service }],
    });

    const c = TestBed.runInInjectionContext(() => new AppointmentDetailModalComponent());

    expect(c.service as unknown).toBe(service);
    TestBed.resetTestingModule();
  });
});
