import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SmartyAddressProvider, SmartyConfig } from './smarty-address.provider';

/**
 * F2 / address validation (2026-05-29) -- tests for the Smarty adapter's HTTP
 * mapping + graceful degradation. All address values are fictional examples.
 */
describe('SmartyAddressProvider', () => {
  const config: SmartyConfig = {
    key: 'test-key',
    autocompleteUrl: 'https://auto.example/lookup',
    verifyUrl: 'https://verify.example/street',
  };
  let httpMock: HttpTestingController;
  let provider: SmartyAddressProvider;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
    provider = new SmartyAddressProvider(TestBed.inject(HttpClient), config);
  });

  afterEach(() => httpMock.verify());

  describe('autocomplete', () => {
    it('does not call the API under 3 characters', (done) => {
      provider.autocomplete('ab').subscribe((r) => {
        expect(r).toEqual([]);
        done();
      });
      httpMock.expectNone(() => true);
    });

    it('maps Smarty suggestions and sends the key + search params', (done) => {
      provider.autocomplete('100 main').subscribe((r) => {
        expect(r.length).toBe(1);
        expect(r[0].street).toBe('100 Main St');
        expect(r[0].city).toBe('Springfield');
        expect(r[0].state).toBe('IL');
        expect(r[0].zip).toBe('62704');
        done();
      });
      const req = httpMock.expectOne((r) => r.url === config.autocompleteUrl);
      expect(req.request.params.get('key')).toBe('test-key');
      expect(req.request.params.get('search')).toBe('100 main');
      req.flush({
        suggestions: [
          { street_line: '100 Main St', city: 'Springfield', state: 'IL', zipcode: '62704' },
        ],
      });
    });

    it('returns [] on a transport error', (done) => {
      provider.autocomplete('100 main').subscribe((r) => {
        expect(r).toEqual([]);
        done();
      });
      httpMock.expectOne(() => true).error(new ProgressEvent('error'));
    });
  });

  describe('validate', () => {
    it('reports unverified without calling the API when street is empty', (done) => {
      provider.validate({ street: '' }).subscribe((r) => {
        expect(r.status).toBe('unverified');
        done();
      });
      httpMock.expectNone(() => true);
    });

    it('builds a standardized address from candidate components', (done) => {
      provider
        .validate({ street: '100 main', city: 'springfield', state: 'IL', zip: '62704' })
        .subscribe((r) => {
          expect(r.status).toBe('corrected');
          expect(r.matchesInput).toBeFalse();
          expect(r.standardized!.street).toBe('100 Main St');
          expect(r.standardized!.city).toBe('Springfield');
          expect(r.standardized!.state).toBe('IL');
          expect(r.standardized!.zip).toBe('62704-1234');
          done();
        });
      httpMock
        .expectOne((r) => r.url === config.verifyUrl)
        .flush([
          {
            delivery_line_1: '100 Main St',
            components: {
              primary_number: '100',
              street_name: 'Main',
              street_suffix: 'St',
              city_name: 'Springfield',
              state_abbreviation: 'IL',
              zipcode: '62704',
              plus4_code: '1234',
            },
          },
        ]);
    });

    it('reports error (never throws) on a transport failure', (done) => {
      provider.validate({ street: '100 main' }).subscribe((r) => {
        expect(r.status).toBe('error');
        expect(r.matchesInput).toBeTrue();
        done();
      });
      httpMock.expectOne(() => true).error(new ProgressEvent('error'));
    });
  });
});
