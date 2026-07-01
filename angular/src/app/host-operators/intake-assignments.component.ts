import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { finalize } from 'rxjs/operators';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { OfficeNamePipe } from '../shared/pipes/office-name.pipe';
import { IntakeAssignmentsService } from '../proxy/host-operators/intake-assignments.service';
import type { IntakeOfficeAssignmentDto } from '../proxy/host-operators/models';
import type { LookupDto } from '../proxy/shared/models';

/** One practice a staff member is assigned to (a single IntakeOfficeAssignment row). */
interface AssignedPractice {
  officeId: string;
  officeName: string;
}

/** A staff member grouped with all their assigned practices (QA item 8). */
interface StaffAssignments {
  operatorUserId: string;
  operatorName: string;
  operatorEmail: string;
  practices: AssignedPractice[];
}

/**
 * Phase D (2026-06-25) -- intake office-assignment management for IT Admin and
 * the host Staff Supervisor (gated by CaseEvaluation.IntakeAssignments.Manage).
 * Assigning eagerly provisions the operator's limited shadow Intake user in the
 * office's database; unassigning disables it. The assignment rows are the
 * deny-by-default boundary the impersonation grant enforces.
 *
 * QA item 8 (2026-06-30): the flat one-row-per-(staff,practice) table is grouped
 * into one expandable row per staff. Grouping is client-side over GetListAsync
 * (assignments are a bounded set), so no backend/proxy change. The top assign form
 * is unchanged; each expanded staff row manages its own practices.
 */
