import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  OnInit,
  Output,
  SimpleChanges,
  inject,
  signal,
} from '@angular/core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { firstValueFrom } from 'rxjs';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { CalendarDatePipe } from '../../shared/pipes/pacific-date.pipe';
import type { PartyConsentRequestDto } from '../../proxy/appointment-change-requests/models';
import { PartyChangeRequestConsentService } from '../../proxy/appointment-change-requests/party-change-request-consent.service';
import {
  partyConsentConfirmText,
  partyConsentHeadline,
  partyConsentSubject,
} from './party-consent.util';

/**
 * 2026-10-10 -- "Your agreement is needed": the change requests waiting for the signed-in
 * party's answer, with Agree / Decline, so a party who has an account does not have to find the
 * consent email. On the external appointment page (one appointment) and the external home page
 * (all of them). Renders nothing when nothing is waiting, so it can sit on any page safely.
 *
 * The server decides who may answer; this card only shows what it is given. Emits
 * {@link decided} after an answer so the host page can refresh the appointment, which may have
 * moved or been cancelled as a result.
 */
@Component({
  selector: 'app-party-consent-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [IconComponent, CalendarDatePipe],
  templateUrl: './party-consent-card.component.html',
  styles: `
    .pc-card {
      border: 1px solid var(--st-pending-border, #f0c36d);
      background: var(--st-pending-bg, #fff8e6);
      border-radius: var(--r-md, 11px);
      padding: 14px 16px;
      margin-bottom: 16px;
    }
    .pc-item + .pc-item {
      margin-top: 12px;
      padding-top: 12px;
      border-top: 1px solid var(--border, #e6ebf2);
    }
    .pc-title {
      font-weight: 700;
      margin: 0 0 4px;
    }
    .pc-meta {
      font-size: 13px;
      color: var(--n-600, #4b5565);
    }
    .pc-actions {
      display: flex;
      gap: 10px;
      margin-top: 10px;
      flex-wrap: wrap;
    }
  `,
})
export class PartyConsentCardComponent implements OnInit, OnChanges {
  private readonly service = inject(PartyChangeRequestConsentService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);

  /** One appointment's requests; omit to list every request waiting for the party. */
  @Input() appointmentId: string | null = null;

  /** Show which appointment each request is about (the home page lists several). */
  @Input() showSubject = false;

  @Output() readonly decided = new EventEmitter<PartyConsentRequestDto>();

  protected readonly items = signal<PartyConsentRequestDto[]>([]);
  protected readonly busyId = signal<string | null>(null);

  protected readonly headline = partyConsentHeadline;
  protected readonly subject = partyConsentSubject;

  ngOnInit(): void {
    this.load();
  }

  /** A host that switches appointment in place gets that appointment's requests. */
  ngOnChanges(changes: SimpleChanges): void {
    const appointment = changes['appointmentId'];
    if (appointment && !appointment.firstChange) {
      this.load();
    }
  }

  load(): void {
    // An empty id is dropped from the query (RestService skips empty values): every request.
    this.service.getMyPending(this.appointmentId ?? '').subscribe({
      next: (result) => this.items.set(result?.items ?? []),
      error: () => this.items.set([]),
    });
  }

  protected async answer(item: PartyConsentRequestDto, approved: boolean): Promise<void> {
    const id = item.changeRequestId;
    if (this.busyId() || !id) {
      return;
    }
    const status = await firstValueFrom(
      this.confirmation.warn(
        partyConsentConfirmText(item, approved),
        approved ? 'Agree' : 'Decline',
      ),
    );
    if (status !== Confirmation.Status.confirm) {
      return;
    }

    this.busyId.set(id);
    try {
      const result = await firstValueFrom(this.service.submitMyDecision(id, { approved }));
      this.items.update((list) => list.filter((i) => i.changeRequestId !== id));
      this.toaster.success(approved ? 'Your agreement was recorded.' : 'Your answer was recorded.');
      this.decided.emit(result);
    } catch {
      // ABP's error handler has already shown the server's message (for example, the other
      // side answered first). Reload so the card reflects what is actually still waiting.
      this.load();
    } finally {
      this.busyId.set(null);
    }
  }
}
