import { TestBed } from '@angular/core/testing';

import { DoctorDetailModalComponent } from './doctor-detail.component';
import { DoctorDetailViewService } from '../services/doctor-detail.service';

/**
 * The generated doctor detail modal is a template over its view service; the one thing the class
 * does is expose that service to the template.
 *
 * <p>It had no spec. It is built in an injection context with a stub service, and never
 * rendered.</p>
 */
describe('DoctorDetailModalComponent', () => {
  it('exposes the injected detail view service to its template', () => {
    const service = { isVisible: false };
    TestBed.configureTestingModule({
      providers: [{ provide: DoctorDetailViewService, useValue: service }],
    });

    const c = TestBed.runInInjectionContext(() => new DoctorDetailModalComponent());

    expect(c.service as unknown).toBe(service);
    TestBed.resetTestingModule();
  });
});
