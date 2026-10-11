import { TestBed } from '@angular/core/testing';
import { RestService } from '@abp/ng.core';
import { of } from 'rxjs';

import { OfficeLetterheadService } from './office-letterhead.service';

/** The hand-written letterhead client must hit the routes OfficeLetterheadController serves. */
describe('OfficeLetterheadService', () => {
  let rest: { request: jasmine.Spy };
  let service: OfficeLetterheadService;

  beforeEach(() => {
    rest = { request: jasmine.createSpy('request').and.returnValue(of({})) };
    TestBed.configureTestingModule({ providers: [{ provide: RestService, useValue: rest }] });
    service = TestBed.inject(OfficeLetterheadService);
  });

  it('reads the current office letterhead', () => {
    service.get().subscribe();
    expect(rest.request).toHaveBeenCalledWith(
      { method: 'GET', url: '/api/app/branding/letterhead' },
      { apiName: 'Default' },
    );
  });

  it('saves the whole letterhead with PUT', () => {
    const body = { phone: '555-0100' };
    service.update(body).subscribe();
    expect(rest.request).toHaveBeenCalledWith(
      { method: 'PUT', url: '/api/app/branding/letterhead', body },
      { apiName: 'Default' },
    );
  });
});
