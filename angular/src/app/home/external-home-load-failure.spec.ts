import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { ConfigStateService, ListService, RestService } from '@abp/ng.core';
import { NEVER, Subject, of } from 'rxjs';

import { ExternalHomeComponent } from './external-home.component';
import { AppointmentService } from '../proxy/appointments/appointment.service';

/**
 * A failed appointment-list load (#1113). ABP's ListService swallows the error and emits
 * nothing, so the page has to notice through requestStatus$ or the loading skeleton stays
 * on screen for good.
 */
describe('ExternalHomeComponent failed list load', () => {
  afterEach(() => TestBed.resetTestingModule());

  function create() {
    const status$ = new Subject<string>();
    TestBed.configureTestingModule({
      providers: [
        { provide: ConfigStateService, useValue: { getOne: () => null, getAll: () => ({}) } },
        { provide: RestService, useValue: { request: () => of({}) } },
        { provide: Router, useValue: { navigate: () => undefined } },
      ],
    });
    TestBed.overrideComponent(ExternalHomeComponent, {
      set: {
        providers: [
          { provide: AppointmentService, useValue: {} },
          {
            provide: ListService,
            useValue: { hookToQuery: () => NEVER, requestStatus$: status$.asObservable() },
          },
        ],
      },
    });
    const component = TestBed.createComponent(ExternalHomeComponent)
      .componentInstance as unknown as {
      ngOnInit(): void;
      loading: () => boolean;
    };
    component.ngOnInit();
    return { component, status$ };
  }

  it('stops showing the skeleton when the load fails', () => {
    const { component, status$ } = create();
    expect(component.loading()).toBeTrue();

    status$.next('loading');
    expect(component.loading()).toBeTrue();

    status$.next('error');
    expect(component.loading()).toBeFalse();
  });
});