@Component({
  selector: 'app-intake-assignments',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, IconComponent, OfficeNamePipe],
  styles: `
    .ho-assign__search {
      margin: 16px 0 8px;
    }
    .ho-assign__search input {
      width: 100%;
      max-width: 360px;
      height: 40px;
      padding: 0 12px;
      border: 1px solid var(--border);
      border-radius: 9px;
      font: inherit;
    }
    .ho-stafflist {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .ho-staffrow {
      border: 1px solid var(--border);
      border-radius: 10px;
      background: #fff;
      overflow: hidden;
    }
    .ho-staffrow__head {
      display: flex;
      align-items: center;
      gap: 12px;
      width: 100%;
      padding: 12px 14px;
      background: none;
      border: 0;
      cursor: pointer;
      text-align: left;
      font: inherit;
    }
    .ho-staffrow__head:hover {
      background: var(--blue-50, #eff6ff);
    }
    .ho-staffrow__name {
      display: flex;
      flex-direction: column;
      flex: 1;
      min-width: 0;
    }
    .ho-staffrow__name b {
      color: var(--n-800, #1f2c3d);
    }
    .ho-staffrow__email {
      font-size: 12px;
      color: var(--n-500, #6b7684);
    }
    .ho-staffrow__count {
      font-size: 12px;
      color: var(--n-500, #6b7684);
      white-space: nowrap;
    }
    .ho-staffrow__practices {
      list-style: none;
      margin: 0;
      padding: 4px 14px 12px 42px;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .ho-staffrow__practices li {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .ho-staffrow__practices li .nm {
      flex: 1;
    }
  `,
  template: `
    <section class="ho-assign">
      <header class="ho-assign__head">
        <!-- UI label: 'Staff Assignments' (code: intake office assignments) -->
        <h1>Staff Assignments</h1>
        <!-- UI label: 'Assign practices to intake staff' (code: offices/operator) -->
        <p>Assign practices to intake staff.</p>
      </header>

      <form class="ho-assign__form" (ngSubmit)="assign()">
        <label>
          <!-- UI label: 'Staff' (code: operator) -->
          Staff
          <select [(ngModel)]="operatorId" name="operatorId" [disabled]="busy()">
            <!-- UI label: 'Select staff...' (code: operator) -->
            <option value="">Select staff...</option>
            @for (op of operators(); track op.id) {
              <option [value]="op.id">{{ op.displayName }}</option>
            }
          </select>
        </label>
        <label>
          <!-- UI label: 'Practice' (code: office) -->
          Practice
          <select [(ngModel)]="officeId" name="officeId" [disabled]="busy()">
            <!-- UI label: 'Select a practice...' (code: office) -->
            <option value="">Select a practice...</option>
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

      <div class="ho-assign__search">
        <!-- UI label: search 'staff or practice' (code: operator/office) -->
        <input
          type="search"
          [ngModel]="search()"
          (ngModelChange)="search.set($event)"
          placeholder="Search by staff or practice..."
          aria-label="Search staff assignments"
        />
      </div>

      <div class="ho-stafflist">
        @for (s of staff(); track s.operatorUserId) {
          <div class="ho-staffrow" [class.open]="isExpanded(s.operatorUserId)">
            <button
              type="button"
              class="ho-staffrow__head"
              [attr.aria-expanded]="isExpanded(s.operatorUserId)"
              (click)="toggleExpand(s.operatorUserId)"
            >
              <app-icon
                [name]="isExpanded(s.operatorUserId) ? 'chevDown' : 'chevRight'"
                [size]="16"
              />
              <span class="ho-staffrow__name">
                <b>{{ s.operatorName }}</b>
                <span class="ho-staffrow__email">{{ s.operatorEmail }}</span>
              </span>
              <!-- UI label: 'practice(s)' (code: offices) -->
              <span class="ho-staffrow__count">
                {{ s.practices.length }} {{ s.practices.length === 1 ? 'practice' : 'practices' }}
              </span>
            </button>
            @if (isExpanded(s.operatorUserId)) {
              <ul class="ho-staffrow__practices">
                @for (p of s.practices; track p.officeId) {
                  <li>
                    <span class="i"><app-icon name="map" [size]="15" /></span>
                    <span class="nm">{{ p.officeName | officeName }}</span>
                    <button
                      type="button"
                      class="ho-assign__btn ho-assign__unassign"
                      [disabled]="busy()"
                      (click)="unassign(s, p)"
                    >
                      <app-icon name="trash" [size]="14" />
                      Unassign
                    </button>
                  </li>
                } @empty {
                  <li class="ho-assign__muted">No practices assigned.</li>
                }
              </ul>
            }
          </div>
        } @empty {
          <!-- UI label: 'staff' (code: operator) -->
          <p class="ho-assign__muted">No staff assignments yet.</p>
        }
      </div>
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

  /** Raw per-(staff,practice) assignment rows; grouped into staff() below. */
  private readonly rawAssignments = signal<IntakeOfficeAssignmentDto[]>([]);
  protected readonly search = signal('');
  private readonly expanded = signal<Set<string>>(new Set<string>());

  /** One entry per staff, each carrying their assigned practices (item 8 grouping). */
  protected readonly staff = computed<StaffAssignments[]>(() => {
    const byStaff = new Map<string, StaffAssignments>();
    for (const a of this.rawAssignments()) {
      const uid = a.operatorUserId ?? '';
      if (!uid) {
        continue;
      }
      let group = byStaff.get(uid);
      if (!group) {
        group = {
          operatorUserId: uid,
          operatorName: a.operatorName ?? '',
          operatorEmail: a.operatorEmail ?? '',
          practices: [],
        };
        byStaff.set(uid, group);
      }
      group.practices.push({ officeId: a.officeId ?? '', officeName: a.officeName ?? '' });
    }

    const groups = Array.from(byStaff.values());
    for (const g of groups) {
      g.practices.sort((x, y) => x.officeName.localeCompare(y.officeName));
    }
    groups.sort((x, y) => x.operatorName.localeCompare(y.operatorName));

    const query = this.search().trim().toLowerCase();
    if (!query) {
      return groups;
    }
    return groups.filter(
      (g) =>
        g.operatorName.toLowerCase().includes(query) ||
        g.operatorEmail.toLowerCase().includes(query) ||
        g.practices.some((p) => p.officeName.toLowerCase().includes(query)),
    );
  });

  constructor() {
    this.service.getAssignableOperators().subscribe((res) => this.operators.set(res.items ?? []));
    this.service.getOfficeOptions().subscribe((res) => this.offices.set(res.items ?? []));
    this.loadAssignments();
  }

  protected isExpanded(operatorUserId: string): boolean {
    return this.expanded().has(operatorUserId);
  }

  protected toggleExpand(operatorUserId: string): void {
    const next = new Set(this.expanded());
    if (next.has(operatorUserId)) {
      next.delete(operatorUserId);
    } else {
      next.add(operatorUserId);
    }
    this.expanded.set(next);
  }

  private loadAssignments(): void {
    this.service.getList().subscribe({
      next: (res) => this.rawAssignments.set(res.items ?? []),
      error: () => this.rawAssignments.set([]),
    });
  }

  protected assign(): void {
    if (this.busy() || !this.operatorId || !this.officeId) {
      return;
    }
    this.busy.set(true);
    const assignedOperator = this.operatorId;
    this.service
      .assign({ operatorUserId: this.operatorId, officeId: this.officeId })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          // UI label: 'Staff assigned.' (code: operator)
          this.toaster.success('Staff assigned.');
          this.operatorId = '';
          this.officeId = '';
          // Auto-expand the staff we just assigned so the new practice is visible.
          const next = new Set(this.expanded());
          next.add(assignedOperator);
          this.expanded.set(next);
          this.loadAssignments();
        },
        error: () => undefined,
      });
  }

  protected unassign(staffRow: StaffAssignments, practice: AssignedPractice): void {
    if (this.busy() || !staffRow.operatorUserId || !practice.officeId) {
      return;
    }
    this.busy.set(true);
    this.service
      .unassign(staffRow.operatorUserId, practice.officeId)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Assignment removed.');
          this.loadAssignments();
        },
        error: () => undefined,
      });
  }
}
