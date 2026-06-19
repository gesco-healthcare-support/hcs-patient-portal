import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ConfigStateService, PermissionService } from '@abp/ng.core';
import { isHostScope } from '../shared/auth/internal-user-roles';
import { ToasterService } from '@abp/ng.theme.shared';
import { finalize } from 'rxjs/operators';
import type {
  GetPermissionListResultDto,
  PermissionGroupDto,
  UpdatePermissionDto,
} from '@abp/ng.permission-management/proxy';
import type { AuditLogDto } from '@volo/abp.ng.audit-logging/proxy';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { QuillEditorComponent } from 'ngx-quill';
import type Quill from 'quill';
import type { NotificationTemplateVariableDto } from '../proxy/notification-templates/models';
import type { SystemParameterDto } from '../proxy/system-parameters/models';
import {
  ADMIN_SECTIONS,
  AdminSection,
  AdminSectionKey,
  auditMethodClass,
  auditResultLabel,
  auditStatusClass,
  buildAuditCsv,
  insertVariable,
  isLockedRole,
  previewSegments,
  SP_GROUPS,
} from './admin-hub.util';
import { AdminSectionGateway, NtRow, RoleRow } from './admin-section.gateway';

/** The editable working copy of the selected notification template. */
interface NtDraft {
  subject: string;
  bodyEmail: string;
  bodySms: string;
  active: boolean;
}

/**
 * Admin hub (Prompt 16, Part B). One standalone component mounted at the four
 * `/admin/*` section routes; reads `data.section` to pick the surface, with the
 * left rail as real routerLinks gated by granted policy. Sections: Notification
 * Templates (split list + editor, variable chips, live preview, send-test),
 * System Parameters (grouped editor), Users & Roles (permission matrix), Audit
 * Logs (filterable + expandable + CSV). Mirrors the Users hub pattern.
 */
