import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  inject,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgbDatepickerModule, NgbDateStruct } from '@ng-bootstrap/ng-bootstrap';
import { firstValueFrom } from 'rxjs';
import { DoctorAvailabilityService } from '../../proxy/doctor-availabilities/doctor-availability.service';
import {
  buildAvailableDateKeys,
  isSelectableDate,
  toDateKey,
  toDateKeyFromApi,
} from './availability-rules';

/** One selectable time within a date, and the slot that backs it. */
export interface AvailabilityTimeOption {
  value: string;
  label: string;
  doctorAvailabilityId: string;
}

/** What the calendar emits when the user settles on a date and time. */
export interface AvailabilitySelection {
  /** `YYYY-MM-DD`, or null when the selection was cleared. */
  date: string | null;
  time: string | null;
  doctorAvailabilityId: string | null;
}

/**
 * Date + time availability picker, extracted from `AppointmentAddComponent` in phase 4a
 * (2026-08-03) so booking, the booking wizard and the reschedule flow (4b) share one implementation
 * of the rules instead of drifting apart.
 *
 * <p>CONTROLLED, not form-bound: it takes `selectedDate` / `selectedTime` and emits
 * {@link AvailabilitySelection}. It deliberately owns no `FormGroup`, because the reschedule modal
 * does not share booking's form -- the parent adapts the output onto whatever it uses.</p>
 *
 * <p>DELIBERATELY ROLE-AGNOSTIC. The 60-day external horizon is NOT applied here: between 60 and 90
 * days external users must still SEE those dates and be intercepted with a contact-staff notice on
 * SELECTION. That interception is role-aware UX belonging to the booking context, so the parent
 * applies it when handling `slotSelected`. This component caps only the shared absolute ceiling.</p>
 */
