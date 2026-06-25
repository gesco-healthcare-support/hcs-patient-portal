import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import type { AppointmentChangeLogDto } from '../proxy/appointment-change-logs/models';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { changeTypeMeta, groupChangeLogEntries, type ClgKindMeta } from './clg-log.util';

/**
 * Internal Workflow (Prompt 13) -- shared change-log timeline. Renders the
 * PHI-redacted per-field rows as collapsible per-save entries with field diffs
 * (.clg-* in styles/_clg-log.scss). Reused by the global change-log list and the
 * per-appointment view so the presentation + redaction handling stay in one place.
 * Redacted rows render "value hidden" -- raw PHI is never shown (and never arrives).
 */
@Component({
  selector: 'app-change-log-timeline',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, IconComponent],
  templateUrl: './change-log-timeline.component.html',
})
export class ChangeLogTimelineComponent {
  readonly rows = input<AppointmentChangeLogDto[]>([]);

  private readonly router = inject(Router);
  protected readonly openKeys = signal<ReadonlySet<string>>(new Set());
  protected readonly entries = computed(() => groupChangeLogEntries(this.rows()));

  protected meta(changeType: string | null | undefined): ClgKindMeta {
    return changeTypeMeta(changeType);
  }

  protected isOpen(key: string): boolean {
    return this.openKeys().has(key);
  }

  protected toggle(key: string): void {
    const next = new Set(this.openKeys());
    if (next.has(key)) {
      next.delete(key);
    } else {
      next.add(key);
    }
    this.openKeys.set(next);
  }

  protected openAppointment(appointmentId: string | null | undefined, event: Event): void {
    event.stopPropagation();
    if (appointmentId) {
      void this.router.navigateByUrl(`/appointments/view/${appointmentId}`);
    }
  }
}
