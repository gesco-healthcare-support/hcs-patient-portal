import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ToasterService } from '@abp/ng.theme.shared';
import { forkJoin } from 'rxjs';
import { AppointmentChangeRequestApprovalService } from '../../proxy/appointment-change-requests/appointment-change-request-approval.service';
import type { AppointmentChangeRequestDto } from '../../proxy/appointment-change-requests/models';
import { ChangeRequestType } from '../../proxy/appointment-change-requests/change-request-type.enum';
import { AppointmentStatusType } from '../../proxy/enums/appointment-status-type.enum';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { makeComparator, type SortModel, type SortValue } from '../../shared/sort/sort-state';
import {
  changeRequestAgeClass,
  changeRequestAgeDays,
  changeRequestConsentView,
  consentBlockNote,
  consentBlocksApproval,
  requestingSideLabel,
  type CrConsentView,
} from './cr-inbox.util';

type CrTab = 'all' | 'reschedule' | 'cancel';
interface CrModal {
  kind: 'approve' | 'reject';
  row: AppointmentChangeRequestDto;
}

/**
 * Internal Workflow (Prompt 13) -- unified internal-staff change-request inbox
 * (supervisor + intake since QA #15 item 4, 2026-07-06).
 * Replaces the two legacy per-type Bootstrap tables (reschedules / cancellations)
 * with one tabbed inbox over the SAME approval engine
 * (AppointmentChangeRequestApprovalService): both queues load via getPending and
 * are filtered client-side by tab. Approve keeps the required NoBill/Late outcome
 * (the prototype's mock omitted it) and warns when an unresolved opposing-side
 * consent would be overridden; reject requires a reason. Age + consent are derived
 * client-side (cr-inbox.util). OnPush + signals.
 */
@Component({
  selector: 'app-internal-change-request-inbox',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, IconComponent, SkeletonComponent],
  templateUrl: './internal-change-request-inbox.component.html',
  styles: `
    /* QA #15 item 6: right-aligned Sort-by control in the chips row. */
    .cr-sortbar {
      margin-left: auto;
      display: inline-flex;
      align-items: center;
      gap: 8px;
      color: var(--n-500, #6b7789);
      font-size: 12.5px;
      font-weight: 600;
    }
    .cr-sortbar label {
      margin: 0;
    }
    .cr-sortbar select {
      border: 1px solid var(--border-strong, #d8deea);
      background: #fff;
      color: var(--n-700, #3b4554);
      border-radius: 9px;
      padding: 7px 10px;
      font: inherit;
      cursor: pointer;
    }
    .cr-sortbar select:focus {
      outline: none;
      border-color: var(--blue-400, #2f7cbf);
      box-shadow: 0 0 0 4px var(--blue-50, #eef5fb);
    }
    .cr-sortbar__dir {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 34px;
      height: 34px;
      border: 1px solid var(--border-strong, #d8deea);
      background: #fff;
      color: var(--n-600, #515c6e);
      border-radius: 9px;
      cursor: pointer;
      transition: all 0.14s;
    }
    .cr-sortbar__dir:hover:not(:disabled) {
      border-color: var(--blue-300, #6ea7d6);
      color: var(--blue-700, #055495);
    }
    .cr-sortbar__dir:disabled {
      opacity: 0.5;
      cursor: default;
    }
  `,
})
export class InternalChangeRequestInboxComponent implements OnInit {
  private readonly approvalService = inject(AppointmentChangeRequestApprovalService);
  private readonly router = inject(Router);
  private readonly toaster = inject(ToasterService);

  protected readonly rows = signal<AppointmentChangeRequestDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly tab = signal<CrTab>('all');
  protected readonly openId = signal<string | null>(null);
  protected readonly modal = signal<CrModal | null>(null);
  protected readonly outcome = signal<AppointmentStatusType | null>(null);
  protected readonly reason = signal('');
  protected readonly isBusy = signal(false);

  // Captured once per load so the age pills stay stable + the template is pure.
  private nowMs = 0;

  protected readonly counts = computed(() => {
    const all = this.rows();
    return {
      all: all.length,
      reschedule: all.filter((r) => r.changeRequestType === ChangeRequestType.Reschedule).length,
      cancel: all.filter((r) => r.changeRequestType === ChangeRequestType.Cancel).length,
    };
  });

