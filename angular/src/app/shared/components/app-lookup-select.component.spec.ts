import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { FormControl, NgControl } from '@angular/forms';
import { LocalizationService } from '@abp/ng.core';
import { config, of, throwError } from 'rxjs';

import { AppLookupSelectComponent } from './app-lookup-select.component';

/**
 * The local `<app-lookup-select>` wrapper. `get()` used to subscribe with no error branch, so
 * ABP's rethrown copy of a failed lookup reached RxJS's unhandled-error path (#1113). The
 * dropdown must simply stay as it was; ABP's RestService has already shown the failure.
 *
 * <p>The component is created but never change-detected. The global unhandled-error hook is
 * replaced inside each test's lifetime and restored afterwards.</p>
 */
describe('AppLookupSelectComponent', () => {
  let unhandled: jasmine.Spy;
  let previous: typeof config.onUnhandledError;

  interface Probe {
    [key: string]: any;
  }

  function create(getFn: () => unknown): Probe {
    // The ABP base reads its own NgControl; a bare control is all it needs here.
    TestBed.configureTestingModule({
      providers: [
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        { provide: NgControl, useValue: { control: new FormControl(), valueAccessor: null } },
      ],
    });
    const fixture = TestBed.createComponent(AppLookupSelectComponent);
    const c = fixture.componentInstance as unknown as Probe;
    c.getFn = getFn;
    return c;
  }

  beforeEach(() => {
    previous = config.onUnhandledError;
    unhandled = jasmine.createSpy('onUnhandledError');
    config.onUnhandledError = unhandled;
  });

  afterEach(() => {
    config.onUnhandledError = previous;
    TestBed.resetTestingModule();
  });

  it('can see an unhandled error at all (detector self-check)', fakeAsync(() => {
    throwError(() => new Error('probe')).subscribe({ next: () => undefined });
    tick();
    expect(unhandled).toHaveBeenCalled();
  }));

  it('fills the options from the lookup', fakeAsync(() => {
    const c = create(() => of({ items: [{ key: 'a', displayName: 'A' }] }));
    c.get();
    tick();
    expect(c.datas).toEqual([{ key: 'a', displayName: 'A' }]);
  }));

  it('keeps the options it already had when the lookup fails', fakeAsync(() => {
    const kept = [{ key: 'a', displayName: 'A' }];
    const c = create(() => throwError(() => ({ status: 500 })));
    c.datas = kept;

    c.get();
    tick();

    expect(unhandled).not.toHaveBeenCalled();
    expect(c.datas).toBe(kept);
  }));
});
