import { BookingSubmitMode } from '../../proxy/enums/booking-submit-mode.enum';
import { isRevalWithoutSource } from './reval-without-source.util';

describe('reval-without-source util', () => {
  it('uses the server wire value 4', () => {
    expect(BookingSubmitMode.RevalWithoutSource as number).toBe(4);
  });

  it('applies only to a ticked re-evaluation', () => {
    expect(isRevalWithoutSource('reval', true)).toBeTrue();
    expect(isRevalWithoutSource('reval', false)).toBeFalse();
    expect(isRevalWithoutSource('reBook', true)).toBeFalse();
    expect(isRevalWithoutSource('reRequest', true)).toBeFalse();
    expect(isRevalWithoutSource('new', true)).toBeFalse();
  });
});