@Component({
  selector: 'app-internal-admin-hub',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, RouterLink, IconComponent, QuillEditorComponent],
  templateUrl: './internal-admin-hub.component.html',
})
export class InternalAdminHubComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly gateway = inject(AdminSectionGateway);
  private readonly permission = inject(PermissionService);
  private readonly config = inject(ConfigStateService);
  private readonly toaster = inject(ToasterService);

  protected readonly sections = ADMIN_SECTIONS;
  protected readonly spGroups = SP_GROUPS;
  protected readonly auditMethodClass = auditMethodClass;
  protected readonly auditStatusClass = auditStatusClass;
  protected readonly auditResultLabel = auditResultLabel;

  protected readonly section = signal<AdminSectionKey>('templates');
  protected readonly meta = computed<AdminSection>(
    () => this.sections.find((s) => s.key === this.section()) ?? this.sections[0],
  );
  protected readonly loading = signal(true);
  protected readonly isBusy = signal(false);

  // ---- Notification Templates ----
  protected readonly ntRows = signal<NtRow[]>([]);
  protected readonly ntTypes = signal<{ id: string; name: string }[]>([]);
  protected readonly ntQuery = signal('');
  protected readonly ntTypeFilter = signal('');
  protected readonly ntSelectedId = signal<string | null>(null);
  protected readonly ntDraft = signal<NtDraft | null>(null);
  protected readonly ntVariables = signal<NotificationTemplateVariableDto[]>([]);
  /** Live Quill instance for the email body (cursor-aware variable inserts). */
  private editor: Quill | null = null;
  /** Toolbar: common email formatting. ##Var## tokens insert as literal text. */
  protected readonly quillModules = {
    toolbar: [
      [{ header: [1, 2, 3, false] }],
      ['bold', 'italic', 'underline'],
      [{ list: 'ordered' }, { list: 'bullet' }],
      ['link'],
      ['clean'],
    ],
  };

  protected readonly ntShown = computed(() => {
    const q = this.ntQuery().trim().toLowerCase();
    const type = this.ntTypeFilter();
    return this.ntRows().filter(
      (r) => (!type || r.typeName === type) && (!q || r.code.toLowerCase().includes(q)),
    );
  });
  protected readonly ntSelected = computed(
    () => this.ntRows().find((r) => r.id === this.ntSelectedId()) ?? null,
  );
  private readonly ntLabelByToken = computed<Record<string, string>>(() => {
    const map: Record<string, string> = {};
    for (const v of this.ntVariables()) {
      map[v.token ?? ''] = v.label ?? '';
    }
    return map;
  });
  protected readonly ntPreviewSubject = computed(() =>
    previewSegments(this.ntDraft()?.subject, this.ntLabelByToken()),
  );
  // ---- #6: client-side pager over the filtered list (~62 fixed codes) ----
  protected readonly ntPageSize = 10;
  protected readonly ntPage = signal(0);
  protected readonly ntTotalPages = computed(() =>
    Math.max(1, Math.ceil(this.ntShown().length / this.ntPageSize)),
  );
  protected readonly ntPaged = computed(() => {
    const start = this.ntPage() * this.ntPageSize;
    return this.ntShown().slice(start, start + this.ntPageSize);
  });

  // ---- System Parameters ----
  protected readonly params = signal<SystemParameterDto | null>(null);
  protected readonly canEditParams = signal(false);

  // ---- Users & Roles ----
  protected readonly roleKinds: ('Internal' | 'External')[] = ['Internal', 'External'];
  protected readonly roles = signal<RoleRow[]>([]);
  protected readonly roleSelected = signal<string | null>(null);
  protected readonly permGroups = signal<PermissionGroupDto[]>([]);
  protected readonly granted = signal<Set<string>>(new Set<string>());
  protected readonly roleLocked = computed(() => isLockedRole(this.roleSelected()));
  protected readonly grantedCount = computed(() => this.granted().size);
  protected readonly permTotal = computed(() =>
    this.permGroups().reduce((sum, g) => sum + (g.permissions?.length ?? 0), 0),
  );

  // ---- Audit Logs ----
  protected readonly auditRows = signal<AuditLogDto[]>([]);
  protected readonly auditQuery = signal('');
  protected readonly auditMethod = signal('');
  protected readonly auditOpen = signal<Set<string>>(new Set<string>());
  protected readonly auditShown = computed(() => {
    const q = this.auditQuery().trim().toLowerCase();
    if (!q) {
      return this.auditRows();
    }
    return this.auditRows().filter(
      (l) =>
        (l.userName ?? '').toLowerCase().includes(q) || (l.url ?? '').toLowerCase().includes(q),
    );
  });

  constructor() {
    this.route.data.pipe(takeUntilDestroyed()).subscribe((data) => {
      this.section.set((data['section'] as AdminSectionKey) ?? 'templates');
      this.load();
    });
  }

  /**
   * A section shows when its policy is granted AND (it is not tenant-scoped, or
   * we are inside a clinic). Tenant-scoped sections 403 at host scope, so IT
   * Admin reaches them by switching into a clinic first.
   */
  protected canSee(section: AdminSection): boolean {
    if (!this.permission.getGrantedPolicy(section.policy)) {
      return false;
    }
    return !section.tenantScoped || !isHostScope(this.config);
  }

  /** The current section is a tenant-scoped one being viewed at host scope. */
  protected readonly activeBlockedAtHost = computed(
    () => this.meta().tenantScoped && isHostScope(this.config),
  );

  private load(): void {
    // Tenant-scoped section opened at host scope: do not call the API (it 403s);
    // the template shows a "switch into a clinic" placeholder instead.
    if (this.activeBlockedAtHost()) {
      this.loading.set(false);
      return;
    }
    const key = this.section();
    this.loading.set(true);
    if (key === 'templates') {
      this.loadTemplates();
    } else if (key === 'parameters') {
      this.loadParameters();
    } else if (key === 'roles') {
      this.loadRoles();
    } else {
      this.loadAudit();
    }
  }

  // ===================== Notification Templates =====================
  private loadTemplates(): void {
    if (!this.ntTypes().length) {
      this.gateway.listTemplateTypes().subscribe({
        next: (t) => this.ntTypes.set(t),
        error: () => this.ntTypes.set([]),
      });
    }
    this.gateway
      .listTemplates('')
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (rows) => {
          this.ntRows.set(rows);
          const current = this.ntSelectedId();
          if (!current || !rows.some((r) => r.id === current)) {
            this.selectTemplate(rows[0] ?? null);
          }
        },
        error: () => this.ntRows.set([]),
      });
  }
  protected selectTemplate(row: NtRow | null): void {
    this.ntSelectedId.set(row?.id ?? null);
    this.ntVariables.set([]);
    if (!row) {
      this.ntDraft.set(null);
      return;
    }
    this.ntDraft.set({
      subject: row.subject,
      bodyEmail: row.bodyEmail,
      bodySms: row.bodySms,
      active: row.active,
    });
    this.gateway.getTemplateVariables(row.code).subscribe({
      next: (v) => this.ntVariables.set(v),
      error: () => this.ntVariables.set([]),
    });
  }
  protected patchDraft(partial: Partial<NtDraft>): void {
    const current = this.ntDraft();
    if (current) {
      this.ntDraft.set({ ...current, ...partial });
    }
  }
  protected setNtQuery(value: string): void {
    this.ntQuery.set(value);
    this.ntPage.set(0);
  }
  protected setNtTypeFilter(value: string): void {
    this.ntTypeFilter.set(value);
    this.ntPage.set(0);
  }
  protected goNtPage(delta: number): void {
    const next = this.ntPage() + delta;
    if (next >= 0 && next < this.ntTotalPages()) {
      this.ntPage.set(next);
    }
  }
  /** Capture the Quill instance so variable inserts land at the cursor. */
  protected onEditorCreated(editor: Quill): void {
    this.editor = editor;
  }
  protected insertVar(token: string | undefined): void {
    const current = this.ntDraft();
    if (!current || !token) {
      return;
    }
    const placeholder = '##' + token + '##';
    if (this.editor) {
      // Insert at the caret (or end if unfocused); ngModelChange syncs the draft.
      const range = this.editor.getSelection(true);
      const index = range ? range.index : this.editor.getLength();
      this.editor.insertText(index, placeholder, 'user');
      this.editor.setSelection(index + placeholder.length, 0);
    } else {
      // Editor not ready: fall back to appending to the body string.
      this.ntDraft.set({ ...current, bodyEmail: insertVariable(current.bodyEmail, token) });
    }
    this.toaster.info(placeholder + ' inserted.');
  }
  protected sendTest(): void {
    const row = this.ntSelected();
    if (!row || this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.gateway
      .sendTestTemplate(row.id)
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: () => this.toaster.success('Test email queued to your address.'),
        error: () => undefined,
      });
  }
  protected saveTemplate(): void {
    const row = this.ntSelected();
    const draft = this.ntDraft();
    if (!row || !draft || this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.gateway
      .updateTemplate(row.id, {
        subject: draft.subject,
        bodyEmail: draft.bodyEmail,
        bodySms: draft.bodySms,
        isActive: draft.active,
        concurrencyStamp: row.concurrencyStamp,
      })
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Template saved.');
          this.loadTemplates();
        },
        error: () => undefined,
      });
  }

  // ===================== System Parameters =====================
  private loadParameters(): void {
    this.canEditParams.set(
      this.permission.getGrantedPolicy('CaseEvaluation.SystemParameters.Edit'),
    );
    this.gateway
      .getParameters()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (p) => this.params.set(p),
        error: () => this.params.set(null),
      });
  }
  protected paramValue(key: string): number {
    const p = this.params() as Record<string, unknown> | null;
    return (p?.[key] as number) ?? 0;
  }
  protected patchParam(key: string, value: number | string | boolean): void {
    const p = this.params();
    if (p) {
      this.params.set({ ...(p as Record<string, unknown>), [key]: value } as SystemParameterDto);
    }
  }
  protected revertParameters(): void {
    this.loadParameters();
    this.toaster.info('Reverted to saved values.');
  }
  protected saveParameters(): void {
    const p = this.params();
    if (!p || this.isBusy() || !this.canEditParams()) {
      return;
    }
    this.isBusy.set(true);
    this.gateway
      .updateParameters({
        appointmentLeadTime: p.appointmentLeadTime,
        appointmentMaxTimePQME: p.appointmentMaxTimePQME,
        appointmentMaxTimeAME: p.appointmentMaxTimeAME,
        appointmentMaxTimeOTHER: p.appointmentMaxTimeOTHER,
        appointmentMaxTimeInternal: p.appointmentMaxTimeInternal,
        appointmentCancelTime: p.appointmentCancelTime,
        appointmentDueDays: p.appointmentDueDays,
        appointmentDurationTime: p.appointmentDurationTime,
        autoCancelCutoffTime: p.autoCancelCutoffTime,
        jointDeclarationUploadCutoffDays: p.jointDeclarationUploadCutoffDays,
        pendingAppointmentOverDueNotificationDays: p.pendingAppointmentOverDueNotificationDays,
        reminderCutoffTime: p.reminderCutoffTime,
        isCustomField: p.isCustomField,
        ccEmailIds: p.ccEmailIds,
        concurrencyStamp: p.concurrencyStamp,
      })
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: (saved) => {
          this.params.set(saved);
          this.toaster.success('System parameters saved.');
        },
        error: () => undefined,
      });
  }

  // ===================== Users & Roles =====================
  private loadRoles(): void {
    this.gateway
      .listRoles()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (rows) => {
          this.roles.set(rows);
          const current = this.roleSelected();
          if (!current || !rows.some((r) => r.name === current)) {
            const first = rows.find((r) => !isLockedRole(r.name)) ?? rows[0];
            this.selectRole(first?.name ?? null);
          }
        },
        error: () => this.roles.set([]),
      });
  }
  protected rolesOfKind(kind: 'Internal' | 'External'): RoleRow[] {
    return this.roles().filter((r) => r.kind === kind);
  }
  protected selectRole(name: string | null): void {
    this.roleSelected.set(name);
    this.permGroups.set([]);
    this.granted.set(new Set<string>());
    if (!name) {
      return;
    }
    this.gateway.getPermissions(name).subscribe({
      next: (result: GetPermissionListResultDto) => {
        const groups = result.groups ?? [];
        this.permGroups.set(groups);
        const set = new Set<string>();
        for (const g of groups) {
          for (const p of g.permissions ?? []) {
            if (p.isGranted && p.name) {
              set.add(p.name);
            }
          }
        }
        this.granted.set(set);
      },
      error: () => {
        this.permGroups.set([]);
        this.granted.set(new Set<string>());
      },
    });
  }
  protected isGranted(name: string | undefined): boolean {
    return !!name && this.granted().has(name);
  }
  protected togglePermission(name: string | undefined): void {
    if (!name || this.roleLocked()) {
      return;
    }
    const set = new Set(this.granted());
    if (set.has(name)) {
      set.delete(name);
    } else {
      set.add(name);
    }
    this.granted.set(set);
  }
  protected savePermissions(): void {
    const role = this.roleSelected();
    if (!role || this.roleLocked() || this.isBusy()) {
      return;
    }
    const permissions: UpdatePermissionDto[] = [];
    for (const g of this.permGroups()) {
      for (const p of g.permissions ?? []) {
        if (p.name) {
          permissions.push({ name: p.name, isGranted: this.granted().has(p.name) });
        }
      }
    }
    this.isBusy.set(true);
    this.gateway
      .updatePermissions(role, permissions)
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: () => this.toaster.success('Permissions saved for ' + role + '.'),
        error: () => undefined,
      });
  }

  // ===================== Audit Logs =====================
  private loadAudit(): void {
    this.gateway
      .listAuditLogs(this.auditMethod() || undefined)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (rows) => this.auditRows.set(rows),
        error: () => this.auditRows.set([]),
      });
  }
  protected applyAuditMethod(method: string): void {
    this.auditMethod.set(method);
    this.loadAudit();
  }
  protected toggleAuditRow(id: string | undefined): void {
    if (!id) {
      return;
    }
    const set = new Set(this.auditOpen());
    if (set.has(id)) {
      set.delete(id);
    } else {
      set.add(id);
    }
    this.auditOpen.set(set);
  }
  protected isAuditOpen(id: string | undefined): boolean {
    return !!id && this.auditOpen().has(id);
  }
  protected exportAudit(): void {
    const csv = buildAuditCsv(
      this.auditShown().map((l) => ({
        time: l.executionTime ?? '',
        user: l.userName ?? 'anonymous',
        method: l.httpMethod ?? '',
        url: l.url ?? '',
        status: l.httpStatusCode ?? 0,
        durationMs: l.executionDuration ?? 0,
        ip: l.clientIpAddress ?? '',
        client: l.browserInfo ?? '',
        tenant: l.tenantName ?? '--',
      })),
    );
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = 'audit-logs.csv';
    anchor.click();
    URL.revokeObjectURL(url);
    this.toaster.success('Audit CSV exported.');
  }
}
