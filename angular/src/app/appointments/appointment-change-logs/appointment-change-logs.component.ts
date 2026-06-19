import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  AppointmentChangeLogService,
  AppointmentChangeLogDto,
} from '../../proxy/appointment-change-logs';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { ChangeLogTimelineComponent } from '../../appointment-change-logs/change-log-timeline.component';
import { AppointmentInfoRequestService } from '../../proxy/appointment-info-requests/appointment-info-request.service';
import type { AppointmentInfoRequestRoundDto } from '../../proxy/appointment-info-requests/models';
import { InfoRequestHistoryComponent } from '../appointment/components/info-request-history.component';

/**
 * Per-appointment change-log viewer. Group K (G-02-02) repoints this from ABP's
 * raw single-FQN audit endpoint to the feature endpoint
 * (`/api/app/appointment-change-logs/by-appointment/{id}`), which aggregates the
 * appointment AND its child entities (injury / body part / claim examiner /
 * insurance) and returns PHI-redacted, per-field rows. Sensitive values arrive
 * already masked (valueRedacted = true), so the template never sees raw PHI.
 *
 * #14: that audit projection scans only those 5 entity types, so booker resubmit
 * edits (Patient / DefenseAttorney) leave it near-empty. We augment the page with
 * the Send Back / request-info rounds (GetHistoryAsync) in a separate section
 * above the audit, so the meaningful resubmit history is visible here too. The two
 * sources load independently -- one failing does not blank the other.
 *
 * The link is gated by CaseEvaluation.AppointmentChangeLogs (internal only).
 */
@Component({
  selector: 'app-appointment-change-logs',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, IconComponent, ChangeLogTimelineComponent, InfoRequestHistoryComponent],
  templateUrl: './appointment-change-logs.component.html',
  styleUrl: './appointment-change-logs.component.scss',
})
export class AppointmentChangeLogsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly changeLogService = inject(AppointmentChangeLogService);
  private readonly infoRequestService = inject(AppointmentInfoRequestService);

  appointmentId: string | null = null;
  isLoading = true;
  errorMessage = '';
  entries: AppointmentChangeLogDto[] = [];

  /** Send Back / request-info rounds (newest-first); loaded independently. */
  rounds: AppointmentInfoRequestRoundDto[] = [];
  roundsError = false;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.errorMessage = 'No appointment id provided.';
      this.isLoading = false;
      return;
    }
    this.appointmentId = id;
    this.changeLogService.getByAppointment(id).subscribe({
      next: (rows) => {
        this.entries = rows ?? [];
        this.isLoading = false;
      },
      error: () => {
        this.errorMessage = 'Failed to load change log.';
        this.isLoading = false;
      },
    });
    // Independent of the audit call: a failure here surfaces an inline note in
    // the history section but leaves the audit timeline below intact.
    this.infoRequestService.getHistory(id).subscribe({
      next: (rows) => (this.rounds = rows ?? []),
      error: () => (this.roundsError = true),
    });
  }

  back(): void {
    if (this.appointmentId) {
      this.router.navigate(['/appointments/view', this.appointmentId]);
    } else {
      this.router.navigate(['/appointments']);
    }
  }
}
