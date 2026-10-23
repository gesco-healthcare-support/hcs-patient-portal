import { TestBed } from '@angular/core/testing';
import { RestService } from '@abp/ng.core';
import { of } from 'rxjs';
import { AccountActivationService } from './account-activation.service';

describe('AccountActivationService', () => {
  let rest: jasmine.SpyObj<RestService>;
  let service: AccountActivationService;

  beforeEach(() => {
    rest = jasmine.createSpyObj<RestService>('RestService', ['request']);
    rest.request.and.returnValue(of(undefined) as never);
    TestBed.configureTestingModule({ providers: [{ provide: RestService, useValue: rest }] });
    service = TestBed.inject(AccountActivationService);
  });

  it('posts a user resend to the hand-written controller route', () => {
    service.resendToUser('u-1').subscribe();
    expect(rest.request).toHaveBeenCalledOnceWith(
      { method: 'POST', url: '/api/app/account-activation/users/u-1/resend' },
      { apiName: 'Default' },
    );
  });

  it('posts an office administrator resend to the hand-written controller route', () => {
    service.resendToOfficeAdministrator('o-1').subscribe();
    expect(rest.request).toHaveBeenCalledOnceWith(
      { method: 'POST', url: '/api/app/account-activation/offices/o-1/resend-administrator' },
      { apiName: 'Default' },
    );
  });
});
