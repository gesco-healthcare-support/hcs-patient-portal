import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { PermissionsService } from '@abp/ng.permission-management/proxy';
import type {
  GetPermissionListResultDto,
  UpdatePermissionDto,
} from '@abp/ng.permission-management/proxy';
import { IdentityRoleService } from '@volo/abp.ng.identity/proxy';
import { AuditLogsService } from '@volo/abp.ng.audit-logging/proxy';
import type { AuditLogDto } from '@volo/abp.ng.audit-logging/proxy';
import { NotificationTemplatesService } from '../proxy/notification-templates/notification-templates.service';
import type {
  NotificationTemplateDto,
  NotificationTemplateUpdateDto,
  NotificationTemplateVariableDto,
  NotificationTemplateWithNavigationPropertiesDto,
} from '../proxy/notification-templates/models';
import { SystemParametersService } from '../proxy/system-parameters-controllers/system-parameters.service';
import type {
  SystemParameterDto,
  SystemParameterUpdateDto,
} from '../proxy/system-parameters/models';
import { roleKind } from './admin-hub.util';

/** ABP RolePermissionValueProvider name -- permissions are read/written per role. */
const PROVIDER_ROLE = 'R';
const PAGE = { maxResultCount: 200, skipCount: 0 };

/** A normalized notification-template row for the list + editor. */
export interface NtRow {
  id: string;
  code: string;
  typeName: string;
  active: boolean;
  isCustomized: boolean;
  subject: string;
  bodyEmail: string;
  bodySms: string;
  concurrencyStamp?: string;
}

/** A normalized role row for the permission-matrix rail. */
export interface RoleRow {
  name: string;
  kind: 'Internal' | 'External';
  isStatic: boolean;
}

function toNtRow(item: NotificationTemplateWithNavigationPropertiesDto): NtRow {
  const t = item.notificationTemplate;
  return {
    id: t?.id ?? '',
    code: t?.templateCode ?? '',
    typeName: item.notificationTemplateType?.name ?? '',
    active: t?.isActive ?? false,
    isCustomized: t?.isCustomized ?? false,
    subject: t?.subject ?? '',
    bodyEmail: t?.bodyEmail ?? '',
    bodySms: t?.bodySms ?? '',
    concurrencyStamp: t?.concurrencyStamp,
  };
}

/**
 * Routes each Admin-hub section to the right proxy. Notification Templates +
 * System Parameters ride the custom CaseEvaluation services (the latter plus
 * the B-B1/B-B2 send-test + variable-catalog); the Permission Matrix rides the
 * stock ABP PermissionsService + IdentityRoleService; Audit Logs rides the Volo
 * AuditLogsService. Keeps the hub component free of per-section proxy wiring.
 */
@Injectable({ providedIn: 'root' })
export class AdminSectionGateway {
  private readonly templates = inject(NotificationTemplatesService);
  private readonly parameters = inject(SystemParametersService);
  private readonly permissions = inject(PermissionsService);
  private readonly roles = inject(IdentityRoleService);
  private readonly audit = inject(AuditLogsService);

  // ---- Notification Templates ----
  listTemplates(filter: string, typeId?: string): Observable<NtRow[]> {
    return this.templates
      .getList({
        filterText: filter.trim() || undefined,
        templateTypeId: typeId || undefined,
        ...PAGE,
      })
      .pipe(map((r) => (r.items ?? []).map(toNtRow)));
  }
  listTemplateTypes(): Observable<{ id: string; name: string }[]> {
    return this.templates
      .getTypeLookup()
      .pipe(map((r) => (r.items ?? []).map((t) => ({ id: t.id ?? '', name: t.name ?? '' }))));
  }
  getTemplateVariables(code: string): Observable<NotificationTemplateVariableDto[]> {
    return this.templates.getVariables(code).pipe(map((r) => r.items ?? []));
  }
  sendTestTemplate(id: string): Observable<void> {
    return this.templates.sendTest(id);
  }
  updateTemplate(
    id: string,
    input: NotificationTemplateUpdateDto,
  ): Observable<NotificationTemplateDto> {
    return this.templates.update(id, input);
  }

  // ---- System Parameters ----
  getParameters(): Observable<SystemParameterDto> {
    return this.parameters.get();
  }
  updateParameters(input: SystemParameterUpdateDto): Observable<SystemParameterDto> {
    return this.parameters.update(input);
  }

  // ---- Users & Roles (permission matrix) ----
  listRoles(): Observable<RoleRow[]> {
    return this.roles.getAllList().pipe(
      map((r) =>
        (r.items ?? []).map((role) => ({
          name: role.name ?? '',
          kind: roleKind(role.name),
          isStatic: role.isStatic,
        })),
      ),
    );
  }
  getPermissions(roleName: string): Observable<GetPermissionListResultDto> {
    return this.permissions.get(PROVIDER_ROLE, roleName);
  }
  updatePermissions(roleName: string, permissions: UpdatePermissionDto[]): Observable<void> {
    return this.permissions.update(PROVIDER_ROLE, roleName, { permissions });
  }

  // ---- Audit Logs ----
  /**
   * Most-recent page of audit entries, newest first, optionally narrowed to one
   * HTTP method server-side. Free-text (user / URL) search is applied
   * client-side in the section so one box can match either column.
   */
  listAuditLogs(httpMethod?: string): Observable<AuditLogDto[]> {
    return this.audit
      .getList({
        sorting: 'executionTime desc',
        httpMethod: httpMethod || undefined,
        ...PAGE,
        maxResultCount: 100,
      })
      .pipe(map((r) => r.items ?? []));
  }
}
