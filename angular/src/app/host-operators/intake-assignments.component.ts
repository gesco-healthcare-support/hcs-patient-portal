import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { finalize } from 'rxjs/operators';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { OfficeNamePipe } from '../shared/pipes/office-name.pipe';
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
  imports: [CommonModule, FormsModule, IconComponent, OfficeNamePipe],
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

      @if (loading()) {
        <p class="ho-assign__muted">Loading assignments...</p>
      } @else if (rows().length === 0) {
        <p class="ho-assign__muted">No assignments yet.</p>
      } @else {
        <table class="ho-assign__table">
          <thead>
            <tr>
              <th>Operator</th>
              <th>Email</th>
              <th>Office</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (row of rows(); track row.id) {
              <tr>
                <td>{{ row.operatorName }}</td>
                <td>{{ row.operatorEmail }}</td>
                <td>{{ row.officeName | officeName }}</td>
                <td>
                  <button
                    type="button"
                    class="ho-assign__unassign"
                    [disabled]="busy()"
                    (click)="unassign(row)"
                  >
                    <app-icon name="trash" />
                    Unassign
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      }
    </section>
  `,
})
export class IntakeAssignmentsComponent {
  private readonly service = inject(IntakeAssignmentsService);
  private readonly toaster = inject(ToasterService);

  protected readonly rows = signal<IntakeOfficeAssignmentDto[]>([]);
  protected readonly operators = signal<LookupDto<string>[]>([]);
  protected readonly offices = signal<LookupDto<string>[]>([]);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);

  protected operatorId = '';
  protected officeId = '';

  constructor() {
    this.service.getAssignableOperators().subscribe((res) => this.operators.set(res.items ?? []));
    this.service.getOfficeOptions().subscribe((res) => this.offices.set(res.items ?? []));
    this.reload();
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
          this.reload();
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
          this.reload();
        },
        error: () => undefined,
      });
  }

  private reload(): void {
    this.loading.set(true);
    this.service
      .getList()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => this.rows.set(res.items ?? []),
        error: () => this.rows.set([]),
      });
  }
}
