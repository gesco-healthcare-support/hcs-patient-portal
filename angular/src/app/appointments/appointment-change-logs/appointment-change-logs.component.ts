import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import {
  AuditLogsService,
  EntityChangeWithUsernameDto,
  EntityChangeType,
} from '@volo/abp.ng.audit-logging/proxy';

/**
 * W2-4: per-appointment change-log viewer. Reads the appointment id from the
 * route param, calls the existing ABP audit-logging proxy to fetch entity
 * changes for that Appointment row, and renders a flat table of property
 * diffs (oldValue / newValue / propertyName / changeType / changeTime / user).
 *
 * The cap intentionally lives outside the appointment-view tab so the
 * permission guard (CaseEvaluation.AppointmentChangeLogs) can hide the link
 * without affecting the rest of the view page.
 */
@Component({
  selector: 'app-appointment-change-logs',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterLink, LocalizationPipe],
  templateUrl: './appointment-change-logs.component.html',
})
export class AppointmentChangeLogsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auditLogsService = inject(AuditLogsService);

  appointmentId: string | null = null;
  isLoading = true;
  errorMessage = '';
  entries: EntityChangeWithUsernameDto[] = [];

  // Full type name as ABP records it; matches the [Audited] entity FQN.
  private readonly entityTypeFullName = 'HealthcareSupport.CaseEvaluation.Appointments.Appointment';

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.errorMessage = 'No appointment id provided.';
      this.isLoading = false;
      return;
    }
    this.appointmentId = id;
    this.auditLogsService
      .getEntityChangesWithUsername({
        entityId: id,
        entityTypeFullName: this.entityTypeFullName,
      })
      .subscribe({
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

  changeTypeLabel(type: EntityChangeType): string {
    switch (type) {
      case EntityChangeType.Created:
        return 'Created';
      case EntityChangeType.Updated:
        return 'Updated';
      case EntityChangeType.Deleted:
        return 'Deleted';
      default:
        return 'Unknown';
    }
  }

  back(): void {
    if (this.appointmentId) {
      this.router.navigate(['/appointments/view', this.appointmentId]);
    } else {
      this.router.navigate(['/appointments']);
    }
  }
}
