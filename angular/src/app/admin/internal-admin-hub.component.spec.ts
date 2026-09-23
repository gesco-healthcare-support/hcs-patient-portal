import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { ConfigStateService, PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { InternalAdminHubComponent } from './internal-admin-hub.component';
import { AdminSectionGateway, type NtRow, type RoleRow } from './admin-section.gateway';

/**
 * The admin hub component.
 *
 * <p>`admin-hub.util.spec.ts` and `email-template-catalog.spec.ts` cover the pure helpers this
 * component composes. The COMPONENT itself had no spec at all, which is why it sat at 1/254
 * covered lines -- one standalone class serving four different admin surfaces (notification
 * templates, system parameters, the permission matrix, audit logs) with nothing exercising the
 * wiring between them.</p>
 *
 * <p>What is pinned here is that wiring rather than the helpers: which section loads which
 * endpoint, what each failure path leaves on screen, and the gates that stop a tenant-scoped
 * section being requested at host scope (where it 403s and puts ABP's blocking error overlay
 * over the page).</p>
 *
 * <p>Built with `createComponent` and never change-detected. The constructor calls
 * `takeUntilDestroyed()`, which needs a real `DestroyRef` -- `runInInjectionContext` is the
 * lighter pattern used elsewhere in this repo but does not supply one, so the component is
 * created properly and simply never rendered.</p>
 *
 * <p>All template codes, role names and audit rows below are synthetic.</p>
 */
describe('InternalAdminHubComponent', () => {
  let gateway: Record<string, jasmine.Spy>;
  let toaster: { info: jasmine.Spy; success: jasmine.Spy; error: jasmine.Spy };
  let routeData: Subject<Record<string, unknown>>;
  let granted: Set<string>;
  let currentTenant: unknown;

  interface Probe {
    [key: string]: any;
  }

  function ntRow(over: Partial<NtRow> = {}): NtRow {
    return {
      id: 'nt-1',
      code: 'AppointmentRequested',
      typeName: 'Appointment',
      active: true,
      isCustomized: false,
      subject: 'Your appointment request',
      bodyEmail: '<p>Hello ##Patient.FirstName##</p>',
      bodySms: 'Hello',
      concurrencyStamp: 'stamp-1',
      ...over,
    };
  }

  function roleRow(over: Partial<RoleRow> = {}): RoleRow {
    return { name: 'Staff Supervisor', kind: 'Internal', isStatic: false, ...over };
  }

  function create(
    options: { section?: string; policies?: string[]; tenant?: unknown } = {},
  ): Probe {
    routeData = new Subject();
    granted = new Set(options.policies ?? []);
    currentTenant = 'tenant' in options ? options.tenant : null;

    gateway = {
      listTemplateTypes: jasmine.createSpy('listTemplateTypes').and.returnValue(of([])),
      listTemplates: jasmine.createSpy('listTemplates').and.returnValue(of([])),
      getTemplateVariables: jasmine.createSpy('getTemplateVariables').and.returnValue(of([])),
      sendTestTemplate: jasmine.createSpy('sendTestTemplate').and.returnValue(of({})),
      updateTemplate: jasmine.createSpy('updateTemplate').and.returnValue(of({})),
      getParameters: jasmine.createSpy('getParameters').and.returnValue(of(null)),
      updateParameters: jasmine.createSpy('updateParameters').and.returnValue(of({})),
      listRoles: jasmine.createSpy('listRoles').and.returnValue(of([])),
      getPermissions: jasmine.createSpy('getPermissions').and.returnValue(of({ groups: [] })),
      updatePermissions: jasmine.createSpy('updatePermissions').and.returnValue(of({})),
      listAuditLogs: jasmine.createSpy('listAuditLogs').and.returnValue(of([])),
    };
    toaster = {
      info: jasmine.createSpy('info'),
      success: jasmine.createSpy('success'),
      error: jasmine.createSpy('error'),
    };

    TestBed.configureTestingModule({
      imports: [InternalAdminHubComponent],
      providers: [
        { provide: AdminSectionGateway, useValue: gateway },
        {
          provide: PermissionService,
          useValue: { getGrantedPolicy: (p: string) => granted.has(p) },
        },
        {
          provide: ConfigStateService,
          useValue: { getOne: (k: string) => (k === 'currentTenant' ? currentTenant : null) },
        },
        { provide: ToasterService, useValue: toaster },
        { provide: ActivatedRoute, useValue: { data: routeData } },
      ],
    });

    const component = TestBed.createComponent(InternalAdminHubComponent)
      .componentInstance as unknown as Probe;
    if (options.section !== undefined) {
      routeData.next({ section: options.section });
    }
    return component;
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('section routing', () => {
    it('defaults to notification templates when the route carries no section', () => {
      // Inside a clinic, because templates are tenant-scoped: at host scope the load is
      // deliberately suppressed (see activeBlockedAtHost), so asserting the fetch there would
      // be asserting against the gate rather than against the default.
      const c = create({ tenant: { id: 'office-a' } });
      routeData.next({});
      expect(c.section()).toBe('templates');
      expect(gateway['listTemplates']).toHaveBeenCalled();
    });

    it('takes the section from route data', () => {
      const c = create({ section: 'audit' });
      expect(c.section()).toBe('audit');
      expect(gateway['listAuditLogs']).toHaveBeenCalled();
    });

    it('resolves the section metadata', () => {
      const c = create({ section: 'parameters' });
      expect(c.meta().key).toBe('parameters');
      expect(c.meta().label).toBe('System Parameters');
    });

    it('falls back to the first section when the key is unknown', () => {
      const c = create({ section: 'not-a-section' });
      expect(c.meta()).toBe(c.sections[0]);
    });

    it('loads parameters for the parameters section only', () => {
      // Tenant-scoped, so this needs a clinic for the same reason as templates above.
      create({ section: 'parameters', tenant: { id: 'office-a' } });
      expect(gateway['getParameters']).toHaveBeenCalled();
      expect(gateway['listAuditLogs']).not.toHaveBeenCalled();
      expect(gateway['listRoles']).not.toHaveBeenCalled();
    });

    it('loads roles for the roles section only', () => {
      create({ section: 'roles' });
      expect(gateway['listRoles']).toHaveBeenCalled();
      expect(gateway['listAuditLogs']).not.toHaveBeenCalled();
    });

    it('does NOT call the audit endpoint for a self-loading section', () => {
      /**
       * The load() chain ends in an `else` that must NOT be the audit branch. This was an
       * `else` back when audit was the last key, so adding integration-failures silently made
       * the hub fetch audit logs for it -- which 403s for a role without AuditLogging.AuditLogs
       * and puts ABP's blocking error overlay over the whole page. Found in live testing, and
       * this is the test that would have found it first.
       */
      const c = create({ section: 'integration-failures' });
      expect(gateway['listAuditLogs']).not.toHaveBeenCalled();
      expect(gateway['listTemplates']).not.toHaveBeenCalled();
      expect(c.loading()).toBeFalse();
    });

    it('reloads when the route emits a different section', () => {
      const c = create({ section: 'templates' });
      routeData.next({ section: 'audit' });
      expect(c.section()).toBe('audit');
      expect(gateway['listAuditLogs']).toHaveBeenCalled();
    });
  });

  describe('visibility gates', () => {
    it('hides a section whose policy is not granted', () => {
      const c = create({ section: 'audit', policies: [] });
      const audit = c.sections.find((s: any) => s.key === 'audit');
      expect(c.canSee(audit)).toBeFalse();
    });

    it('shows a non-tenant-scoped section at host scope when granted', () => {
      const c = create({ section: 'audit', policies: ['AuditLogging.AuditLogs'], tenant: null });
      const audit = c.sections.find((s: any) => s.key === 'audit');
      expect(c.canSee(audit)).toBeTrue();
    });

    it('hides a tenant-scoped section at host scope even when granted', () => {
      // Tenant-scoped sections 403 at host scope; IT Admin reaches them by switching into a
      // clinic first. Showing the link would be a guaranteed click-into-error.
      const c = create({
        section: 'templates',
        policies: ['CaseEvaluation.NotificationTemplates'],
        tenant: null,
      });
      const templates = c.sections.find((s: any) => s.key === 'templates');
      expect(c.canSee(templates)).toBeFalse();
    });

    it('shows a tenant-scoped section inside a clinic', () => {
      const c = create({
        section: 'templates',
        policies: ['CaseEvaluation.NotificationTemplates'],
        tenant: { id: 'office-a', name: 'Falkinstein' },
      });
      const templates = c.sections.find((s: any) => s.key === 'templates');
      expect(c.canSee(templates)).toBeTrue();
    });

    it('does not call the API for a tenant-scoped section opened at host scope', () => {
      /**
       * The load() guard, and the reason it exists: the call 403s. The template shows a
       * "switch into a clinic" placeholder instead, so loading must also be released or the
       * page would spin forever behind it.
       */
      const c = create({ section: 'templates', tenant: null });
      expect(c.activeBlockedAtHost()).toBeTrue();
      expect(gateway['listTemplates']).not.toHaveBeenCalled();
      expect(c.loading()).toBeFalse();
    });

    it('does call the API for the same section inside a clinic', () => {
      const c = create({ section: 'templates', tenant: { id: 'office-a' } });
      expect(c.activeBlockedAtHost()).toBeFalse();
      expect(gateway['listTemplates']).toHaveBeenCalled();
    });
  });

  describe('notification templates', () => {
    function withTemplates(rows: NtRow[]): Probe {
      const c = create({ section: undefined, tenant: { id: 'office-a' } });
      gateway['listTemplates'].and.returnValue(of(rows));
      // A NON-EMPTY type list, because the load-once guard is `if (!this.ntTypes().length)`.
      // With the empty default the list never populates, so a refetch is correct behaviour and
      // "loads once" would be asserting against a precondition that never held.
      gateway['listTemplateTypes'].and.returnValue(of([{ id: 'ty-1', name: 'Appointment' }]));
      routeData.next({ section: 'templates' });
      return c;
    }

    it('lists the templates and selects the first', () => {
      const c = withTemplates([ntRow(), ntRow({ id: 'nt-2', code: 'AppointmentApproved' })]);
      expect(c.ntRows().length).toBe(2);
      expect(c.ntSelectedId()).toBe('nt-1');
      expect(c.loading()).toBeFalse();
    });

    it('loads the type list once rather than per section visit', () => {
      const c = withTemplates([ntRow()]);
      gateway['listTemplateTypes'].calls.reset();
      c.loadTemplates();
      expect(gateway['listTemplateTypes']).not.toHaveBeenCalled();
    });

    it('leaves the list empty when the fetch fails', () => {
      const c = create({ tenant: { id: 'office-a' } });
      gateway['listTemplates'].and.returnValue(throwError(() => ({ status: 500 })));
      routeData.next({ section: 'templates' });
      expect(c.ntRows()).toEqual([]);
    });

    it('keeps the current selection across a reload', () => {
      // A save triggers a reload; re-selecting the first row would throw the editor back to
      // the top of the list every time someone saved.
      const c = withTemplates([ntRow(), ntRow({ id: 'nt-2' })]);
      c.selectTemplate(c.ntRows()[1]);
      c.loadTemplates();
      expect(c.ntSelectedId()).toBe('nt-2');
    });

    it('re-selects when the current row is gone after a reload', () => {
      const c = withTemplates([ntRow(), ntRow({ id: 'nt-2' })]);
      c.selectTemplate(c.ntRows()[1]);
      gateway['listTemplates'].and.returnValue(of([ntRow()]));
      c.loadTemplates();
      expect(c.ntSelectedId()).toBe('nt-1');
    });

    it('builds an editable draft from the selected row', () => {
      const c = withTemplates([ntRow()]);
      expect(c.ntDraft()).toEqual({
        subject: 'Your appointment request',
        bodyEmail: '<p>Hello ##Patient.FirstName##</p>',
        bodySms: 'Hello',
        active: true,
      });
    });

    it('clears the draft when nothing is selected', () => {
      const c = withTemplates([ntRow()]);
      c.selectTemplate(null);
      expect(c.ntDraft()).toBeNull();
      expect(c.ntSelectedId()).toBeNull();
    });

    it('clears stale variables before loading the new row own', () => {
      /**
       * A REMOVAL needing the variables seeded. Left behind, the chip row would offer the
       * PREVIOUS template's merge tokens, and inserting one would put a token the new
       * template cannot resolve into an email that reaches a patient.
       */
      const c = withTemplates([ntRow(), ntRow({ id: 'nt-2' })]);
      c.ntVariables.set([{ token: 'Patient.FirstName', label: 'First name' }]);
      gateway['getTemplateVariables'].and.returnValue(throwError(() => ({ status: 500 })));

      c.selectTemplate(c.ntRows()[1]);

      expect(c.ntVariables()).toEqual([]);
    });

    it('loads the variables for the selected template', () => {
      const c = withTemplates([ntRow()]);
      gateway['getTemplateVariables'].and.returnValue(
        of([{ token: 'Patient.FirstName', label: 'First name' }]),
      );
      c.selectTemplate(c.ntRows()[0]);
      expect(c.ntVariables().length).toBe(1);
      expect(gateway['getTemplateVariables']).toHaveBeenCalledWith('AppointmentRequested');
    });

    it('patches the draft without discarding the untouched fields', () => {
      const c = withTemplates([ntRow()]);
      c.patchDraft({ subject: 'Edited subject' });
      expect(c.ntDraft().subject).toBe('Edited subject');
      expect(c.ntDraft().bodySms).toBe('Hello');
    });

    it('ignores a patch when nothing is selected', () => {
      const c = withTemplates([ntRow()]);
      c.selectTemplate(null);
      expect(() => c.patchDraft({ subject: 'x' })).not.toThrow();
      expect(c.ntDraft()).toBeNull();
    });

    it('filters the list by type', () => {
      const c = withTemplates([
        ntRow({ id: 'nt-1', typeName: 'Appointment' }),
        ntRow({ id: 'nt-2', typeName: 'Invitation' }),
      ]);
      c.setNtTypeFilter('Invitation');
      expect(c.ntShown().map((r: NtRow) => r.id)).toEqual(['nt-2']);
    });

    it('shows everything when no type filter is set', () => {
      const c = withTemplates([ntRow({ id: 'nt-1' }), ntRow({ id: 'nt-2' })]);
      c.setNtTypeFilter('');
      expect(c.ntShown().length).toBe(2);
    });

    it('filters the list by search query', () => {
      const c = withTemplates([
        ntRow({ id: 'nt-1', code: 'AppointmentRequested' }),
        ntRow({ id: 'nt-2', code: 'PasswordReset' }),
      ]);
      c.setNtQuery('password');
      expect(c.ntShown().map((r: NtRow) => r.id)).toEqual(['nt-2']);
    });

    it('buckets the filtered templates into lifecycle groups', () => {
      const c = withTemplates([ntRow(), ntRow({ id: 'nt-2', code: 'AppointmentApproved' })]);
      const grouped = c.ntGroups();
      expect(Array.isArray(grouped)).toBeTrue();
      expect(grouped.reduce((n: number, g: any) => n + g.items.length, 0)).toBe(2);
    });

    it('resolves the selected row and its catalog entry', () => {
      const c = withTemplates([ntRow()]);
      expect(c.ntSelected()?.id).toBe('nt-1');
      expect(c.ntSelectedMeta()).not.toBeNull();
    });

    it('has no catalog entry when nothing is selected', () => {
      const c = withTemplates([ntRow()]);
      c.selectTemplate(null);
      expect(c.ntSelectedMeta()).toBeNull();
    });

    it('segments the subject preview using the variable labels', () => {
      const c = withTemplates([ntRow({ subject: 'Hi ##Patient.FirstName##' })]);
      c.ntVariables.set([{ token: 'Patient.FirstName', label: 'First name' }]);
      expect(Array.isArray(c.ntPreviewSubject())).toBeTrue();
      expect(c.ntPreviewSubject().length).toBeGreaterThan(0);
    });
  });

  describe('inserting a merge variable', () => {
    function editorStub() {
      return {
        getSelection: jasmine.createSpy('getSelection').and.returnValue({ index: 6, length: 0 }),
        getLength: jasmine.createSpy('getLength').and.returnValue(20),
        insertText: jasmine.createSpy('insertText'),
        setSelection: jasmine.createSpy('setSelection'),
      };
    }

    function ready(): Probe {
      const c = create({ tenant: { id: 'office-a' } });
      gateway['listTemplates'].and.returnValue(of([ntRow()]));
      routeData.next({ section: 'templates' });
      return c;
    }

    it('inserts at the caret when the editor is live', () => {
      const c = ready();
      const editor = editorStub();
      c.onEditorCreated(editor);

      c.insertVar('Patient.FirstName');

      expect(editor.insertText).toHaveBeenCalledWith(6, '##Patient.FirstName##', 'user');
      expect(editor.setSelection).toHaveBeenCalledWith(6 + '##Patient.FirstName##'.length, 0);
      expect(toaster.info).toHaveBeenCalled();
    });

    it('inserts at the end when the editor is unfocused', () => {
      // getSelection(true) returns null when the editor never had focus; falling back to the
      // document length is what stops the token landing at index 0, ahead of the greeting.
      const c = ready();
      const editor = editorStub();
      editor.getSelection.and.returnValue(null);
      c.onEditorCreated(editor);

      c.insertVar('Patient.FirstName');

      expect(editor.insertText).toHaveBeenCalledWith(20, '##Patient.FirstName##', 'user');
    });

    it('appends to the draft body when the editor has not been created', () => {
      const c = ready();
      c.insertVar('Patient.FirstName');
      expect(c.ntDraft().bodyEmail).toContain('##Patient.FirstName##');
      expect(toaster.info).toHaveBeenCalled();
    });

    it('does nothing without a token or a draft', () => {
      const c = ready();
      c.insertVar(undefined);
      expect(toaster.info).not.toHaveBeenCalled();

      c.selectTemplate(null);
      c.insertVar('Patient.FirstName');
      expect(toaster.info).not.toHaveBeenCalled();
    });
  });

  describe('template actions', () => {
    function ready(): Probe {
      const c = create({ tenant: { id: 'office-a' } });
      gateway['listTemplates'].and.returnValue(of([ntRow()]));
      routeData.next({ section: 'templates' });
      return c;
    }

    it('sends a test email for the selected template', () => {
      const c = ready();
      c.sendTest();
      expect(gateway['sendTestTemplate']).toHaveBeenCalledWith('nt-1');
      expect(toaster.success).toHaveBeenCalled();
      expect(c.isBusy()).toBeFalse();
    });

    it('refuses a second send while one is in flight', () => {
      const c = ready();
      c.isBusy.set(true);
      c.sendTest();
      expect(gateway['sendTestTemplate']).not.toHaveBeenCalled();
    });

    it('releases the busy flag when the send fails', () => {
      // A stuck spinner is indistinguishable from a hung request.
      const c = ready();
      gateway['sendTestTemplate'].and.returnValue(throwError(() => ({ status: 500 })));
      c.sendTest();
      expect(c.isBusy()).toBeFalse();
      expect(toaster.success).not.toHaveBeenCalled();
    });

    it('saves the draft with the row concurrency stamp', () => {
      /**
       * The stamp is what makes the save a concurrency-checked update. Dropping it would let
       * two admins silently overwrite each other's template edits.
       */
      const c = ready();
      c.patchDraft({ subject: 'Edited subject', active: false });

      c.saveTemplate();

      expect(gateway['updateTemplate']).toHaveBeenCalledWith('nt-1', {
        subject: 'Edited subject',
        bodyEmail: '<p>Hello ##Patient.FirstName##</p>',
        bodySms: 'Hello',
        isActive: false,
        concurrencyStamp: 'stamp-1',
      });
      expect(toaster.success).toHaveBeenCalled();
    });

    it('reloads the list after a successful save', () => {
      const c = ready();
      gateway['listTemplates'].calls.reset();
      c.saveTemplate();
      expect(gateway['listTemplates']).toHaveBeenCalled();
    });

    it('refuses to save with no draft or while busy', () => {
      const c = ready();
      c.isBusy.set(true);
      c.saveTemplate();
      expect(gateway['updateTemplate']).not.toHaveBeenCalled();

      c.isBusy.set(false);
      c.selectTemplate(null);
      c.saveTemplate();
      expect(gateway['updateTemplate']).not.toHaveBeenCalled();
    });

    it('releases the busy flag when the save fails', () => {
      const c = ready();
      gateway['updateTemplate'].and.returnValue(throwError(() => ({ status: 409 })));
      c.saveTemplate();
      expect(c.isBusy()).toBeFalse();
    });
  });

  describe('system parameters', () => {
    const params = {
      appointmentLeadTime: 3,
      appointmentMaxTimePQME: 60,
      appointmentCancelTime: 24,
      emailEnabled: true,
      concurrencyStamp: 'sp-stamp',
    };

    function ready(policies: string[] = []): Probe {
      const c = create({ tenant: { id: 'office-a' }, policies });
      gateway['getParameters'].and.returnValue(of(params));
      routeData.next({ section: 'parameters' });
      return c;
    }

    it('loads the parameters', () => {
      const c = ready();
      expect(c.params().appointmentLeadTime).toBe(3);
      expect(c.loading()).toBeFalse();
    });

    it('records whether the viewer may edit them', () => {
      expect(ready([]).canEditParams()).toBeFalse();
      TestBed.resetTestingModule();
      expect(ready(['CaseEvaluation.SystemParameters.Edit']).canEditParams()).toBeTrue();
    });

    it('leaves the parameters null when the fetch fails', () => {
      const c = create({ tenant: { id: 'office-a' } });
      gateway['getParameters'].and.returnValue(throwError(() => ({ status: 500 })));
      routeData.next({ section: 'parameters' });
      expect(c.params()).toBeNull();
    });

    it('reads a parameter by key, defaulting to zero', () => {
      const c = ready();
      expect(c.paramValue('appointmentLeadTime')).toBe(3);
      expect(c.paramValue('noSuchParameter')).toBe(0);
    });

    it('reads zero when no parameters are loaded', () => {
      const c = create({ tenant: { id: 'office-a' } });
      gateway['getParameters'].and.returnValue(of(null));
      routeData.next({ section: 'parameters' });
      expect(c.paramValue('appointmentLeadTime')).toBe(0);
    });

    it('patches one parameter without dropping the rest', () => {
      const c = ready();
      c.patchParam('appointmentLeadTime', 7);
      expect(c.params().appointmentLeadTime).toBe(7);
      expect(c.params().appointmentCancelTime).toBe(24);
      expect(c.params().concurrencyStamp).toBe('sp-stamp');
    });

    it('ignores a patch when nothing is loaded', () => {
      const c = create({ tenant: { id: 'office-a' } });
      gateway['getParameters'].and.returnValue(of(null));
      routeData.next({ section: 'parameters' });
      expect(() => c.patchParam('appointmentLeadTime', 7)).not.toThrow();
    });

    it('discards edits on revert by re-reading the server values', () => {
      /**
       * A REMOVAL needing the edit seeded: revert is only observable against a CHANGED
       * value. Against untouched parameters it would pass with the re-fetch deleted.
       */
      const c = ready();
      c.patchParam('appointmentLeadTime', 99);
      expect(c.params().appointmentLeadTime).toBe(99);

      c.revertParameters();

      expect(c.params().appointmentLeadTime).toBe(3);
      expect(toaster.info).toHaveBeenCalled();
    });

    it('saves the edited parameters and keeps the server response', () => {
      const c = ready(['CaseEvaluation.SystemParameters.Edit']);
      gateway['updateParameters'].and.returnValue(of({ ...params, appointmentLeadTime: 5 }));
      c.patchParam('appointmentLeadTime', 5);

      c.saveParameters();

      expect(gateway['updateParameters']).toHaveBeenCalled();
      expect(gateway['updateParameters'].calls.mostRecent().args[0].appointmentLeadTime).toBe(5);
      expect(c.params().appointmentLeadTime).toBe(5);
      expect(toaster.success).toHaveBeenCalled();
    });

    it('refuses to save without the edit permission', () => {
      // The rail still renders the section for a read-only viewer; the save must not fire.
      const c = ready([]);
      c.saveParameters();
      expect(gateway['updateParameters']).not.toHaveBeenCalled();
    });

    it('refuses to save while busy or with nothing loaded', () => {
      const c = ready(['CaseEvaluation.SystemParameters.Edit']);
      c.isBusy.set(true);
      c.saveParameters();
      expect(gateway['updateParameters']).not.toHaveBeenCalled();
    });

    it('releases the busy flag when the save fails', () => {
      const c = ready(['CaseEvaluation.SystemParameters.Edit']);
      gateway['updateParameters'].and.returnValue(throwError(() => ({ status: 409 })));
      c.saveParameters();
      expect(c.isBusy()).toBeFalse();
    });
  });

  describe('users and roles', () => {
    function ready(rows: RoleRow[] = [roleRow()]): Probe {
      const c = create({ policies: ['AbpIdentity.Roles'] });
      gateway['listRoles'].and.returnValue(of(rows));
      routeData.next({ section: 'roles' });
      return c;
    }

    it('loads the roles and selects the first editable one', () => {
      const c = ready([roleRow({ name: 'IT Admin' }), roleRow({ name: 'Staff Supervisor' })]);
      expect(c.roles().length).toBe(2);
      expect(c.roleSelected()).toBe('Staff Supervisor');
    });

    it('falls back to the first role when every role is locked', () => {
      // Better to show the locked role read-only than to show an empty matrix.
      const c = ready([roleRow({ name: 'IT Admin' })]);
      expect(c.roleSelected()).toBe('IT Admin');
      expect(c.roleLocked()).toBeTrue();
    });

    it('leaves the list empty when the fetch fails', () => {
      const c = create({ policies: ['AbpIdentity.Roles'] });
      gateway['listRoles'].and.returnValue(throwError(() => ({ status: 500 })));
      routeData.next({ section: 'roles' });
      expect(c.roles()).toEqual([]);
    });

    it('splits the roles by kind', () => {
      const c = ready([
        roleRow({ name: 'Staff Supervisor', kind: 'Internal' }),
        roleRow({ name: 'Patient', kind: 'External' }),
      ]);
      expect(c.rolesOfKind('Internal').map((r: RoleRow) => r.name)).toEqual(['Staff Supervisor']);
      expect(c.rolesOfKind('External').map((r: RoleRow) => r.name)).toEqual(['Patient']);
    });

    it('loads the permission matrix for the selected role', () => {
      const c = ready();
      gateway['getPermissions'].and.returnValue(
        of({
          groups: [
            {
              name: 'CaseEvaluation',
              displayName: 'Case Evaluation',
              permissions: [
                {
                  name: 'CaseEvaluation.Appointments',
                  displayName: 'Appointments',
                  isGranted: true,
                },
                {
                  name: 'CaseEvaluation.Appointments.Create',
                  displayName: 'Create',
                  parentName: 'CaseEvaluation.Appointments',
                  isGranted: false,
                },
              ],
            },
          ],
        }),
      );

      c.selectRole('Staff Supervisor');

      expect(c.permGroups().length).toBe(1);
      expect(c.isGranted('CaseEvaluation.Appointments')).toBeTrue();
      expect(c.isGranted('CaseEvaluation.Appointments.Create')).toBeFalse();
      expect(c.grantedCount()).toBe(1);
      expect(c.permTotal()).toBe(2);
    });

    it('clears the previous role matrix before loading the next', () => {
      /**
       * A REMOVAL needing the previous role's grants seeded. Without the clear, a slow second
       * fetch would leave one role's permissions on screen under another role's name -- and
       * saving then would write them.
       */
      const c = ready();
      c.granted.set(new Set(['CaseEvaluation.Appointments']));
      c.permGroups.set([{ name: 'X', permissions: [] }]);
      gateway['getPermissions'].and.returnValue(throwError(() => ({ status: 500 })));

      c.selectRole('Staff Supervisor');

      expect(c.permGroups()).toEqual([]);
      expect(c.granted().size).toBe(0);
    });

    it('clears the matrix when the role is deselected, without fetching', () => {
      const c = ready();
      gateway['getPermissions'].calls.reset();
      c.selectRole(null);
      expect(c.roleSelected()).toBeNull();
      expect(gateway['getPermissions']).not.toHaveBeenCalled();
    });

    it('treats an undefined permission name as not granted', () => {
      expect(ready().isGranted(undefined)).toBeFalse();
    });

    it('toggles a permission on and off', () => {
      const c = ready();
      c.togglePermission('CaseEvaluation.Appointments');
      expect(c.isGranted('CaseEvaluation.Appointments')).toBeTrue();
      c.togglePermission('CaseEvaluation.Appointments');
      expect(c.isGranted('CaseEvaluation.Appointments')).toBeFalse();
    });

    it('refuses to toggle a permission on a locked role', () => {
      // IT Admin is the recovery role: editing its own permissions can lock everyone out.
      const c = ready([roleRow({ name: 'IT Admin' })]);
      c.togglePermission('CaseEvaluation.Appointments');
      expect(c.isGranted('CaseEvaluation.Appointments')).toBeFalse();
    });

    it('ignores a toggle with no permission name', () => {
      const c = ready();
      expect(() => c.togglePermission(undefined)).not.toThrow();
    });

    it('saves every permission with its current grant state', () => {
      /**
       * The payload must carry the UNGRANTED ones too -- ABP treats the list as the complete
       * desired state, so omitting a revoked permission would silently leave it granted.
       */
      const c = ready();
      c.permGroups.set([
        {
          name: 'CaseEvaluation',
          permissions: [{ name: 'A' }, { name: 'B' }, { name: undefined }],
        },
      ]);
      c.granted.set(new Set(['A']));

      c.savePermissions();

      expect(gateway['updatePermissions']).toHaveBeenCalledWith('Staff Supervisor', [
        { name: 'A', isGranted: true },
        { name: 'B', isGranted: false },
      ]);
      expect(toaster.success).toHaveBeenCalled();
    });

    it('refuses to save a locked role', () => {
      const c = ready([roleRow({ name: 'IT Admin' })]);
      c.savePermissions();
      expect(gateway['updatePermissions']).not.toHaveBeenCalled();
    });

    it('refuses to save with no role selected or while busy', () => {
      const c = ready();
      c.selectRole(null);
      c.savePermissions();
      expect(gateway['updatePermissions']).not.toHaveBeenCalled();
    });

    it('releases the busy flag when the save fails', () => {
      const c = ready();
      gateway['updatePermissions'].and.returnValue(throwError(() => ({ status: 500 })));
      c.savePermissions();
      expect(c.isBusy()).toBeFalse();
    });

    it('nests child permissions under their parent in the matrix', () => {
      const c = ready();
      c.permGroups.set([
        {
          name: 'CaseEvaluation',
          displayName: 'Case Evaluation',
          permissions: [
            { name: 'CaseEvaluation.Appointments', displayName: 'Appointments' },
            {
              name: 'CaseEvaluation.Appointments.Create',
              displayName: 'Create',
              parentName: 'CaseEvaluation.Appointments',
            },
          ],
        },
      ]);

      const matrix = c.permMatrix();

      expect(matrix.length).toBe(1);
      expect(matrix[0].parents.length).toBe(1);
      expect(matrix[0].parents[0].children.length).toBe(1);
    });

    it('drops a group whose permissions all fail the search', () => {
      const c = ready();
      c.permGroups.set([
        { name: 'G', displayName: 'G', permissions: [{ name: 'A', displayName: 'Appointments' }] },
      ]);
      c.permSearch.set('nothing-matches-this');
      expect(c.permMatrix()).toEqual([]);
    });

    it('keeps a matching group and is case-insensitive', () => {
      const c = ready();
      c.permGroups.set([
        { name: 'G', displayName: 'G', permissions: [{ name: 'A', displayName: 'Appointments' }] },
      ]);
      c.permSearch.set('  APPOINT  ');
      expect(c.permMatrix().length).toBe(1);
    });
  });

  describe('audit logs', () => {
    function log(over: Record<string, unknown> = {}) {
      return {
        id: 'a1',
        executionTime: '2026-09-10T09:00:00',
        userName: 'ada',
        url: '/api/app/appointments',
        httpMethod: 'GET',
        httpStatusCode: 200,
        executionDuration: 12,
        clientIpAddress: '10.0.0.1',
        browserInfo: 'Chrome',
        tenantName: 'Falkinstein',
        ...over,
      };
    }

    function ready(rows: Record<string, unknown>[] = [log()]): Probe {
      const c = create({ policies: ['AuditLogging.AuditLogs'] });
      gateway['listAuditLogs'].and.returnValue(of(rows));
      routeData.next({ section: 'audit' });
      return c;
    }

    it('loads the audit rows', () => {
      const c = ready();
      expect(c.auditRows().length).toBe(1);
      expect(c.loading()).toBeFalse();
    });

    it('leaves the rows empty when the fetch fails', () => {
      const c = create({ policies: ['AuditLogging.AuditLogs'] });
      gateway['listAuditLogs'].and.returnValue(throwError(() => ({ status: 500 })));
      routeData.next({ section: 'audit' });
      expect(c.auditRows()).toEqual([]);
    });

    it('re-queries the server when the method filter changes', () => {
      // The method filter is server-side; the text query is client-side. Mixing them up would
      // filter one page instead of the whole log.
      const c = ready();
      c.applyAuditMethod('POST');
      expect(gateway['listAuditLogs']).toHaveBeenCalledWith('POST');
    });

    it('sends undefined rather than an empty method', () => {
      const c = ready();
      c.applyAuditMethod('');
      expect(gateway['listAuditLogs']).toHaveBeenCalledWith(undefined);
    });

    it('filters the loaded page by user or url', () => {
      const c = ready([
        log({ id: 'a1', userName: 'ada', url: '/api/app/appointments' }),
        log({ id: 'a2', userName: 'grace', url: '/api/app/patients' }),
      ]);
      c.auditQuery.set('grace');
      expect(c.auditShown().map((l: any) => l.id)).toEqual(['a2']);
      c.auditQuery.set('patients');
      expect(c.auditShown().map((l: any) => l.id)).toEqual(['a2']);
    });

    it('returns every row for a blank query', () => {
      const c = ready([log({ id: 'a1' }), log({ id: 'a2' })]);
      c.auditQuery.set('   ');
      expect(c.auditShown().length).toBe(2);
    });

    it('leaves the rows in server order until a sort is chosen', () => {
      const c = ready([log({ id: 'a1', userName: 'zoe' }), log({ id: 'a2', userName: 'ada' })]);
      expect(c.auditShown().map((l: any) => l.id)).toEqual(['a1', 'a2']);
    });

    it('sorts by user name', () => {
      const c = ready([log({ id: 'a1', userName: 'zoe' }), log({ id: 'a2', userName: 'ada' })]);
      c.sortAudit({ key: 'userName', dir: 'asc' });
      expect(c.auditShown().map((l: any) => l.id)).toEqual(['a2', 'a1']);
    });

    it('sorts time numerically rather than as text', () => {
      /**
       * A string sort would order "2026-09-9T..." after "2026-09-10T...". The dates here are
       * chosen so a lexicographic sort gives the opposite answer to the correct one.
       */
      const c = ready([
        log({ id: 'a1', executionTime: '2026-09-10T09:00:00' }),
        log({ id: 'a2', executionTime: '2026-09-09T09:00:00' }),
      ]);
      c.sortAudit({ key: 'executionTime', dir: 'asc' });
      expect(c.auditShown().map((l: any) => l.id)).toEqual(['a2', 'a1']);
    });

    it('sorts status and duration numerically', () => {
      const c = ready([
        log({ id: 'a1', httpStatusCode: 500, executionDuration: 100 }),
        log({ id: 'a2', httpStatusCode: 200, executionDuration: 9 }),
      ]);
      c.sortAudit({ key: 'httpStatusCode', dir: 'asc' });
      expect(c.auditShown().map((l: any) => l.id)).toEqual(['a2', 'a1']);
      c.sortAudit({ key: 'executionDuration', dir: 'asc' });
      expect(c.auditShown().map((l: any) => l.id)).toEqual(['a2', 'a1']);
    });

    it('sorts by url and method', () => {
      const c = ready([
        log({ id: 'a1', url: '/z', httpMethod: 'POST' }),
        log({ id: 'a2', url: '/a', httpMethod: 'GET' }),
      ]);
      c.sortAudit({ key: 'url', dir: 'asc' });
      expect(c.auditShown().map((l: any) => l.id)).toEqual(['a2', 'a1']);
      c.sortAudit({ key: 'httpMethod', dir: 'asc' });
      expect(c.auditShown().map((l: any) => l.id)).toEqual(['a2', 'a1']);
    });

    it('leaves the order alone for an unknown sort key', () => {
      const c = ready([log({ id: 'a1', userName: 'zoe' }), log({ id: 'a2', userName: 'ada' })]);
      c.sortAudit({ key: 'notAColumn', dir: 'asc' });
      expect(c.auditShown().map((l: any) => l.id)).toEqual(['a1', 'a2']);
    });

    it('does not mutate the loaded rows when sorting', () => {
      // `[...filtered].sort()` -- sorting in place would reorder the source array and make the
      // "server order" case above depend on which test ran first.
      const c = ready([log({ id: 'a1', userName: 'zoe' }), log({ id: 'a2', userName: 'ada' })]);
      c.sortAudit({ key: 'userName', dir: 'asc' });
      c.auditShown();
      expect(c.auditRows().map((l: any) => l.id)).toEqual(['a1', 'a2']);
    });

    it('expands and collapses a row', () => {
      const c = ready();
      expect(c.isAuditOpen('a1')).toBeFalse();
      c.toggleAuditRow('a1');
      expect(c.isAuditOpen('a1')).toBeTrue();
      c.toggleAuditRow('a1');
      expect(c.isAuditOpen('a1')).toBeFalse();
    });

    it('ignores a toggle with no row id', () => {
      const c = ready();
      expect(() => c.toggleAuditRow(undefined)).not.toThrow();
      expect(c.isAuditOpen(undefined)).toBeFalse();
    });

    it('exports the FILTERED rows, not every loaded row', () => {
      /**
       * `auditShown()`, not `auditRows()`. Exporting the unfiltered page would hand someone a
       * CSV that does not match what they were looking at when they clicked Export.
       */
      const c = ready([log({ id: 'a1', userName: 'ada' }), log({ id: 'a2', userName: 'grace' })]);
      c.auditQuery.set('grace');
      const click = spyOn(HTMLAnchorElement.prototype, 'click');
      const created = spyOn(URL, 'createObjectURL').and.returnValue('blob:fake');
      spyOn(URL, 'revokeObjectURL');

      c.exportAudit();

      expect(click).toHaveBeenCalled();
      expect(created).toHaveBeenCalled();
      expect(toaster.success).toHaveBeenCalled();
    });

    it('releases the object URL after the download', () => {
      // A blob URL held open pins the whole CSV in memory for the life of the document.
      const c = ready();
      spyOn(HTMLAnchorElement.prototype, 'click');
      spyOn(URL, 'createObjectURL').and.returnValue('blob:fake');
      const revoked = spyOn(URL, 'revokeObjectURL');

      c.exportAudit();

      expect(revoked).toHaveBeenCalledWith('blob:fake');
    });

    it('substitutes a placeholder for an anonymous user', () => {
      const c = ready([log({ userName: undefined })]);
      spyOn(HTMLAnchorElement.prototype, 'click');
      spyOn(URL, 'createObjectURL').and.returnValue('blob:fake');
      spyOn(URL, 'revokeObjectURL');

      expect(() => c.exportAudit()).not.toThrow();
    });
  });

  describe('template type lookup failure', () => {
    it('falls back to no type filter and still loads the templates', () => {
      // The type list only feeds the filter dropdown; losing it must not cost the page its rows.
      const c = create({ tenant: { id: 'office-a' } });
      gateway['listTemplateTypes'].and.returnValue(throwError(() => new Error('boom')));

      routeData.next({});

      expect(c.ntTypes()).toEqual([]);
      expect(gateway['listTemplates']).toHaveBeenCalled();
    });
  });
});
