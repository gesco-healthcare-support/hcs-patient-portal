import { Injectable } from '@angular/core';
import { NgbDateAdapter, NgbDateStruct } from '@ng-bootstrap/ng-bootstrap';
import { toDateKey, toDateKeyFromApi } from './availability-rules';

/**
 * Bridges `ngbDatepicker`'s internal `NgbDateStruct` and the `YYYY-MM-DD` key that
 * `AvailabilityCalendarComponent` exposes as its `selectedDate` contract.
 *
 * <p>WHY THIS EXISTS (defect #6, 2026-08-03). `ngbDatepicker` runs every control value through the
 * ambient `NgbDateAdapter`, and the ambient one is decided by whoever HOSTS the component: both
 * booking surfaces provide ABP's (`NgbDateAdapter<string>`), while the reschedule modal (4b) and this
 * component's own spec would fall back to ng-bootstrap's default (`NgbDateAdapter<NgbDateStruct>`).
 * The same control value therefore rendered differently per host -- feeding a struct to a
 * string adapter made `fromModel` return null and the input displayed EMPTY. Pinning an adapter on
 * the component makes the model shape part of the component's contract instead of an accident of
 * placement.</p>
 *
 * <p>Deliberately local rather than reusing ABP's `DateAdapter`: behaviour is identical for a plain
 * `YYYY-MM-DD` key, but this keeps the component free of the ABP DI barrel (matching its no-ABP
 * design note), reuses the same local-midnight helpers the rules are tested against, and is itself
 * unit-testable. ABP's variant round-trips through `Date.parse` plus a timezone-offset correction,
 * which is machinery a plain calendar date does not need.</p>
 */
@Injectable()
export class AvailabilityDateAdapter extends NgbDateAdapter<string> {
  /** Tolerates a full ISO timestamp as well as a bare key, so an API value cannot render blank. */
  fromModel(value: string | null): NgbDateStruct | null {
    const key = toDateKeyFromApi(value);
    if (!key) {
      return null;
    }
    const [year, month, day] = key.split('-').map(Number);
    return year && month && day ? { year, month, day } : null;
  }

  toModel(date: NgbDateStruct | null): string | null {
    return date ? toDateKey(date.year, date.month, date.day) : null;
  }
}
