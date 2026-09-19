import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';

import { RestService } from '@abp/ng.core';

import { PublicChangeRequestConsentComponent } from './public-change-request-consent.component';

/**
 * F-015 regression: the public consent page uses the verb form in "A request to
 * {verb} appointment ..." -- "cancel"/"reschedule", never the noun
 * "cancellation". changeRequestType 2 = reschedule, otherwise cancel.
 */
describe('PublicChangeRequestConsentComponent actionVerb (F-015)', () => {
  let fixture: ComponentFixture<PublicChangeRequestConsentComponent>;
  let component: PublicChangeRequestConsentComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PublicChangeRequestConsentComponent],
      providers: [
        // The component reads route.snapshot.paramMap at field-init and calls
        // load() (-> rest.request) in its constructor; stub both so it builds.
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => 'test-token' } } },
        },
        { provide: RestService, useValue: { request: () => ({ subscribe: () => undefined }) } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(PublicChangeRequestConsentComponent);
    component = fixture.componentInstance;
  });

  it('uses the verb "reschedule" for a reschedule request', () => {
    component.info = { changeRequestType: 2 } as typeof component.info;
    expect(component.actionVerb).toBe('reschedule');
  });

  it('uses the verb "cancel" (not "cancellation") for a cancel request', () => {
    component.info = { changeRequestType: 1 } as typeof component.info;
    expect(component.actionVerb).toBe('cancel');
  });
});
