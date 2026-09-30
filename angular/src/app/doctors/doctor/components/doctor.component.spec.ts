import { TestBed } from '@angular/core/testing';
import { ListService, PermissionService } from '@abp/ng.core';

import type { DoctorWithNavigationPropertiesDto } from '../../../proxy/doctors/models';
import { DoctorViewService } from '../services/doctor.service';
import { DoctorDetailViewService } from '../services/doctor-detail.service';
import { DoctorComponent } from './doctor.component';

/**
 * The doctor list page (`doctor-management/doctors`): what it starts on load, whether it shows the
 * row action buttons, and the actions it hands to its two view services.
 *
 * <p>Built in an injection context with recording stub services and never rendered, like
 * `doctor-detail.component.spec.ts`.</p>
 */
describe('DoctorComponent', () => {
  const record = { doctor: { id: 'TEST-doctor-1' } } as DoctorWithNavigationPropertiesDto;

  let calls: string[];
  let granted: Record<string, boolean>;
  let detail: { selected: unknown; showForm: () => void; update: (r: unknown) => void };

  function build(policies: Record<string, boolean>): DoctorComponent {
    calls = [];
    granted = policies;
    const view = {
      hookToQuery: () => calls.push('hookToQuery'),
      clearFilters: () => calls.push('clearFilters'),
      delete: (r: DoctorWithNavigationPropertiesDto) => calls.push(`delete:${r.doctor!.id}`),
    };
    detail = {
      selected: { doctor: { id: 'TEST-previous' } },
      showForm: () => calls.push('showForm'),
      update: (r: unknown) =>
        calls.push(`update:${(r as DoctorWithNavigationPropertiesDto).doctor!.id}`),
    };
    const permissions = {
      getGrantedPolicy: (policy: string) => {
        calls.push(`policy:${policy}`);
        return granted[policy] ?? false;
      },
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: ListService, useValue: {} },
        { provide: DoctorViewService, useValue: view },
        { provide: DoctorDetailViewService, useValue: detail },
        { provide: PermissionService, useValue: permissions },
      ],
    });
    return TestBed.runInInjectionContext(() => new DoctorComponent());
  }

  afterEach(() => TestBed.resetTestingModule());

  it('starts the list query on load', () => {
    const component = build({});

    component.ngOnInit();

    expect(calls[0]).toBe('hookToQuery');
  });

  it('shows the row actions when the user may edit OR delete, and hides them when neither', () => {
    const editor = build({ 'CaseEvaluation.Doctors.Edit': true });
    editor.ngOnInit();
    expect(editor['isActionButtonVisible']).toBe(true);
    TestBed.resetTestingModule();

    const deleter = build({ 'CaseEvaluation.Doctors.Delete': true });
    deleter.ngOnInit();
    expect(deleter['isActionButtonVisible']).toBe(true);
    TestBed.resetTestingModule();

    const reader = build({});
    reader.ngOnInit();
    expect(reader['isActionButtonVisible']).toBe(false);
  });

  it('works out the action visibility once and does not ask for the policies again', () => {
    const component = build({ 'CaseEvaluation.Doctors.Edit': true });
    component.ngOnInit();
    const policyChecks = calls.filter((c) => c.startsWith('policy:')).length;

    component.checkActionButtonVisibility();

    expect(calls.filter((c) => c.startsWith('policy:')).length).toBe(policyChecks);
  });

  it('opens an empty form for create, clearing the previously selected doctor', () => {
    const component = build({});

    component.create();

    expect(detail.selected).toBeUndefined();
    expect(calls).toEqual(['showForm']);
  });

  it('hands clear-filters, show-form, edit and delete to the view services', () => {
    const component = build({});

    component.clearFilters();
    component.showForm();
    component.update(record);
    component.delete(record);

    expect(calls).toEqual([
      'clearFilters',
      'showForm',
      'update:TEST-doctor-1',
      'delete:TEST-doctor-1',
    ]);
  });
});
