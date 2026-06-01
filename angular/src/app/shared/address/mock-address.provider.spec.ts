import { MockAddressProvider } from './mock-address.provider';

/**
 * F2 / address validation (2026-05-29) -- tests for the deterministic dev/test
 * provider. All address values here are fictional examples, never patient PHI.
 */
describe('MockAddressProvider', () => {
  let provider: MockAddressProvider;

  beforeEach(() => {
    provider = new MockAddressProvider();
  });

  describe('autocomplete', () => {
    it('returns nothing under 3 characters', (done) => {
      provider.autocomplete('ab').subscribe((r) => {
        expect(r).toEqual([]);
        done();
      });
    });

    it('returns query-echoing suggestions at 3+ characters', (done) => {
      provider.autocomplete('100').subscribe((r) => {
        expect(r.length).toBe(2);
        expect(r[0].street).toContain('100');
        expect(r[0].state).toBe('IL');
        expect(r[1].suite).toBe('Ste 200');
        done();
      });
    });
  });

  describe('validate', () => {
    it('reports unverified when street is empty', (done) => {
      provider.validate({ street: '', city: 'x' }).subscribe((r) => {
        expect(r.status).toBe('unverified');
        expect(r.matchesInput).toBeTrue();
        expect(r.standardized).toBeUndefined();
        done();
      });
    });

    it('corrects lowercase input to USPS uppercase + ZIP+4 + state code', (done) => {
      provider
        .validate({ street: '100 test st', city: 'springfield', state: 'Illinois', zip: '62704' })
        .subscribe((r) => {
          expect(r.status).toBe('corrected');
          expect(r.matchesInput).toBeFalse();
          expect(r.standardized!.street).toBe('100 TEST ST');
          expect(r.standardized!.city).toBe('SPRINGFIELD');
          expect(r.standardized!.state).toBe('IL');
          expect(r.standardized!.zip).toBe('62704-0000');
          done();
        });
    });

    it('reports verified when the input is already standardized', (done) => {
      provider
        .validate({ street: '100 TEST ST', city: 'SPRINGFIELD', state: 'IL', zip: '62704-0000' })
        .subscribe((r) => {
          expect(r.status).toBe('verified');
          expect(r.matchesInput).toBeTrue();
          done();
        });
    });
  });
});
