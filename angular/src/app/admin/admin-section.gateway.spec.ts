import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { PermissionsService } from '@abp/ng.permission-management/proxy';
import { IdentityRoleService } from '@volo/abp.ng.identity/proxy';
import { AuditLogsService } from '@volo/abp.ng.audit-logging/proxy';

import { AdminSectionGateway } from './admin-section.gateway';
import { NotificationTemplatesService } from '../proxy/notification-templates/notification-templates.service';
import { SystemParametersService } from '../proxy/system-parameters-controllers/system-parameters.service';

/**
 * The Admin hub's section gateway: one place that routes each section of the hub to the right
 * proxy service, so the hub component carries no per-section proxy wiring.
 *
 * <p>It had NO spec and sat at 3 of 25 lines covered.</p>
 *
 * <p>No component is constructed here. The gateway is an `@Injectable` with five proxy services
 * behind it, so the whole file is reachable through `TestBed.inject` with a plain spy object per
 * service -- no harness, no change detection, no DOM. This is the third gateway in this shape;
 * the other two are `people-section` and `config-section`.</p>
 *
 * <p>What is worth pinning in a file of thin delegating methods is the part that is NOT
 * delegation: the query each method builds, the constants it sends, and the row shapes it
 * normalises. A method that forwards to the wrong proxy, sends the wrong provider key, or reads
 * the wrong nesting level of a DTO still compiles and still returns an observable.</p>
 *
 * <p>All names and identifiers below are synthetic.</p>
 */
