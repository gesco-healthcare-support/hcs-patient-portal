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
import { IconName } from '../../shared/ui/icon/icon.registry';
import { SkeletonComponent } from '../../shared/ui/skeleton/skeleton.component';
import { makeComparator, type SortModel, type SortValue } from '../../shared/sort/sort-state';
import { PacificDatePipe } from '../../shared/pipes/pacific-date.pipe';
import {
  AvailabilityCalendarComponent,
  type AvailabilitySelection,
} from '../availability-calendar/availability-calendar.component';
import {
  canConfirmDate,
  canFinalizeReschedule,
  consentStatusLabel,
  formatSlotLabel,
  requiresAdminReason,
  rescheduleStage,
  // Aliased: the component exposes same-named template helpers that delegate here.
  rowActionLabel as rowActionLabelFor,
  rowActionIsFinal as rowActionIsFinalFor,
  type ConsentRoundStage,
} from './cr-approve.util';
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
  imports: [
    PacificDatePipe,
    CommonModule,
    FormsModule,
    IconComponent,
    SkeletonComponent,
    AvailabilityCalendarComponent,
  ],
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

  // Phase 4b (2026-08-04): staff choose the reschedule date HERE. Phase 4c (2026-08-05) split
  // the act of choosing from the act of committing: picking has NO server effect, and the
  // Confirm button is what opens a consent round and emails both sides. Adrian: "what if the
  // staff selects a date and then changes it immediately, in that case 2 different emails will
  // go out." chosenDate/-Time drive the calendar's display; chosenSlotId is what is submitted.
  protected readonly chosenSlotId = signal<string | null>(null);
  protected readonly chosenDate = signal<string | null>(null);
  protected readonly chosenTime = signal<string | null>(null);
  protected readonly adminReason = signal('');

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

  private load(afterLoad?: () => void): void {
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
        afterLoad?.();
      },
      error: () => {
        this.rows.set([]);
        this.loading.set(false);
        afterLoad?.();
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
    this.resetSlotChoice();
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
    this.resetSlotChoice();
  }

  // ---- reschedule date choice (phase 4b, 2026-08-04) ----

  /**
   * Human-readable summary of what the requestor asked for, or null when they asked for no
   * particular date -- the normal case since 4b, where staff choose it here. Reads the queue's
   * enriched requestedSlotDate / requestedSlotFromTime rather than the bare slot GUID the row
   * used to carry (which is why the panel previously told staff to go open the appointment).
   */
  protected requestedSlotLabel(row: AppointmentChangeRequestDto): string | null {
    return formatSlotLabel(row.requestedSlotDate, row.requestedSlotFromTime);
  }

  protected onSlotSelected(selection: AvailabilitySelection): void {
    this.chosenDate.set(selection.date);
    this.chosenTime.set(selection.time);
    this.chosenSlotId.set(selection.doctorAvailabilityId);
  }

  protected onDateCleared(): void {
    this.resetSlotChoice();
  }

  /** An admin reason is owed only when staff REPLACE a slot the requestor proposed. */
  protected needsAdminReason(row: AppointmentChangeRequestDto): boolean {
    return requiresAdminReason(row.newDoctorAvailabilityId, this.chosenSlotId());
  }

  // ---- consent rounds (phase 4c, 2026-08-05) ----

  /**
   * Which of the three steps the reschedule modal is on. Derived from the row's current-round
   * fields rather than held as component state, so closing and reopening the modal -- or a full
   * reload -- lands on the step the SERVER believes it is on.
   */
  protected stage(row: AppointmentChangeRequestDto): ConsentRoundStage {
    return rescheduleStage(row);
  }

  /**
   * Item K (2026-08-22) -- what the row button actually does at this point in the flow.
   *
   * <p>It said "Approve" at every stage, but on a reschedule approving is step 3 of 3: the first
   * click opens a modal whose real job is to pick a date and email both sides for consent. Staff
   * reasonably read the label as "this approves the request", which it does not.</p>
   *
   * <p>Driven off the stage the component already computes from the row's own fields, so the label
   * survives a reload and always matches what the server believes. Cancellations have no consent
   * round, so they keep saying Approve -- for them it is accurate.</p>
   */
  protected rowActionLabel(row: AppointmentChangeRequestDto): string {
    return rowActionLabelFor(this.isReschedule(row), this.stage(row));
  }

  /**
   * Item K: only the genuine approve step gets the green, final-looking treatment. The earlier
   * stages open the same modal but are not approvals, so they should not look like one.
   */
  protected rowActionIsFinal(row: AppointmentChangeRequestDto): boolean {
    return rowActionIsFinalFor(this.isReschedule(row), this.stage(row));
  }

  /** Item K: matches the label, so the icon does not promise an approval either. */
  protected rowActionIcon(row: AppointmentChangeRequestDto): IconName {
    if (this.rowActionIsFinal(row)) {
      return 'check';
    }
    return this.stage(row) === 'needs-date' ? 'calendar' : 'clock';
  }

  /** Whether "Confirm date & request consent" may fire. */
  protected canConfirm(row: AppointmentChangeRequestDto): boolean {
    return canConfirmDate({
      slotId: this.chosenSlotId(),
      time: this.chosenTime(),
      proposedSlotId: row.newDoctorAvailabilityId,
      adminReason: this.adminReason(),
    });
  }

  /** Whether "Finalize reschedule" may fire. */
  protected canFinalize(row: AppointmentChangeRequestDto): boolean {
    return canFinalizeReschedule({ stage: this.stage(row), outcome: this.outcome() });
  }

  /** The date both sides are being asked to agree to, or null before anything is confirmed. */
  protected confirmedSlotLabel(row: AppointmentChangeRequestDto): string | null {
    return formatSlotLabel(row.currentRoundProposedDate, row.currentRoundProposedFromTime);
  }

  protected sideAConsentLabel(row: AppointmentChangeRequestDto): string {
    return consentStatusLabel(row.currentRoundSideAStatus);
  }

  protected sideBConsentLabel(row: AppointmentChangeRequestDto): string {
    return consentStatusLabel(row.currentRoundSideBStatus);
  }

  /**
   * Commits the picked date: opens a consent round and sends one email per side. Confirming the
   * SAME date again resends inside the current round; a different date supersedes it and opens
   * the next one. Both are decided server-side.
   */
  protected confirmDate(): void {
    const m = this.modal();
    if (!m || m.kind !== 'approve' || !m.row.id || this.isBusy() || !this.isReschedule(m.row)) {
      return;
    }
    if (!this.canConfirm(m.row)) {
      this.toaster.warn(
        this.needsAdminReason(m.row)
          ? 'Explain why you are changing the requested date before confirming.'
          : 'Choose the new appointment date and time before confirming.',
      );
      return;
    }

    this.isBusy.set(true);
    this.approvalService
      .confirmRescheduleDate(
        m.row.id,
        {
          doctorAvailabilityId: this.chosenSlotId()!,
          adminReScheduleReason: this.adminReason().trim() || null,
        },
        { skipHandleError: true },
      )
      .subscribe({
        next: () => this.onRoundChanged(m.row.id!, 'Consent request sent to both sides.'),
        error: (err) => this.handleRequestError(err),
      });
  }

  /** Re-asks whoever has not answered yet, without changing the date. */
  protected resendConsent(): void {
    const m = this.modal();
    if (!m || m.kind !== 'approve' || !m.row.id || this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.approvalService.resendConsentRequest(m.row.id, { skipHandleError: true }).subscribe({
      next: () => this.onRoundChanged(m.row.id!, 'Consent request sent again.'),
      error: (err) => this.handleRequestError(err),
    });
  }

  /**
   * Reloads after a confirm/resend and re-points the open modal at the refreshed row, so the
   * modal advances to the next stage in place. The request stays Pending through both actions,
   * so unlike approve/reject the row does NOT leave the queue.
   */
  private onRoundChanged(changeRequestId: string, message: string): void {
    this.isBusy.set(false);
    this.toaster.success(message);
    this.resetSlotChoice();
    this.reloadAndRefreshModal(changeRequestId);
  }

  private reloadAndRefreshModal(changeRequestId: string): void {
    // Re-point the modal from load()'s own completion, NOT from a microtask: load() is an HTTP
    // forkJoin, so a microtask runs long before the rows arrive and the modal would keep
    // rendering the pre-confirm row -- i.e. sit on "needs a date" forever after a confirm.
    this.load(() => {
      const refreshed = this.rows().find((r) => r.id === changeRequestId);
      const current = this.modal();
      if (refreshed && current?.kind === 'approve') {
        this.modal.set({ kind: 'approve', row: refreshed });
      }
    });
  }

  private resetSlotChoice(): void {
    this.chosenSlotId.set(null);
    this.chosenDate.set(null);
    this.chosenTime.set(null);
    this.adminReason.set('');
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
    // Phase 4c: same defense-in-depth, now on CONSENT rather than on the date. The server
    // rejects a finalize whose current round is missing or not fully agreed
    // (ChangeRequestConsentNotGranted), so never fire a doomed request.
    if (this.isReschedule(m.row) && !this.canFinalize(m.row)) {
      this.toaster.warn('Both sides must agree to the confirmed date before you can finalize.');
      return;
    }
    this.isBusy.set(true);
    // skipHandleError: surface failures as our own corrective toast (see
    // handleRequestError) instead of ABP's global blocking dialog, which left
    // the modal stuck behind it -- the dead-end staff hit on a consent block.
    const req$ = this.isReschedule(m.row)
      ? this.approvalService.approveReschedule(
          m.row.id,
          // Phase 4c: the date is no longer sent. It comes from the consent round both sides
          // agreed to, so finalize carries only the billing outcome -- there is no way for this
          // call to move the appointment to a date nobody consented to.
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
