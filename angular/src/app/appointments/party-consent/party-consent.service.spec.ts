import { TestBed } from '@angular/core/testing';
import { RestService } from '@abp/ng.core';
import { of } from 'rxjs';

import { PartyConsentService } from './party-consent.service';

/** The hand-written client must hit the routes PartyChangeRequestConsentController serves. */
describe('PartyConsentService', () => {
  let rest: { request: jasmine.Spy };
  let service: PartyConsentService;

  beforeEach(() => {
    rest = { request: jasmine.createSpy('request').and.returnValue(of({})) };
    TestBed.configureTestingModule({ providers: [{ provide: RestService, useValue: rest }] });
    service = TestBed.inject(PartyConsentService);
  });

  it('lists my waiting requests for one appointment', () => {
    service.getMine('appt-1').subscribe();
    expect(rest.request).toHaveBeenCalledWith(
      {
        method: 'GET',
        url: '/api/app/change-request-consents/mine',
        params: { appointmentId: 'appt-1' },
      },
      { apiName: 'Default' },
    );
  });

  it('lists every waiting request when no appointment is named', () => {
    service.getMine().subscribe();
    expect(rest.request.calls.mostRecent().args[0].params).toEqual({});
  });

  it('posts the answer', () => {
    service.decide('cr-1', false).subscribe();
    expect(rest.request).toHaveBeenCalledWith(
      {
        method: 'POST',
        url: '/api/app/change-request-consents/cr-1/decision',
        body: { approved: false },
      },
      { apiName: 'Default' },
    );
  });
});
