import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToasterService } from '@abp/ng.theme.shared';
import { ImpersonationService } from '@volo/abp.commercial.ng.ui/config';
import { finalize } from 'rxjs/operators';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { IntakeAssignmentsService } from '../proxy/host-operators/intake-assignments.service';
import type { LookupDto } from '../proxy/shared/models';

/**
 * Phase D (2026-06-25) -- the host Intake operator's landing page. Lists ONLY
 * the offices the operator is assigned to (server-filtered via GetMyOffices,
 * gated by CaseEvaluation.IntakeImpersonation) and switches into one. The switch
 * posts a tenant-impersonation with NO username, so the custom grant
 * (HostIntakeImpersonationExtensionGrant) forces the operator's own limited
 * shadow Intake user as the target. The per-office assignment gate (server-side,
 * deny-by-default) is the real boundary -- this list is a convenience, not a
 * security control.
 */
@Component({
  selector: 'app-intake-office-switcher',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, IconComponent],
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
      } @else if (offices().length === 0) {
        <!-- UI label: 'practice' (code: office) -->
        <p class="ho-switcher__muted">
          You have no practice assignments yet. Ask an administrator to assign you to a practice.
        </p>
      } @else {
        <ul class="ho-switcher__list">
          @for (office of offices(); track office.id) {
            <li class="ho-switcher__item">
              <span class="ho-switcher__name">
                <app-icon name="map" />
                {{ office.displayName }}
              </span>
              <button
                type="button"
                class="ho-switcher__btn"
                [disabled]="busy()"
                (click)="switchInto(office)"
              >
                <!-- UI label: 'Enter practice' (code: office) -->
                Enter practice
              </button>
            </li>
          }
        </ul>
      }
    </section>
  `,
})
export class IntakeOfficeSwitcherComponent {
  private readonly assignments = inject(IntakeAssignmentsService);
  private readonly impersonation = inject(ImpersonationService);
  private readonly toaster = inject(ToasterService);

  protected readonly offices = signal<LookupDto<string>[]>([]);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);

  constructor() {
    this.assignments
      .getMyOffices()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => this.offices.set(res.items ?? []),
        error: () => this.offices.set([]),
      });
  }

  protected switchInto(office: LookupDto<string>): void {
    const officeId = office.id;
    if (this.busy() || !officeId) {
      return;
    }
    this.busy.set(true);
    // UI label: fallback 'practice' (code: office)
    this.toaster.info('Switching into ' + (office.displayName ?? 'practice') + '...');
    // Empty username -> the custom grant forces the operator's own shadow user.
    this.impersonation
      .impersonateTenant(officeId, '')
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({ error: () => undefined });
  }
}