describe('AdminSectionGateway', () => {
  let templates: Record<string, jasmine.Spy>;
  let parameters: Record<string, jasmine.Spy>;
  let permissions: Record<string, jasmine.Spy>;
  let roles: Record<string, jasmine.Spy>;
  let audit: Record<string, jasmine.Spy>;

  /**
   * The provider key ABP uses for role-scoped permissions. It is a bare 'R' in the source; if
   * it ever becomes 'U' (user scope) the matrix silently reads and writes the wrong subject.
   */
  const PROVIDER_ROLE = 'R';

  function gateway(): AdminSectionGateway {
    templates = {
      getList: jasmine.createSpy('getList').and.returnValue(of({ items: [] })),
      getTypeLookup: jasmine.createSpy('getTypeLookup').and.returnValue(of({ items: [] })),
      getVariables: jasmine.createSpy('getVariables').and.returnValue(of({ items: [] })),
      sendTest: jasmine.createSpy('sendTest').and.returnValue(of(undefined)),
      update: jasmine.createSpy('update').and.returnValue(of({})),
    };
    parameters = {
      get: jasmine.createSpy('get').and.returnValue(of({})),
      update: jasmine.createSpy('update').and.returnValue(of({})),
    };
    permissions = {
      get: jasmine.createSpy('get').and.returnValue(of({ groups: [] })),
      update: jasmine.createSpy('update').and.returnValue(of(undefined)),
    };
    roles = {
      getAllList: jasmine.createSpy('getAllList').and.returnValue(of({ items: [] })),
    };
    audit = {
      getList: jasmine.createSpy('getList').and.returnValue(of({ items: [] })),
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: NotificationTemplatesService, useValue: templates },
        { provide: SystemParametersService, useValue: parameters },
        { provide: PermissionsService, useValue: permissions },
        { provide: IdentityRoleService, useValue: roles },
        { provide: AuditLogsService, useValue: audit },
      ],
    });

    return TestBed.inject(AdminSectionGateway);
  }

  /** Collect what an observable emits, synchronously, since every stub is `of(...)`. */
  function emitted<T>(source: { subscribe: (fn: (value: T) => void) => unknown }): T {
    let captured: T | undefined;
    source.subscribe((value) => (captured = value));
    return captured as T;
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('notification templates', () => {
    it('reads the row from the NESTED navigation dto, not the top level', () => {
      /**
       * The list endpoint returns `{ notificationTemplate, notificationTemplateType }` pairs,
       * and the type NAME lives on the sibling rather than on the template. Reading the wrong
       * level yields a row of empty strings that renders as a blank table with no error.
       */
      const g = gateway();
      templates['getList'].and.returnValue(
        of({
          items: [
            {
              notificationTemplate: {
                id: 'nt-1',
                templateCode: 'AppointmentApproved',
                isActive: true,
                isCustomized: true,
                subject: 'Your appointment',
                bodyEmail: '<p>body</p>',
                bodySms: 'body',
                concurrencyStamp: 'stamp-1',
              },
              notificationTemplateType: { name: 'Email' },
            },
          ],
        }),
      );

      const [row] = emitted(g.listTemplates(''));

      expect(row.id).toBe('nt-1');
      expect(row.code).toBe('AppointmentApproved');
      expect(row.typeName).withContext('name comes from the sibling dto').toBe('Email');
      expect(row.subject).toBe('Your appointment');
      expect(row.concurrencyStamp).toBe('stamp-1');
    });

    it('substitutes empty strings and false for a row with nothing on it', () => {
      const g = gateway();
      templates['getList'].and.returnValue(of({ items: [{}] }));

      const [row] = emitted(g.listTemplates(''));

      expect(row.id).toBe('');
      expect(row.typeName).toBe('');
      expect(row.active).toBeFalse();
      expect(row.isCustomized).toBeFalse();
      expect(row.concurrencyStamp).toBeUndefined();
    });

    it('sends UNDEFINED rather than an empty filter, so the server does not match on blank', () => {
      // An empty string is a filter value; undefined omits the parameter.
      const g = gateway();
      g.listTemplates('   ');

      const [query] = templates['getList'].calls.mostRecent().args;
      expect(query.filterText).toBeUndefined();
      expect(query.templateTypeId).toBeUndefined();
    });

    it('trims a real filter and forwards the type id', () => {
      const g = gateway();
      g.listTemplates('  approved  ', 'type-1');

      const [query] = templates['getList'].calls.mostRecent().args;
      expect(query.filterText).toBe('approved');
      expect(query.templateTypeId).toBe('type-1');
    });

    it('maps the type lookup to id and name', () => {
      const g = gateway();
      templates['getTypeLookup'].and.returnValue(of({ items: [{ id: 't-1', name: 'Email' }, {}] }));

      expect(emitted(g.listTemplateTypes())).toEqual([
        { id: 't-1', name: 'Email' },
        { id: '', name: '' },
      ]);
    });

    it('unwraps the variable catalog and treats an absent list as empty', () => {
      /**
       * The catalog row is `{ token, label }` -- NOT `{ name }`, which is what a fixture
       * written from memory reaches for. A plausible-but-wrong key here would have made the
       * variable list render blank while every assertion still passed.
       */
      const g = gateway();
      templates['getVariables'].and.returnValue(
        of({ items: [{ token: '{{PatientName}}', label: 'Patient name' }] }),
      );

      const variables = emitted(g.getTemplateVariables('AppointmentApproved'));
      expect(variables.length).toBe(1);
      expect(variables[0].token).toBe('{{PatientName}}');
      expect(variables[0].label).toBe('Patient name');

      templates['getVariables'].and.returnValue(of({}));
      expect(emitted(g.getTemplateVariables('x'))).toEqual([]);
    });

    it('passes the send-test and update calls straight through', () => {
      const g = gateway();
      g.sendTestTemplate('nt-1');
      expect(templates['sendTest']).toHaveBeenCalledWith('nt-1');

      const input = { subject: 'x', bodyEmail: 'y' } as never;
      g.updateTemplate('nt-1', input);
      expect(templates['update']).toHaveBeenCalledWith('nt-1', input);
    });
  });

  describe('system parameters', () => {
    it('reads and writes through the parameters proxy', () => {
      const g = gateway();
      g.getParameters();
      expect(parameters['get']).toHaveBeenCalled();

      const input = { leadTimeDays: 3 } as never;
      g.updateParameters(input);
      expect(parameters['update']).toHaveBeenCalledWith(input);
    });
  });

  describe('the permission matrix', () => {
    it('classifies each role as internal or external', () => {
      const g = gateway();
      roles['getAllList'].and.returnValue(
        of({
          items: [
            { name: 'IT Admin', isStatic: true },
            { name: 'Applicant Attorney', isStatic: false },
          ],
        }),
      );

      expect(emitted(g.listRoles())).toEqual([
        { name: 'IT Admin', kind: 'Internal', isStatic: true },
        { name: 'Applicant Attorney', kind: 'External', isStatic: false },
      ]);
    });

    it('names an unnamed role as an empty string rather than undefined', () => {
      const g = gateway();
      roles['getAllList'].and.returnValue(of({ items: [{ isStatic: false }] }));
      expect(emitted(g.listRoles())[0].name).toBe('');
    });

    it('READS permissions against the role provider, not some other subject', () => {
      // 'R' is role scope. Read against 'U' and the matrix shows one user's grants while
      // claiming to show a role's.
      const g = gateway();
      g.getPermissions('Staff Supervisor');

      expect(permissions['get']).toHaveBeenCalledWith(PROVIDER_ROLE, 'Staff Supervisor');
    });

    it('WRITES permissions against the same provider, wrapped in the expected envelope', () => {
      const g = gateway();
      const grants = [{ name: 'CaseEvaluation.Appointments.Create', isGranted: true }];

      g.updatePermissions('Staff Supervisor', grants);

      expect(permissions['update']).toHaveBeenCalledWith(PROVIDER_ROLE, 'Staff Supervisor', {
        permissions: grants,
      });
    });
  });

  describe('audit logs', () => {
    it('asks for the NEWEST entries first', () => {
      // Audit is read to find out what just happened; ascending order buries that on the
      // last page, and nothing in the UI would reveal the sort had flipped.
      const g = gateway();
      g.listAuditLogs();

      expect(audit['getList'].calls.mostRecent().args[0].sorting).toBe('executionTime desc');
    });

    it('caps the page at 100, overriding the shared page size', () => {
      /**
       * The shared PAGE constant is 200 and is spread FIRST, so the 100 that follows wins.
       * Swap the order and the cap silently doubles -- a plausible-looking edit that only a
       * test on the actual argument catches.
       */
      const g = gateway();
      g.listAuditLogs();

      const [query] = audit['getList'].calls.mostRecent().args;
      expect(query.maxResultCount).toBe(100);
      expect(query.skipCount).toBe(0);
    });

    it('omits the method filter when none is chosen', () => {
      const g = gateway();
      g.listAuditLogs('');
      expect(audit['getList'].calls.mostRecent().args[0].httpMethod).toBeUndefined();
    });

    it('narrows to one http method server-side when one is chosen', () => {
      const g = gateway();
      g.listAuditLogs('DELETE');
      expect(audit['getList'].calls.mostRecent().args[0].httpMethod).toBe('DELETE');
    });

    it('unwraps the page and treats an absent list as empty', () => {
      const g = gateway();
      audit['getList'].and.returnValue(of({ items: [{ id: 'a-1', httpMethod: 'DELETE' }] }));

      const rows = emitted(g.listAuditLogs());
      expect(rows.length).toBe(1);
      expect(rows[0].httpMethod).toBe('DELETE');

      audit['getList'].and.returnValue(of({}));
      expect(emitted(g.listAuditLogs())).toEqual([]);
    });
  });

  describe('section routing', () => {
    it('does not touch any other section while serving one', () => {
      // The whole point of the gateway is that a section reaches exactly one proxy.
      const g = gateway();

      g.getParameters();

      expect(templates['getList']).not.toHaveBeenCalled();
      expect(permissions['get']).not.toHaveBeenCalled();
      expect(roles['getAllList']).not.toHaveBeenCalled();
      expect(audit['getList']).not.toHaveBeenCalled();
    });
  });
});
