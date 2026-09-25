import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RestService } from '@abp/ng.core';
import { firstValueFrom } from 'rxjs';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { PacificDatePipe } from '../shared/pipes/pacific-date.pipe';
import { AppointmentStatusType } from '../proxy/enums/appointment-status-type.enum';

/** One appointment in the report, mirroring `CaseTrackerMissingIntakeItemDto`. */
export interface MissingIntakeItem {
  appointmentId: string;
  confirmationNumber: string;
  /** `AppointmentStatusType`, serialised as its number. */
  status: number;
  approvedAt: string;
}

/** One office's section, mirroring `CaseTrackerMissingIntakeOfficeDto`. */
export interface MissingIntakeOffice {
  officeId: string;
  officeName: string;
  failed: boolean;
  firstIntakeRowAt: string | null;
  likelyLost: MissingIntakeItem[];
  settling: MissingIntakeItem[];
  beforeIntegration: MissingIntakeItem[];
  beforeIntegrationCount: number;
}

/** Mirrors `CaseTrackerMissingIntakeReportDto`. */
export interface MissingIntakeReport {
  generatedAt: string;
  offices: MissingIntakeOffice[];
}

/**
 * The missing-intake report (#944): approved appointments, in every office, that have no Case Tracker intake
 * row -- with no date limit, which is what the hourly sweep cannot offer.
 *
 * <p>REPORT ONLY. The panel reads and shows; it has no control that queues or sends anything, because a
 * wholesale resend of old appointments would create cases the Case Tracker's staff already made by hand. The
 * way to act on one row is the manual push on the appointment inside its office.</p>
 *
 * <p>Nothing loads until the operator asks: the check reads every appointment in every office. Old history --
 * appointments approved before an office's integration wrote anything -- is counted and kept collapsed, so the
 * likely losses are what the eye lands on.</p>
 *
 * <p>A sibling of the offices and failures panels in the same admin-hub section, for the reason
 * `CaseTrackerOfficesComponent` gives.</p>
 */
@Component({
  selector: 'app-case-tracker-missing-intakes',
  standalone: true,
  imports: [CommonModule, IconComponent, PacificDatePipe],
  templateUrl: './case-tracker-missing-intakes.component.html',
  styleUrl: './case-tracker-missing-intakes.component.scss',
})
export class CaseTrackerMissingIntakesComponent {
  private readonly rest = inject(RestService);

  /** Null until a check has succeeded. */
  protected readonly report = signal<MissingIntakeReport | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Offices whose before-integration list is open. */
  protected readonly expanded = signal<ReadonlySet<string>>(new Set<string>());

  /** The report's offices, or none before a check has succeeded. */
  protected readonly offices = computed(() => this.report()?.offices ?? []);

  protected readonly likelyLostTotal = computed(
    () => this.report()?.offices.reduce((sum, o) => sum + o.likelyLost.length, 0) ?? 0,
  );

  protected async check(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const report = await firstValueFrom(
        this.rest.request<null, MissingIntakeReport>(
          { method: 'GET', url: '/api/app/case-tracker/missing-intakes' },
          { apiName: 'Default' },
        ),
      );
      this.report.set(report ?? { generatedAt: '', offices: [] });
      this.expanded.set(new Set<string>());
    } catch {
      // A failed check shows no list at all: a stale or partial one would read as a current answer.
      this.report.set(null);
      this.error.set('The check could not be run. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  protected toggleBefore(officeId: string): void {
    const next = new Set(this.expanded());
    if (next.has(officeId)) {
      next.delete(officeId);
    } else {
      next.add(officeId);
    }
    this.expanded.set(next);
  }

  protected isExpanded(officeId: string): boolean {
    return this.expanded().has(officeId);
  }

  protected statusLabel(status: number): string {
    return AppointmentStatusType[status] ?? String(status);
  }
}
