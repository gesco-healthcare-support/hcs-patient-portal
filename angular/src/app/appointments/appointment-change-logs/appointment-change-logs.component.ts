import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import {
  AppointmentChangeLogService,
  AppointmentChangeLogDto,
} from '../../proxy/appointment-change-logs';

/**
 * Per-appointment change-log viewer. Group K (G-02-02) repoints this from ABP's
 * raw single-FQN audit endpoint to the feature endpoint
 * (`/api/app/appointment-change-logs/by-appointment/{id}`), which aggregates the
 * appointment AND its child entities (injury / body part / claim examiner /
 * insurance) and returns PHI-redacted, per-field rows. Sensitive values arrive
 * already masked (valueRedacted = true), so the template never sees raw PHI.
 *
 * The link is gated by CaseEvaluation.AppointmentChangeLogs (internal only).
 */
@Component({
  selector: 'app-appointment-change-logs',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, LocalizationPipe],
  templateUrl: './appointment-change-logs.component.html',
})
export class AppointmentChangeLogsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly changeLogService = inject(AppointmentChangeLogService);

  appointmentId: string | null = null;
  isLoading = true;
  errorMessage = '';
  entries: AppointmentChangeLogDto[] = [];

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
  }

  back(): void {
    if (this.appointmentId) {
      this.router.navigate(['/appointments/view', this.appointmentId]);
    } else {
      this.router.navigate(['/appointments']);
    }
  }
}
