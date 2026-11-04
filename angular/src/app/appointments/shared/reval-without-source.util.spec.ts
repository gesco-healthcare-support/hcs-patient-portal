import {
  REVAL_WITHOUT_SOURCE_SUBMIT_MODE,
  canOfferRevalWithoutSource,
  isRevalWithoutSource,
} from './reval-without-source.util';

describe('reval-without-source util', () => {
  it('uses the server wire value 4', () => {
    expect(REVAL_WITHOUT_SOURCE_SUBMIT_MODE as number).toBe(4);
  });

  it('applies only to a ticked re-evaluation', () => {
    expect(isRevalWithoutSource('reval', true)).toBeTrue();
    expect(isRevalWithoutSource('reval', false)).toBeFalse();
    expect(isRevalWithoutSource('reBook', true)).toBeFalse();
    expect(isRevalWithoutSource('reRequest', true)).toBeFalse();
    expect(isRevalWithoutSource('new', true)).toBeFalse();
  });

  it('offers the option only for a re-evaluation with no source loaded', () => {
    expect(canOfferRevalWithoutSource('reval', null)).toBeTrue();
    expect(canOfferRevalWithoutSource('reval', 'C0001')).toBeFalse();
    expect(canOfferRevalWithoutSource('reBook', null)).toBeFalse();
    expect(canOfferRevalWithoutSource('new', null)).toBeFalse();
  });
});
