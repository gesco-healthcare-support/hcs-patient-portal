import { TestBed } from '@angular/core/testing';
import { RestService } from '@abp/ng.core';
import { of } from 'rxjs';
import { ExternalLoginsService } from './external-logins.service';

describe('ExternalLoginsService', () => {
  let rest: jasmine.SpyObj<RestService>;
  let service: ExternalLoginsService;

  beforeEach(() => {
    rest = jasmine.createSpyObj<RestService>('RestService', ['request']);
    rest.request.and.returnValue(of({}) as never);
    TestBed.configureTestingModule({ providers: [{ provide: RestService, useValue: rest }] });
    service = TestBed.inject(ExternalLoginsService);
  });

  it('lists from the external-logins route with the filter, status and paging', () => {
    service.getList({ filterText: 'burgis', isActive: false, skipCount: 20, maxResultCount: 20 });
    const [req] = rest.request.calls.mostRecent().args;
    expect(req.method).toBe('GET');
    expect(req.url).toBe('/api/app/external-logins');
    expect(req.params).toEqual({
      filterText: 'burgis',
      isActive: false,
      skipCount: 20,
      maxResultCount: 20,
    });
  });

  it('leaves the status and text out when they are not set', () => {
    service.getList({ filterText: '', isActive: null, skipCount: 0, maxResultCount: 20 });
    const [req] = rest.request.calls.mostRecent().args;
    expect(req.params).toEqual({
      filterText: undefined,
      isActive: undefined,
      skipCount: 0,
      maxResultCount: 20,
    });
  });

  it('puts the new state on the login', () => {
    service.setActive('u1', { isActive: false });
    const [req] = rest.request.calls.mostRecent().args;
    expect(req.method).toBe('PUT');
    expect(req.url).toBe('/api/app/external-logins/u1/active');
    expect(req.body).toEqual({ isActive: false });
  });
});