@Component({
  selector: 'app-availability-calendar',
  standalone: true,
  imports: [CommonModule, FormsModule, NgbDatepickerModule],
  templateUrl: './availability-calendar.component.html',
  styleUrls: ['./availability-calendar.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AvailabilityCalendarComponent implements OnChanges {
  private readonly doctorAvailabilityService = inject(DoctorAvailabilityService);

  @Input() locationId: string | null = null;
  @Input() appointmentTypeId: string | null = null;

  /** False before an appointment type is chosen, when the picker greys nothing out. */
  @Input() typeChosen = false;

  /** Client-side mirror of the tenant lead time. The server remains authoritative. */
  @Input() leadDays = 3;

  /** The shared absolute ceiling, applied to every role. */
  @Input() ceilingDays = 90;

  /** `YYYY-MM-DD`. Drives which day renders as selected and which times are offered. */
  @Input() selectedDate: string | null = null;
  @Input() selectedTime: string | null = null;

  @Input() dateInvalid = false;
  @Input() timeInvalid = false;
  @Input() minimumBookingRuleMessage = '';
  @Input() noBookableDatesMessage = '';

  // Copy comes in as inputs rather than being localized here on purpose: a presentational calendar
  // should not own wording, and taking the strings keeps this component free of ABP's localization
  // DI chain so it can be dropped into the reschedule modal (4b) as-is.
  @Input() dateLabel = 'Appointment Date';
  @Input() timeLabel = 'Appointment Time';
  @Input() noSlotsMessage = '';

  @Output() slotSelected = new EventEmitter<AvailabilitySelection>();
  @Output() dateCleared = new EventEmitter<void>();

  protected isLoading = false;
  protected timeOptions: AvailabilityTimeOption[] = [];

  private availableDateKeys = new Set<string>();
  private availableSlotsByDate = new Map<
    string,
    Array<{ time: string; doctorAvailabilityId: string }>
  >();

  /**
   * Guards against a stale response being applied. Without it, switching location or type quickly
   * can land the PREVIOUS selection's slots on the new one -- i.e. offer the wrong office's times.
   */
  private requestVersion = 0;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['locationId'] || changes['appointmentTypeId']) {
      void this.reload();
      return;
    }
    if (changes['selectedDate'] && this.selectedDate) {
      this.populateTimesFor(this.selectedDate);
    }
  }

  /** ngbDatepicker's `[markDisabled]`: true DISABLES the day, so this is the inverse of selectable. */
  protected readonly markDateDisabled = (date: NgbDateStruct): boolean =>
    !isSelectableDate(date, {
      typeChosen: this.typeChosen,
      leadDays: this.leadDays,
      ceilingDays: this.ceilingDays,
      availableKeys: this.availableDateKeys,
    });

  /** Highlights days that actually have availability. */
  protected readonly isAvailableDate = (date: NgbDateStruct): boolean =>
    this.availableDateKeys.has(toDateKey(date.year, date.month, date.day));

  protected get selectedDateStruct(): NgbDateStruct | null {
    if (!this.selectedDate) return null;
    const [year, month, day] = this.selectedDate.split('-').map(Number);
    return year && month && day ? { year, month, day } : null;
  }

  protected onDatePicked(date: NgbDateStruct): void {
    const key = toDateKey(date.year, date.month, date.day);
    this.populateTimesFor(key);

    // Emit the date immediately with no time: the parent applies its role-horizon interception
    // before committing, and the user then picks a time.
    this.slotSelected.emit({ date: key, time: null, doctorAvailabilityId: null });
  }

  protected onTimePicked(value: string | null): void {
    if (!value) {
      this.slotSelected.emit({ date: this.selectedDate, time: null, doctorAvailabilityId: null });
      return;
    }
    const match = this.timeOptions.find((option) => option.value === value);
    this.slotSelected.emit({
      date: this.selectedDate,
      time: value,
      doctorAvailabilityId: match?.doctorAvailabilityId ?? null,
    });
  }

  protected onClear(): void {
    this.timeOptions = [];
    this.dateCleared.emit();
  }

  private async reload(): Promise<void> {
    if (!this.locationId) {
      this.availableDateKeys = new Set<string>();
      this.availableSlotsByDate = new Map();
      this.timeOptions = [];
      return;
    }

    const version = ++this.requestVersion;
    this.isLoading = true;

    try {
      const items = await firstValueFrom(
        this.doctorAvailabilityService.getDoctorAvailabilityLookup({
          locationId: this.locationId,
          appointmentTypeId: this.appointmentTypeId || null,
        }),
      );

      if (version !== this.requestVersion) {
        return;
      }

      const usable = (items ?? []).filter((item) => !!toDateKeyFromApi(item?.availableDate));
      this.availableDateKeys = buildAvailableDateKeys(usable.map((item) => item?.availableDate));
      this.availableSlotsByDate = new Map();

      for (const item of usable) {
        const dateKey = toDateKeyFromApi(item?.availableDate);
        const fromTime = (item?.fromTime as string | undefined) ?? '';
        const availabilityId = (item?.id as string | undefined) ?? '';
        if (!dateKey || !fromTime) continue;

        const slots = this.availableSlotsByDate.get(dateKey) ?? [];
        const exists = slots.some(
          (slot) => slot.time === fromTime && slot.doctorAvailabilityId === availabilityId,
        );
        if (!exists) {
          slots.push({ time: fromTime, doctorAvailabilityId: availabilityId });
          this.availableSlotsByDate.set(dateKey, slots);
        }
      }

      // A previously chosen date that is no longer available must not stay selected.
      if (this.selectedDate && !this.availableDateKeys.has(this.selectedDate)) {
        this.timeOptions = [];
        this.slotSelected.emit({ date: null, time: null, doctorAvailabilityId: null });
        return;
      }
      if (this.selectedDate) {
        this.populateTimesFor(this.selectedDate);
      }
    } finally {
      if (version === this.requestVersion) {
        this.isLoading = false;
      }
    }
  }

  private populateTimesFor(dateKey: string): void {
    const slots = (this.availableSlotsByDate.get(dateKey) ?? [])
      .slice()
      .sort((a, b) => a.time.localeCompare(b.time));

    this.timeOptions = slots.map((slot) => ({
      value: slot.time,
      label: AvailabilityCalendarComponent.toTimeLabel(slot.time),
      doctorAvailabilityId: slot.doctorAvailabilityId,
    }));
  }

  /** `HH:mm[:ss]` -> a 12-hour label, matching what the booking form showed before. */
  private static toTimeLabel(time: string): string {
    const [rawHour, rawMinute] = time.split(':');
    const hour = Number(rawHour);
    if (Number.isNaN(hour)) return time;
    const suffix = hour < 12 ? 'AM' : 'PM';
    const display = hour % 12 === 0 ? 12 : hour % 12;
    return `${display}:${(rawMinute ?? '00').padStart(2, '0')} ${suffix}`;
  }
}
