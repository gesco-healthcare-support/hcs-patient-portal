import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { finalize } from 'rxjs/operators';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { AppointmentTypeFieldConfigService } from '../proxy/appointment-type-field-configs/appointment-type-field-config.service';
import { ConfigSectionGateway } from './config-section.gateway';
import {
  buildFieldCatalog,
  CONFIG_SECTIONS,
  ConfigFormState,
  ConfigRow,
  ConfigSection,
  ConfigSectionKey,
  emptyFieldState,
  FieldConfigState,
  fieldStateFromConfigs,
  fieldStateToBatch,
  isDeleteLocked,
} from './cf-config.util';

/**
 * Configuration hub (Prompt 15). One standalone component mounted at all five
 * lookup routes; it reads `data.section` to know which lookup to render, so the
 * left rail is real routerLinks (deep-linkable + per-route guarded) rather than
 * in-component tabs. Shows usage counts + System lock chips, guards delete
 * (client pre-check + server 409/400), CRUDs via {@link ConfigSectionGateway},
 * and -- for Appointment Types -- an expandable Field Configuration panel.
 */
@Component({
  selector: 'app-internal-configuration',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, RouterLink, IconComponent],
  templateUrl: './internal-configuration.component.html',
})
export class InternalConfigurationComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly gateway = inject(ConfigSectionGateway);
  private readonly fieldConfigService = inject(AppointmentTypeFieldConfigService);
  private readonly permission = inject(PermissionService);
  private readonly toaster = inject(ToasterService);

  protected readonly sections = CONFIG_SECTIONS;
  protected readonly catalog = buildFieldCatalog();

  protected readonly section = signal<ConfigSectionKey>('types');
  protected readonly meta = computed<ConfigSection>(
    () => this.sections.find((s) => s.key === this.section()) ?? this.sections[0],
  );

  protected readonly loading = signal(true);
  protected readonly isBusy = signal(false);
  protected readonly rows = signal<ConfigRow[]>([]);
  protected readonly form = signal<ConfigFormState | null>(null);

  // Field Configuration (Appointment Types only).
  protected readonly fcOpenId = signal<string | null>(null);
  protected readonly fcBusy = signal(false);
  protected readonly fieldState = signal<Record<string, FieldConfigState>>({});

  constructor() {
    // The same component instance backs all five section routes; react to
    // data.section so rail navigation (and any route reuse) reloads correctly.
    this.route.data.pipe(takeUntilDestroyed()).subscribe((data) => {
      this.section.set((data['section'] as ConfigSectionKey) ?? 'types');
      this.fcOpenId.set(null);
      this.form.set(null);
      this.load();
    });
  }

  private load(): void {
    this.loading.set(true);
    this.gateway
      .list(this.section())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (rows) => this.rows.set(rows),
        error: () => this.rows.set([]),
      });
  }

  // ---- rail ----
  protected canSee(section: ConfigSection): boolean {
    return this.permission.getGrantedPolicy(section.policy);
  }

  // ---- per-action gating: only offer an action the user can actually perform,
  // so the UI never presents a button that 403s (mirrors the nav/rail gating). ----
  protected readonly canEditFieldConfig = this.permission.getGrantedPolicy(
    'CaseEvaluation.CustomFields.Edit',
  );
  protected readonly canCreate = computed(() =>
    this.permission.getGrantedPolicy(`${this.meta().policy}.Create`),
  );
  protected readonly canEdit = computed(() =>
    this.permission.getGrantedPolicy(`${this.meta().policy}.Edit`),
  );
  protected readonly canDelete = computed(() =>
    this.permission.getGrantedPolicy(`${this.meta().policy}.Delete`),
  );

  /** Row click: expand field config for types (when permitted), else edit (when permitted). */
  protected onRowClick(row: ConfigRow): void {
    if (this.meta().key === 'types' && this.canEditFieldConfig) {
      this.toggleFieldConfig(row);
    } else if (this.canEdit()) {
      this.openEdit(row);
    }
  }

  // ---- table helpers ----
  protected usageLabel(row: ConfigRow): string {
    const count = row.usageCount;
    if (count == null) {
      return '--';
    }
    const noun = this.meta().usageNoun;
    return `${count} ${noun}${count === 1 ? '' : 's'}`;
  }
  protected locked(row: ConfigRow): boolean {
    return isDeleteLocked(row);
  }
  protected deleteTitle(row: ConfigRow): string {
    if (row.isSystem) {
      return 'System -- locked';
    }
    if ((row.usageCount ?? 0) > 0) {
      return 'In use -- locked';
    }
    return 'Delete';
  }

  // ---- CRUD modal ----
  protected openNew(): void {
    this.form.set({
      id: null,
      name: '',
      description: '',
      isActive: true,
      isSystem: false,
      appointmentTypeId: null,
    });
  }
  protected openEdit(row: ConfigRow): void {
    this.form.set({
      id: row.id,
      name: row.name,
      description: row.description ?? '',
      isActive: row.isActive ?? true,
      isSystem: row.isSystem,
      appointmentTypeId: row.appointmentTypeId ?? null,
      concurrencyStamp: row.concurrencyStamp,
    });
  }
  protected closeModal(): void {
    if (!this.isBusy()) {
      this.form.set(null);
    }
  }
  protected patch(partial: Partial<ConfigFormState>): void {
    const current = this.form();
    if (current) {
      this.form.set({ ...current, ...partial });
    }
  }
  protected save(): void {
    const form = this.form();
    if (!form || this.isBusy()) {
      return;
    }
    if (!form.name.trim()) {
      this.toaster.warn('Name is required.');
      return;
    }
    const section = this.section();
    const request$ = form.id
      ? this.gateway.update(section, form)
      : this.gateway.create(section, form);
    this.isBusy.set(true);
    request$.pipe(finalize(() => this.isBusy.set(false))).subscribe({
      next: () => {
        this.toaster.success(`${this.capitalize(this.meta().singular)} saved.`);
        this.form.set(null);
        this.load();
      },
      error: () => undefined,
    });
  }

  // ---- delete (client guard + server 409/400) ----
  protected tryDelete(row: ConfigRow): void {
    if (this.isBusy()) {
      return;
    }
    const singular = this.meta().singular;
    if (row.isSystem) {
      this.toaster.warn(`System ${singular}s can't be deleted.`);
      return;
    }
    if ((row.usageCount ?? 0) > 0) {
      this.toaster.warn(`In use by ${row.usageCount} ${this.meta().usageNoun}(s) -- can't delete.`);
      return;
    }
    this.isBusy.set(true);
    this.gateway
      .delete(this.section(), row.id)
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success(`${this.capitalize(singular)} deleted.`);
          this.load();
        },
        // ABP surfaces the server-side system/in-use guard message (400/409).
        error: () => undefined,
      });
  }

  // ---- Field Configuration panel ----
  protected toggleFieldConfig(row: ConfigRow): void {
    if (this.fcOpenId() === row.id) {
      this.fcOpenId.set(null);
      return;
    }
    this.fcOpenId.set(row.id);
    this.fieldState.set(emptyFieldState());
    this.fieldConfigService.getByAppointmentTypeId(row.id).subscribe({
      next: (configs) => this.fieldState.set(fieldStateFromConfigs(configs)),
      error: () => this.fieldState.set(emptyFieldState()),
    });
  }
  protected fieldOf(key: string): FieldConfigState {
    return (
      this.fieldState()[key] ?? {
        hidden: false,
        readOnly: false,
        required: false,
        defaultValue: '',
      }
    );
  }
  protected setField(key: string, patch: Partial<FieldConfigState>): void {
    const state = this.fieldState();
    const current = state[key];
    if (current) {
      this.fieldState.set({ ...state, [key]: { ...current, ...patch } });
    }
  }
  protected saveFieldConfig(): void {
    const typeId = this.fcOpenId();
    if (!typeId || this.fcBusy()) {
      return;
    }
    this.fcBusy.set(true);
    this.fieldConfigService
      .saveForAppointmentType(typeId, fieldStateToBatch(this.fieldState()))
      .pipe(finalize(() => this.fcBusy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Field configuration saved.');
          this.fcOpenId.set(null);
        },
        error: () => undefined,
      });
  }

  private capitalize(value: string): string {
    return value.length ? value.charAt(0).toUpperCase() + value.slice(1) : value;
  }
}