  protected readonly visibleRows = computed(() => {
    const t = this.tab();
    const all = this.rows();
    if (t === 'all') {
      return all;
    }
    const want = t === 'reschedule' ? ChangeRequestType.Reschedule : ChangeRequestType.Cancel;
    return all.filter((r) => r.changeRequestType === want);
  });

  // QA #15 item 6 (2026-07-07): this queue is a card list, not a table, so column
  // sorting is offered as a "Sort by" control. Rows are already fully loaded, so
  // the comparator runs client-side; an unset key preserves the load order
  // (newest-first), which sort() keeps because Array.sort is stable.
  protected readonly sort = signal<SortModel>({ key: null, dir: 'asc' });
  protected readonly displayRows = computed(() =>
    [...this.visibleRows()].sort(
      makeComparator<AppointmentChangeRequestDto>(this.sort(), (row, key) =>
        this.sortValue(row, key),
      ),
    ),
  );

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.nowMs = Date.now();
    forkJoin({
      resched: this.approvalService.getPending({
        changeRequestType: ChangeRequestType.Reschedule,
        skipCount: 0,
        maxResultCount: 100,
      }),
      cancel: this.approvalService.getPending({
        changeRequestType: ChangeRequestType.Cancel,
        skipCount: 0,
        maxResultCount: 100,
      }),
    }).subscribe({
      next: ({ resched, cancel }) => {
        const merged = [...(resched.items ?? []), ...(cancel.items ?? [])].sort(
          (a, b) => this.creationMs(b) - this.creationMs(a),
        );
        this.rows.set(merged);
        this.loading.set(false);
      },
      error: () => {
        this.rows.set([]);
        this.loading.set(false);
      },
    });
  }

  // ---- row presentation ----
  private creationMs(row: AppointmentChangeRequestDto): number {
    return row.creationTime ? new Date(row.creationTime).getTime() : 0;
  }
  protected isReschedule(row: AppointmentChangeRequestDto): boolean {
    return row.changeRequestType === ChangeRequestType.Reschedule;
  }
  protected typeLabel(row: AppointmentChangeRequestDto): string {
    return this.isReschedule(row) ? 'Reschedule' : 'Cancellation';
  }
  protected ageDays(row: AppointmentChangeRequestDto): number {
    return changeRequestAgeDays(row.creationTime, this.nowMs);
  }
  protected ageClass(row: AppointmentChangeRequestDto): string {
    return changeRequestAgeClass(this.ageDays(row));
  }
  protected consent(row: AppointmentChangeRequestDto): CrConsentView {
    return changeRequestConsentView(row.sideAConsentStatus, row.sideBConsentStatus);
  }
  protected sideLabel(row: AppointmentChangeRequestDto): string {
    return requestingSideLabel(row.requestingSide);
  }
  protected reasonOf(row: AppointmentChangeRequestDto): string {
    return (this.isReschedule(row) ? row.reScheduleReason : row.cancellationReason) ?? '';
  }

  // ---- sorting (client-side; card list, so exposed as a Sort-by control) ----
  private sortValue(row: AppointmentChangeRequestDto, key: string): SortValue {
    switch (key) {
      case 'requested':
        return this.creationMs(row);
      case 'type':
        return this.typeLabel(row);
      case 'appt':
        return row.appointmentConfirmationNumber ?? '';
      case 'age':
        return this.ageDays(row);
      default:
        return null;
    }
  }
  protected setSortKey(key: string): void {
    this.sort.set({ key: key || null, dir: 'asc' });
  }
  protected toggleSortDir(): void {
    const current = this.sort();
    if (!current.key) {
      return;
    }
    this.sort.set({ key: current.key, dir: current.dir === 'asc' ? 'desc' : 'asc' });
  }

  protected toggle(row: AppointmentChangeRequestDto): void {
    this.openId.set(this.openId() === row.id ? null : (row.id ?? null));
  }

  protected view(row: AppointmentChangeRequestDto): void {
    if (row.appointmentId) {
      void this.router.navigateByUrl(`/appointments/view/${row.appointmentId}`);
    }
  }

  // ---- modals ----
  protected openApprove(row: AppointmentChangeRequestDto): void {
    this.outcome.set(
      this.isReschedule(row)
        ? AppointmentStatusType.RescheduledNoBill
        : AppointmentStatusType.CancelledNoBill,
    );
    this.modal.set({ kind: 'approve', row });
  }
  protected openReject(row: AppointmentChangeRequestDto): void {
    this.reason.set('');
    this.modal.set({ kind: 'reject', row });
  }
  protected closeModal(): void {
    if (this.isBusy()) {
      return;
    }
    this.modal.set(null);
    this.reason.set('');
  }

  protected outcomeOptions(
    row: AppointmentChangeRequestDto,
  ): { value: AppointmentStatusType; label: string }[] {
    return this.isReschedule(row)
      ? [
          { value: AppointmentStatusType.RescheduledNoBill, label: 'No bill' },
          { value: AppointmentStatusType.RescheduledLate, label: 'Late' },
        ]
      : [
          { value: AppointmentStatusType.CancelledNoBill, label: 'No bill' },
          { value: AppointmentStatusType.CancelledLate, label: 'Late' },
        ];
  }

  /** Corrective note in the approve modal when consent blocks approval (null = approvable). */
  protected consentNote(row: AppointmentChangeRequestDto): string | null {
    return consentBlockNote(row.sideAConsentStatus, row.sideBConsentStatus);
  }

  /** True when the row's consent state blocks approval; the server forbids it (no override). */
  protected approveBlocked(row: AppointmentChangeRequestDto): boolean {
    return consentBlocksApproval(row.sideAConsentStatus, row.sideBConsentStatus);
  }

  protected confirmApprove(): void {
    const m = this.modal();
    const out = this.outcome();
    if (!m || m.kind !== 'approve' || !m.row.id || out === null || this.isBusy()) {
      return;
    }
    // Defense-in-depth: the Approve button is disabled when consent blocks
    // approval, but guard here too so a stale click never fires a doomed request.
    if (this.approveBlocked(m.row)) {
      this.toaster.warn(
        consentBlockNote(m.row.sideAConsentStatus, m.row.sideBConsentStatus) ??
          'This request cannot be approved yet.',
      );
      return;
    }
    this.isBusy.set(true);
    // skipHandleError: surface failures as our own corrective toast (see
    // handleRequestError) instead of ABP's global blocking dialog, which left
    // the modal stuck behind it -- the dead-end staff hit on a consent block.
    const req$ = this.isReschedule(m.row)
      ? this.approvalService.approveReschedule(
          m.row.id,
          { rescheduleOutcome: out },
          { skipHandleError: true },
        )
      : this.approvalService.approveCancellation(
          m.row.id,
          { cancellationOutcome: out },
          { skipHandleError: true },
        );
    req$.subscribe({
      next: () => this.onHandled(m, 'approved'),
      error: (err) => this.handleRequestError(err),
    });
  }

  protected confirmReject(): void {
    const m = this.modal();
    const text = this.reason().trim();
    if (!m || m.kind !== 'reject' || !m.row.id || !text || this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    const req$ = this.isReschedule(m.row)
      ? this.approvalService.rejectReschedule(m.row.id, { reason: text }, { skipHandleError: true })
      : this.approvalService.rejectCancellation(
          m.row.id,
          { reason: text },
          { skipHandleError: true },
        );
    req$.subscribe({
      next: () => this.onHandled(m, 'rejected'),
      error: (err) => this.handleRequestError(err),
    });
  }

  private onHandled(m: CrModal, verb: string): void {
    this.isBusy.set(false);
    this.modal.set(null);
    this.reason.set('');
    this.toaster.success(`${this.typeLabel(m.row)} request ${verb}.`);
    // Drop the handled row immediately, then refresh from the server.
    this.rows.set(this.rows().filter((r) => r.id !== m.row.id));
    this.load();
  }

  /**
   * Show a failed approve/reject as a dismissible corrective toast and close the
   * modal so the user is never stuck on an error page. With skipHandleError on
   * the call, ABP's global blocking dialog/page is bypassed; we surface the
   * server's message (e.g. the consent-block message) when present, else a safe
   * fallback.
   */
  private handleRequestError(err: unknown): void {
    this.isBusy.set(false);
    this.modal.set(null);
    this.reason.set('');
    const message =
      (err as { error?: { error?: { message?: string } } })?.error?.error?.message ??
      'Could not complete the request. Please try again, or contact your administrator if it persists.';
    this.toaster.error(message);
  }
}
