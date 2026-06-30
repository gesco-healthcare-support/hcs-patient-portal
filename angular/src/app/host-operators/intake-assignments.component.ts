import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { Subject } from 'rxjs';
import { finalize, map } from 'rxjs/operators';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { OfficeNamePipe } from '../shared/pipes/office-name.pipe';
import { ManagedTableComponent } from '../shared/components/managed-table/managed-table.component';
import {
  ManagedTableCellDirective,
  ManagedTableRowActionsDirective,
} from '../shared/components/managed-table/managed-table-cell.directive';
import type {
  ManagedTableColumn,
  ManagedTableDataSource,
} from '../shared/components/managed-table/managed-table.models';
import { IntakeAssignmentsService } from '../proxy/host-operators/intake-assignments.service';
import type { IntakeOfficeAssignmentDto } from '../proxy/host-operators/models';
import type { LookupDto } from '../proxy/shared/models';

/**
 * Phase D (2026-06-25) -- intake office-assignment management for IT Admin and
 * the host Staff Supervisor (gated by CaseEvaluation.IntakeAssignments.Manage).
 * Assigning eagerly provisions the operator's limited shadow Intake user in the
 * office's database; unassigning disables it. The assignment rows are the
 * deny-by-default boundary the impersonation grant enforces.
 */
@Component({
  selector: 'app-intake-assignments',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    IconComponent,
    OfficeNamePipe,
    ManagedTableComponent,
    ManagedTableCellDirective,
    ManagedTableRowActionsDirective,
  ],
  template: `
    <section class="ho-assign">
      <header class="ho-assign__head">
        <h1>Intake office assignments</h1>
        <p>Control which offices each Intake operator may switch into.</p>
      </header>

      <form class="ho-assign__form" (ngSubmit)="assign()">
        <label>
          Operator
          <select [(ngModel)]="operatorId" name="operatorId" [disabled]="busy()">
            <option value="">Select an operator...</option>
            @for (op of operators(); track op.id) {
              <option [value]="op.id">{{ op.displayName }}</option>
            }
          </select>
        </label>
        <label>
          Office
          <select [(ngModel)]="officeId" name="officeId" [disabled]="busy()">
            <option value="">Select an office...</option>
            @for (office of offices(); track office.id) {
              <option [value]="office.id">{{ office.displayName | officeName }}</option>
            }
          </select>
        </label>
        <button
          type="submit"
          class="ho-assign__btn"
          [disabled]="busy() || !operatorId || !officeId"
        >
          Assign
        </button>
      </form>

      <app-managed-table
        [dataSource]="dataSource"
        [columns]="columns"
        [busy]="busy()"
        [reload$]="reload$"
        [pageSize]="20"
        trackByKey="id"
        searchPlaceholder="Search by operator or office..."
        emptyText="No assignments yet."
      >
        <span *managedTableCell="'officeName'; let row">{{ row.officeName | officeName }}</span>
        <button
          type="button"
          class="ho-assign__btn ho-assign__unassign"
          *managedTableRowActions="let row"
          [disabled]="busy()"
          (click)="unassign(row)"
        >
          <app-icon name="trash" />
          Unassign
        </button>
      </app-managed-table>
    </section>
  `,
})
export class IntakeAssignmentsComponent {
  private readonly service = inject(IntakeAssignmentsService);
  private readonly toaster = inject(ToasterService);

  protected readonly operators = signal<LookupDto<string>[]>([]);
  protected readonly offices = signal<LookupDto<string>[]>([]);
  protected readonly busy = signal(false);

  protected operatorId = '';
  protected officeId = '';

  /** Refetch trigger for the assignments table after assign / unassign. */
  protected readonly reload$ = new Subject<void>();

  protected readonly columns: ManagedTableColumn[] = [
    { key: 'operatorName', header: 'Operator', sortable: true, sortKey: 'operatorName' },
    { key: 'operatorEmail', header: 'Email', sortable: true, sortKey: 'operatorEmail' },
    { key: 'officeName', header: 'Office', sortable: true, sortKey: 'officeName' },
  ];

  /** Server-paged data source backed by the paged + batch-loaded GetPagedListAsync. */
  protected readonly dataSource: ManagedTableDataSource<IntakeOfficeAssignmentDto> = (query) =>
    this.service
      .getPagedList({
        filter: query.search.trim() || undefined,
        sorting: query.sorting || undefined,
        skipCount: query.skipCount,
        maxResultCount: query.maxResultCount,
      })
      .pipe(map((r) => ({ items: r.items ?? [], totalCount: r.totalCount ?? 0 })));

  constructor() {
    this.service.getAssignableOperators().subscribe((res) => this.operators.set(res.items ?? []));
    this.service.getOfficeOptions().subscribe((res) => this.offices.set(res.items ?? []));
  }

  protected assign(): void {
    if (this.busy() || !this.operatorId || !this.officeId) {
      return;
    }
    this.busy.set(true);
    this.service
      .assign({ operatorUserId: this.operatorId, officeId: this.officeId })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Operator assigned.');
          this.operatorId = '';
          this.officeId = '';
          this.reload$.next();
        },
        error: () => undefined,
      });
  }

  protected unassign(row: IntakeOfficeAssignmentDto): void {
    const operatorUserId = row.operatorUserId;
    const officeId = row.officeId;
    if (this.busy() || !operatorUserId || !officeId) {
      return;
    }
    this.busy.set(true);
    this.service
      .unassign(operatorUserId, officeId)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Assignment removed.');
          this.reload$.next();
        },
        error: () => undefined,
      });
  }
}
