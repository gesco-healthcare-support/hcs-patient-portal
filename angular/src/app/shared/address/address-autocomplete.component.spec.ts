import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { config, of, throwError } from 'rxjs';

import { AddressAutocompleteComponent } from './address-autocomplete.component';
import { AddressSuggestion, AddressValidationProvider } from './address-validation.provider';
import { PatientService } from '../../proxy/patients/patient.service';

/**
 * The street-address typeahead: typing queries the provider, a pick fills the whole address
 * group, and the state is resolved to the id the state select is keyed by.
 *
 * <p>It had no spec. It is built in an injection context and never rendered. The query stream
 * is debounced, so the typing tests run under `fakeAsync` and advance the virtual clock; each
 * destroys the component, so nothing is left scheduled. `onBlur`'s deferred close is a real
 * `setTimeout` and is not driven here.</p>
 *
 * <p>All addresses below are synthetic.</p>
 */
describe('AddressAutocompleteComponent', () => {
  let autocomplete: jasmine.Spy;

  interface Probe {
    [key: string]: any;
  }

  const suggestion: AddressSuggestion = {
    text: '1 Example Way, Encino, CA',
    street: '1 Example Way',
    suite: 'Apt 2',
    city: 'Encino',
    state: 'CA',
    zip: '00000',
  };

  function create(options: { stateLookupFails?: boolean } = {}): Probe {
    autocomplete = jasmine.createSpy('autocomplete').and.returnValue(of([suggestion]));
    TestBed.configureTestingModule({
      providers: [
        { provide: AddressValidationProvider, useValue: { autocomplete } },
        {
          provide: PatientService,
          useValue: {
            getStateLookup: () =>
              options.stateLookupFails
                ? throwError(() => ({ status: 500 }))
                : of({ items: [{ id: 'state-1', displayName: 'California' }] }),
          },
        },
      ],
    });
    const c = TestBed.runInInjectionContext(
      () => new AddressAutocompleteComponent(),
    ) as unknown as Probe;
    c.group = new FormGroup({
      street: new FormControl(''),
      address: new FormControl(null),
      city: new FormControl(null),
      stateId: new FormControl(null),
      zipCode: new FormControl(null),
    });
    c.fields = {
      street: 'street',
      suite: 'address',
      city: 'city',
      state: 'stateId',
      zip: 'zipCode',
    };
    return c;
  }

  const typed = (value: string) => ({ target: { value } }) as unknown as Event;

  afterEach(() => TestBed.resetTestingModule());

  describe('typing', () => {
    it('queries the provider with the trimmed text once typing pauses, and opens the list', fakeAsync(() => {
      const c = create();
      c.ngOnInit();

      c.onType(typed('  1 Example  '));
      tick(249);
      expect(autocomplete).withContext('still inside the debounce').not.toHaveBeenCalled();
      tick(1);

      expect(autocomplete).toHaveBeenCalledWith('1 Example');
      expect(c.suggestions()).toEqual([suggestion]);
      expect(c.open()).toBeTrue();
      c.ngOnDestroy();
    }));

    it('keeps the list closed when nothing matches', fakeAsync(() => {
      const c = create();
      autocomplete.and.returnValue(of([]));
      c.ngOnInit();

      c.onType(typed('zzz'));
      tick(250);

      expect(c.suggestions()).toEqual([]);
      expect(c.open()).toBeFalse();
      c.ngOnDestroy();
    }));
  });

  describe('focusing the field', () => {
    it('reopens the list when there are suggestions to show', () => {
      const c = create();
      c.suggestions.set([suggestion]);
      c.onFocus();
      expect(c.open()).toBeTrue();
    });

    it('keeps it closed when there are none', () => {
      const c = create();
      c.onFocus();
      expect(c.open()).toBeFalse();
    });
  });

  describe('picking a suggestion', () => {
    it('fills the whole address group and resolves the state to its id', () => {
      const c = create();
      c.ngOnInit();
      c.suggestions.set([suggestion]);
      c.open.set(true);

      c.select(suggestion);

      expect(c.group.getRawValue()).toEqual({
        street: '1 Example Way',
        address: 'Apt 2',
        city: 'Encino',
        stateId: 'state-1',
        zipCode: '00000',
      });
      expect(c.group.dirty).toBeTrue();
      expect(c.open()).toBeFalse();
      expect(c.suggestions()).toEqual([]);
      c.ngOnDestroy();
    });

    it('leaves the unit and state alone when the pick carries neither', () => {
      const c = create();
      c.group.patchValue({ address: 'Suite 9', stateId: 'state-7' });

      c.select({ ...suggestion, suite: undefined, state: 'ZZ' });

      expect(c.group.getRawValue().address).toBe('Suite 9');
      expect(c.group.getRawValue().stateId).toBe('state-7');
    });
  });

  it('exposes the street control of its group', () => {
    const c = create();
    expect(c.streetControl).toBe(c.group.get('street'));
  });

  describe('when the state lookup fails (#1113)', () => {
    /**
     * The one-time State lookup subscribed with no error branch, so ABP's rethrown copy of a failed
     * request reached RxJS's unhandled-error path. A pick must still fill the street, city and zip;
     * only the state select, which needs the lookup to resolve an id, is left as it was.
     */
    let unhandled: jasmine.Spy;
    let previous: typeof config.onUnhandledError;

    beforeEach(() => {
      previous = config.onUnhandledError;
      unhandled = jasmine.createSpy('onUnhandledError');
      config.onUnhandledError = unhandled;
    });

    afterEach(() => {
      config.onUnhandledError = previous;
    });

    it('still fills the address but cannot resolve the state', fakeAsync(() => {
      const c = create({ stateLookupFails: true });
      c.ngOnInit();
      tick();
      c.select(suggestion);

      expect(unhandled).not.toHaveBeenCalled();
      expect(c.group.getRawValue().street).toBe('1 Example Way');
      expect(c.group.getRawValue().city).toBe('Encino');
      expect(c.group.getRawValue().stateId).toBeNull();
      c.ngOnDestroy();
    }));
  });
});
