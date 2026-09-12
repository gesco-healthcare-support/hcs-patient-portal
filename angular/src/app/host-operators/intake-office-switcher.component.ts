import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ImpersonationService } from '@volo/abp.commercial.ng.ui/config';
import { finalize } from 'rxjs/operators';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { OfficeNamePipe } from '../shared/pipes/office-name.pipe';
import { IntakeAssignmentsService } from '../proxy/host-operators/intake-assignments.service';
import type { IntakeOfficeMetricsDto } from '../proxy/host-operators/models';

/**
 * Phase D (2026-06-25) -- the host Intake operator's landing page. Lists ONLY
 * the offices the operator is assigned to (server-filtered, gated by
 * CaseEvaluation.IntakeImpersonation) and switches into one. The switch posts a
 * tenant-impersonation with NO username, so the custom grant
 * (HostIntakeImpersonationExtensionGrant) forces the operator's own limited
 * shadow Intake user as the target. The per-office assignment gate (server-side,
 * deny-by-default) is the real boundary -- this list is a convenience.
 *
 * QA item 9 (2026-06-30): the bland list is now a view-only landing dashboard --
 * each assigned practice is a card with per-office metrics (pending requests,
 * today's appointments, pending change-requests) from GetMyOfficeMetrics, each
 * counted inside that office's own database (isolation).
 */
@Component({
  selector: 'app-intake-office-switcher',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [IconComponent, OfficeNamePipe],
  styles: `
    .ho-cards {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 14px;
      margin-top: 8px;
    }
    .ho-card {
      display: flex;
      flex-direction: column;
      gap: 14px;
      padding: 16px;
      border: 1px solid var(--border);
      border-radius: 12px;
      background: #fff;
    }
    .ho-card__name {
      display: flex;
      align-items: center;
      gap: 8px;
      color: var(--n-800, #1f2c3d);
      font-size: 15px;
    }
    .ho-card__stats {
      display: flex;
      gap: 10px;
    }
    .ho-stat {
      flex: 1;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 2px;
      padding: 10px 6px;
      background: var(--blue-50, #eff6ff);
      border-radius: 9px;
      text-align: center;
    }
    .ho-stat__num {
      font-size: 20px;
      font-weight: 700;
      color: var(--n-800, #1f2c3d);
    }
    .ho-stat__lbl {
      font-size: 11px;
      color: var(--n-500, #6b7684);
      line-height: 1.2;
    }
  `,
  template: `
    <section class="ho-switcher">
      <header class="ho-switcher__head">
        <!-- UI label: 'Select a Practice' (code: office) -->
        <h1>Select a Practice</h1>
        <!-- UI label: 'practices' (code: offices) -->
        <p>Switch into one of your assigned practices to begin intake work.</p>
      </header>

      @if (loading()) {
        <!-- UI label: 'practices' (code: offices) -->
        <p class="ho-switcher__muted">Loading your practices...</p>
      } @else if (metrics().length === 0) {
        <!-- UI label: 'practice' (code: office) -->
        <p class="ho-switcher__muted">
          You have no practice assignments yet. Ask an administrator to assign you to a practice.
        </p>
      } @else {
        <div class="ho-cards">
          @for (m of metrics(); track m.officeId) {
            <article class="ho-card">
              <div class="ho-card__name">
                <app-icon name="map" [size]="16" />
                <b>{{ m.officeName | officeName }}</b>
              </div>
              <div class="ho-card__stats">
                <div class="ho-stat">
                  <span class="ho-stat__num">{{ m.pendingRequests ?? 0 }}</span>
                  <span class="ho-stat__lbl">Pending requests</span>
                </div>
                <div class="ho-stat">
                  <span class="ho-stat__num">{{ m.todayAppointments ?? 0 }}</span>
                  <span class="ho-stat__lbl">Today's appointments</span>
                </div>
                <div class="ho-stat">
                  <span class="ho-stat__num">{{ m.pendingChangeRequests ?? 0 }}</span>
                  <span class="ho-stat__lbl">Pending change requests</span>
                </div>
              </div>
              <button
                type="button"
                class="ho-switcher__btn"
                [disabled]="busy()"
                (click)="switchInto(m)"
              >
                <!-- UI label: 'Enter practice' (code: office) -->
                Enter practice
              </button>
            </article>
          }
        </div>
      }
    </section>
  `,
})
export class IntakeOfficeSwitcherComponent {
  private readonly assignments = inject(IntakeAssignmentsService);
  private readonly impersonation = inject(ImpersonationService);
  private readonly toaster = inject(ToasterService);

  protected readonly metrics = signal<IntakeOfficeMetricsDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);

  constructor() {
    this.assignments
      .getMyOfficeMetrics()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => this.metrics.set(res.items ?? []),
        error: () => this.metrics.set([]),
      });
  }

  protected switchInto(office: IntakeOfficeMetricsDto): void {
    const officeId = office.officeId;
    if (this.busy() || !officeId) {
      return;
    }
    this.busy.set(true);
    // UI label: fallback 'practice' (code: office)
    this.toaster.info('Switching into ' + (office.officeName ?? 'practice') + '...');
    // Empty username -> the custom grant forces the operator's own shadow user.
    this.impersonation
      .impersonateTenant(officeId, '')
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({ error: () => undefined });
  }
}
