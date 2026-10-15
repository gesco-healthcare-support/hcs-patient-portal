import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { RestService } from '@abp/ng.core';
import { of, throwError } from 'rxjs';
import { PublicChangeRequestConsentComponent } from './public-change-request-consent.component';

/**
 * The consent flow: loading the request, responding to it, and what the page says when any of
 * that fails.
 *
 * A SIBLING to public-change-request-consent.component.spec.ts, whose describe is named
 * "actionVerb (F-015)" and covers exactly that. Load and error behaviour would make that name
 * false.
 *
 * THIS COMPONENT LOADS FROM ITS CONSTRUCTOR (:107), so every double must be armed BEFORE the
 * component is created. That is why `configure()` builds the TestBed and returns the instance in
 * one step and why nothing is re-stubbed afterwards -- arming a spy after construction is the
 * mistake that produced seven failures in tranche 6.
 *
 * THE 403/404 TEST IS THE MOST IMPORTANT ONE HERE, for the same reason as the public upload page:
 * this is an anonymous surface, and distinguishing "invalid token" from "expired" would let a
 * caller probe which consent tokens exist.
 */
describe('PublicChangeRequestConsentComponent flow', () => {
  let request: jasmine.Spy;

  const pending = {
    confirmationNumber: 'RCN-2001',
    changeRequestType: 2, // reschedule
    reason: 'Clinic rescheduling',
    requestedNewDateTime: '2026-02-01T09:00:00',
    consentStatus: 1, // Pending
  };

  /**
   * Builds the TestBed and constructs in one call, because the constructor loads. `response` is
   * whatever the initial GET should return -- armed before construction, never after.
   */
  function configure(
    response: unknown = of(pending),
    token = 'token-1',
  ): PublicChangeRequestConsentComponent {
    TestBed.resetTestingModule();
    request = jasmine.createSpy('request').and.returnValue(response);
    TestBed.configureTestingModule({
      imports: [PublicChangeRequestConsentComponent],
      providers: [
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => token } } },
        },
        { provide: RestService, useValue: { request } },
      ],
    });
    return TestBed.createComponent(PublicChangeRequestConsentComponent).componentInstance;
  }

  describe('load, which runs from the constructor', () => {
    it('fetches the consent record for the route token', () => {
      configure();
      const [config] = request.calls.mostRecent().args as [{ method: string; url: string }];
      expect(config.method).toBe('GET');
      expect(config.url).toBe('/api/public/change-request-consent/token-1');
    });

    it('owns its error UX rather than letting the app shell handle it', () => {
      configure();
      const [, options] = request.calls.mostRecent().args as [
        unknown,
        { skipHandleError: boolean },
      ];
      expect(options.skipHandleError).toBeTrue();
    });

    it('becomes ready for a still-pending request', () => {
      const cmp = configure();
      expect(cmp.state).toBe('ready');
      expect(cmp.info).toEqual(pending);
    });

    it('goes straight to done when a decision was already recorded', () => {
      const cmp = configure(of({ ...pending, consentStatus: 2 }));
      expect(cmp.state).toBe('done');
    });

    it('errors WITHOUT calling the service when the link carries no token', () => {
      const cmp = configure(of(pending), '');
      expect(request).not.toHaveBeenCalled();
      expect(cmp.state).toBe('error');
      expect(cmp.errorMessage).toBe('This consent link is invalid.');
    });

    // The positive control for the test above: with a token, it DOES call out.
    it('calls the service when a token is present', () => {
      configure();
      expect(request).toHaveBeenCalled();
    });
  });

  describe('the noun form, which is not the verb form', () => {
    // F-015 was about these two being confused. The existing sibling spec pins
    // actionVerb; nothing pinned actionWord, so the pair could drift back into
    // agreement and only one of them would complain.
    it('uses the NOUN for a cancellation, where the verb would be "cancel"', () => {
      const cmp = configure(of({ ...pending, changeRequestType: 1 }));
      expect(cmp.actionWord).toBe('cancellation');
      expect(cmp.actionVerb).toBe('cancel');
    });

    it('uses the same word for a reschedule, where noun and verb coincide', () => {
      const cmp = configure(of({ ...pending, changeRequestType: 2 }));
      expect(cmp.actionWord).toBe('reschedule');
      expect(cmp.actionVerb).toBe('reschedule');
    });
  });

  describe('alreadyDecided and the closing message', () => {
    it('treats only Pending as undecided', () => {
      expect(configure(of({ ...pending, consentStatus: 1 })).alreadyDecided).toBeFalse();
    });

    it('treats an approval as decided', () => {
      const cmp = configure(of({ ...pending, consentStatus: 2 }));
      expect(cmp.alreadyDecided).toBeTrue();
      expect(cmp.decisionMessage).toBe(
        'You agreed to this request. Our clinic staff will finalize it.',
      );
    });

    it('treats a rejection as decided', () => {
      const cmp = configure(of({ ...pending, consentStatus: 3 }));
      expect(cmp.decisionMessage).toBe(
        'You declined this request. Our clinic staff has been notified.',
      );
    });

    it('explains an expired link as referred to staff', () => {
      const cmp = configure(of({ ...pending, consentStatus: 4 }));
      expect(cmp.decisionMessage).toBe(
        'This link has expired, so the request was referred to our clinic staff.',
      );
    });

    it('falls back to a neutral acknowledgement for any other status', () => {
      const cmp = configure(of({ ...pending, consentStatus: 0 }));
      expect(cmp.decisionMessage).toBe('Thank you -- your response has been recorded.');
    });
  });

  describe('respond', () => {
    it('posts the decision for the route token', () => {
      const cmp = configure();
      cmp.respond(true);
      const [config] = request.calls.mostRecent().args as [
        { method: string; url: string; body: { approved: boolean } },
      ];
      expect(config.method).toBe('POST');
      expect(config.url).toBe('/api/public/change-request-consent/token-1');
      expect(config.body).toEqual({ approved: true });
    });

    it('carries a decline through as approved false', () => {
      const cmp = configure();
      cmp.respond(false);
      const [config] = request.calls.mostRecent().args as [{ body: { approved: boolean } }];
      expect(config.body).toEqual({ approved: false });
    });

    it('adopts the server response and closes the page', () => {
      const cmp = configure();
      const decided = { ...pending, consentStatus: 3 };
      request.and.returnValue(of(decided));
      cmp.respond(false);
      expect(cmp.info).toEqual(decided);
      expect(cmp.state).toBe('done');
    });

    it('refuses a second submit while one is in flight', () => {
      const cmp = configure();
      cmp.state = 'submitting';
      request.calls.reset();
      cmp.respond(true);
      expect(request).not.toHaveBeenCalled();
    });
  });

  describe('error messages are driven by status, never by the server body', () => {
    function messageFor(status: number): string {
      const cmp = configure(throwError(() => new HttpErrorResponse({ status })));
      expect(cmp.state).toBe('error');
      return cmp.errorMessage;
    }

    /**
     * THE ANTI-ENUMERATION PROPERTY, and the reason this component's fail() collapses two cases
     * that look like they want distinct copy. An anonymous caller must not learn whether a token
     * exists. If these messages ever diverge, this page becomes an oracle.
     */
    it('gives 403 and 404 the SAME message, so neither confirms a token exists', () => {
      expect(messageFor(403)).toBe(messageFor(404));
    });

    it('and that shared message names neither cause', () => {
      expect(messageFor(403)).toBe('This consent link is invalid or has expired.');
    });

    it('asks the caller to wait after too many attempts', () => {
      expect(messageFor(429)).toBe('Too many attempts. Please wait a while and try again.');
    });

    it('reports a connection failure as a network problem', () => {
      expect(messageFor(0)).toBe('Network error. Check your connection and try again.');
    });

    it('falls back to a generic message for anything else', () => {
      expect(messageFor(500)).toBe('Sorry, something went wrong. Please try again.');
    });

    it('uses the same wording when a RESPONSE fails, not just a load', () => {
      const cmp = configure();
      request.and.returnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
      cmp.respond(true);
      expect(cmp.state).toBe('error');
      expect(cmp.errorMessage).toBe('This consent link is invalid or has expired.');
    });
  });
});
