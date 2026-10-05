import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  OnInit,
  inject,
  signal,
} from '@angular/core';

import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ConfigStateService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { Router } from '@angular/router';
import { AppointmentService } from '../proxy/appointments/appointment.service';
import { AppointmentDocumentService } from '../proxy/appointment-documents/appointment-document.service';
import { AppointmentDocumentDto } from '../proxy/appointment-documents/models';
import { DocumentStatus } from '../proxy/appointment-documents/document-status.enum';
import { AppointmentDocumentUrls } from '../appointment-documents/appointment-document-urls';
import { ExternalNavbarComponent } from '../shared/components/external-navbar/external-navbar.component';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { SkeletonComponent } from '../shared/ui/skeleton/skeleton.component';
import { EmptyStateComponent } from '../shared/ui/empty-state/empty-state.component';
import { performFullLogout } from '../shared/auth/full-logout';
import { CalendarDatePipe, PacificDatePipe } from '../shared/pipes/pacific-date.pipe';

/** One appointment accordion; its documents load the first time it is opened. */
export interface MyDocsAppointment {
  id: string;
  title: string;
  confirmation: string;
  appointmentDate?: string;
  open: boolean;
  state: 'idle' | 'loading' | 'loaded' | 'error';
  docs: AppointmentDocumentDto[];
}

/**
 * Redesign slice 7 (#729): every document across the caller's appointments.
 *
 * Access: nothing here decides visibility. The appointment list is the same
 * server-filtered call the external home uses, and each appointment's documents
 * come from the per-appointment endpoint the appointment page itself uses, which
 * runs the appointment read-access guard. A refusal shows as a per-appointment error.
 */
@Component({
  selector: 'app-my-documents',
  imports: [
    FormsModule,

    CalendarDatePipe,
    PacificDatePipe,
    ExternalNavbarComponent,
    IconComponent,
    SkeletonComponent,
    EmptyStateComponent,
  ],
  templateUrl: './my-documents.component.html',
  styleUrl: './my-documents.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyDocumentsComponent implements OnInit {
  private readonly appointmentService = inject(AppointmentService);
  private readonly documentService = inject(AppointmentDocumentService);
  private readonly urls = inject(AppointmentDocumentUrls);
  private readonly http = inject(HttpClient);
  private readonly toaster = inject(ToasterService);
  private readonly configState = inject(ConfigStateService);
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);

  protected readonly Status = DocumentStatus;
  protected readonly loading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly appointments = signal<MyDocsAppointment[]>([]);
  protected readonly query = signal('');
  protected readonly statusFilter = signal<number>(0);
  protected userEmail = '';
  protected clinicName = 'Appointment Portal';

  ngOnInit(): void {
    const user = this.configState.getOne('currentUser') as { email?: string; userName?: string };
    const tenant = this.configState.getOne('currentTenant') as { name?: string } | undefined;
    this.userEmail = user?.email || user?.userName || '';
    this.clinicName = tenant?.name || 'Appointment Portal';
    this.appointmentService.getList({ maxResultCount: 500 } as never).subscribe({
      next: (res) => {
        this.appointments.set(
          (res.items ?? []).map((r) => ({
            id: r.appointment?.id ?? '',
            title: r.appointmentType?.name ?? 'Appointment',
            confirmation: r.appointment?.requestConfirmationNumber ?? '',
            appointmentDate: r.appointment?.appointmentDate,
            open: false,
            state: 'idle' as const,
            docs: [],
          })),
        );
        this.loading.set(false);
      },
      error: () => {
        this.loadFailed.set(true);
        this.loading.set(false);
      },
    });
  }

  protected toggle(apt: MyDocsAppointment): void {
    this.patch(apt.id, { open: !apt.open });
    if (!apt.open && (apt.state === 'idle' || apt.state === 'error')) {
      this.loadDocs(apt.id);
    }
  }

  protected visibleDocs(apt: MyDocsAppointment): AppointmentDocumentDto[] {
    const q = this.query().trim().toLowerCase();
    const status = this.statusFilter();
    return apt.docs.filter((d) => {
      if (status && d.status !== status) return false;
      if (!q) return true;
      return [d.documentName, d.fileName, d.otherDocumentTypeName].some((s) =>
        (s ?? '').toLowerCase().includes(q),
      );
    });
  }

  protected statusLabel(s?: DocumentStatus): string {
    if (s === DocumentStatus.Accepted) return 'Accepted';
    if (s === DocumentStatus.Rejected) return 'Rejected';
    return 'Pending review';
  }

  protected download(apt: MyDocsAppointment, doc: AppointmentDocumentDto): void {
    if (!doc.id) return;
    this.http
      .get(this.urls.build(apt.id, doc.id), { responseType: 'blob', observe: 'response' })
      .subscribe({
        next: (resp) => {
          const objectUrl = URL.createObjectURL(resp.body as Blob);
          const a = document.createElement('a');
          a.href = objectUrl;
          a.download = doc.fileName ?? 'document';
          document.body.appendChild(a);
          a.click();
          a.remove();
          setTimeout(() => URL.revokeObjectURL(objectUrl), 0);
        },
        error: () => this.toaster.error('Could not download document.'),
      });
  }

  protected openAppointment(id: string): void {
    void this.router.navigate(['/appointments/view', id]);
  }
  protected goHome(): void {
    void this.router.navigateByUrl('/');
  }
  protected openProfile(): void {
    void this.router.navigateByUrl('/user-management/patients/my-profile');
  }
  protected signOut(): void {
    void performFullLogout(this.injector);
  }

  private loadDocs(id: string): void {
    this.patch(id, { state: 'loading' });
    this.documentService.getList(id).subscribe({
      next: (docs) => this.patch(id, { state: 'loaded', docs: docs ?? [] }),
      error: () => this.patch(id, { state: 'error', docs: [] }),
    });
  }

  private patch(id: string, change: Partial<MyDocsAppointment>): void {
    this.appointments.update((list) => list.map((a) => (a.id === id ? { ...a, ...change } : a)));
  }
}
