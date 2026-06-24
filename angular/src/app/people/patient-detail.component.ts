import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
  inject,
  signal,
} from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { StatusPillComponent } from '../shared/ui/status-pill/status-pill.component';
import { appointmentStatusToPill } from '../shared/ui/status-pill/appointment-status.util';
import { AppointmentStatusType } from '../proxy/enums/appointment-status-type.enum';
import { genderOptions } from '../proxy/enums/gender.enum';
import { phoneNumberTypeOptions } from '../proxy/enums/phone-number-type.enum';
import type { AppointmentWithNavigationPropertiesDto } from '../proxy/appointments/models';
import { PeopleSectionGateway } from './people-section.gateway';
import { avatarColor, initials, maskSsn, PORTAL_LABEL, PersonRow } from './people.util';

/**
 * Patient detail view (Prompt 15): identity header with portal chip +
 * Invite-to-portal deep-link, four demographic cards, and the patient's
 * appointments (loaded via the B5 patientId filter). SSN is always masked --
 * the full value is never fetched or shown here.
 */
@Component({
  selector: 'app-patient-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, DatePipe, IconComponent, StatusPillComponent],
  templateUrl: './patient-detail.component.html',
})
export class PatientDetailComponent {
  private readonly gateway = inject(PeopleSectionGateway);
  private readonly genders = genderOptions;
  private readonly phoneTypes = phoneNumberTypeOptions;

  @Input({ required: true }) set patient(value: PersonRow) {
    this.row.set(value);
    this.loadAppointments(value.id);
  }
  /** Whether the viewer may invite (drives the Invite-to-portal button). */
  @Input() canInvite = false;
  /** Whether the viewer may edit (drives the Edit patient button). */
  @Input() canEdit = false;

  @Output() back = new EventEmitter<void>();
  @Output() edit = new EventEmitter<PersonRow>();
  @Output() invite = new EventEmitter<PersonRow>();

  protected readonly row = signal<PersonRow>({} as PersonRow);
  protected readonly appointments = signal<AppointmentWithNavigationPropertiesDto[]>([]);
  protected readonly loadingAppts = signal(true);

  protected readonly portalLabel = PORTAL_LABEL;
  protected readonly maskSsn = maskSsn;
  protected readonly initials = initials;
  protected readonly avatarColor = avatarColor;

  protected get fullName(): string {
    const p = this.row();
    return [p.firstName, p.middleName, p.lastName].filter(Boolean).join(' ');
  }

  protected pill(a: AppointmentWithNavigationPropertiesDto) {
    return appointmentStatusToPill(
      a.appointment?.appointmentStatus ?? AppointmentStatusType.Pending,
    );
  }

  protected genderLabel(id: number | null | undefined): string {
    return this.genders.find((g) => g.value === id)?.key ?? '';
  }

  protected phoneTypeLabel(id: number | null | undefined): string {
    return this.phoneTypes.find((p) => p.value === id)?.key ?? '';
  }

  private loadAppointments(patientId: string): void {
    this.loadingAppts.set(true);
    this.gateway.appointmentsForPatient(patientId).subscribe({
      next: (items) => {
        this.appointments.set(items);
        this.loadingAppts.set(false);
      },
      error: () => {
        this.appointments.set([]);
        this.loadingAppts.set(false);
      },
    });
  }
}
