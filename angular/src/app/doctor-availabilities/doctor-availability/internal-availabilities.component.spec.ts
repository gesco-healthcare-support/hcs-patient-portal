import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { DoctorAvailabilityService } from '../../proxy/doctor-availabilities/doctor-availability.service';
import { InternalAvailabilitiesComponent } from './internal-availabilities.component';

/**
 * Covers the "All locations" default (issue #1): on load the page should query
 * every location's slots (locationId omitted), a specific selection should scope
 * the query, and the location dropdown should carry an "All locations" option.
 */
describe('InternalAvailabilitiesComponent -- all-locations view', () => {
  let getListSpy: jasmine.Spy;

  beforeEach(() => {
    getListSpy = jasmine.createSpy('getList').and.returnValue(of({ items: [] }));
    const serviceMock = {
      getLocationLookup: jasmine.createSpy('getLocationLookup').and.returnValue(
        of({
          items: [
            { id: 'loc-1', displayName: 'Downtown' },
            { id: 'loc-2', displayName: 'Uptown' },
          ],
        }),
      ),
      getList: getListSpy,
      getSlotPatientNames: jasmine.createSpy('getSlotPatientNames').and.returnValue(of([])),
      delete: jasmine.createSpy('delete').and.returnValue(of(undefined)),
      deleteByDate: jasmine
        .createSpy('deleteByDate')
        .and.returnValue(of({ deletedCount: 0, skippedSlotIds: [] })),
    };

    TestBed.configureTestingModule({
      imports: [InternalAvailabilitiesComponent],
      providers: [
        { provide: DoctorAvailabilityService, useValue: serviceMock },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
        { provide: Router, useValue: { navigateByUrl: () => Promise.resolve(true) } },
      ],
    });
  });

  function create() {
    const fixture = TestBed.createComponent(InternalAvailabilitiesComponent);
    fixture.detectChanges(); // triggers ngOnInit + first render
    return fixture;
  }

  it('defaults to "All locations" (empty locationId) on load', () => {
    const cmp = create().componentInstance as unknown as { locationId: () => string };
    expect(cmp.locationId()).toBe('');
  });

  it('omits locationId from getList while All locations is selected', () => {
    create();
    expect(getListSpy).toHaveBeenCalled();
    expect(getListSpy.calls.mostRecent().args[0].locationId).toBeUndefined();
  });

  it('sends the chosen locationId when a specific location is selected', () => {
    const cmp = create().componentInstance as unknown as {
      onLocationChange: (id: string) => void;
    };
    cmp.onLocationChange('loc-2');
    expect(getListSpy.calls.mostRecent().args[0].locationId).toBe('loc-2');
  });

  it('renders an "All locations" option plus one per location', () => {
    const el = create().nativeElement as HTMLElement;
    const options = el.querySelectorAll('#ia-location option');
    expect(options.length).toBe(3);
    expect(options[0].textContent?.trim()).toBe('All locations');
  });
});
