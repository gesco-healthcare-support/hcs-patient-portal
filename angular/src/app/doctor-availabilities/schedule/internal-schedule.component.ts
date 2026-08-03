import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  ViewEncapsulation,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { FullCalendarModule } from '@fullcalendar/angular';
import type { CalendarOptions, DatesSetInfo, EventClickInfo, EventInput } from 'fullcalendar';
import timeGridPlugin from 'fullcalendar/timegrid';
import classicThemePlugin from 'fullcalendar/themes/classic';
import { DoctorAvailabilityService } from '../../proxy/doctor-availabilities/doctor-availability.service';
import type { ScheduleSlotDto } from '../../proxy/doctor-availabilities/models';
import { isoDate } from '../doctor-availability/avail-grid.util';
import { toAppointmentEvents, toBackgroundEvents } from './schedule-calendar.util';

interface LocationOption {
  id: string;
  name: string;
}

/** Inclusive date range, matching GetScheduleInput's server-side semantics. */
interface DateRange {
  from: string;
  to: string;
}

/**
 * Phase 3 (2026-07-31) -- the staff Schedule screen: a week/day time grid of a
 * clinic's slots, each appointment a clickable chip at its real time.
 *
 * Distinct from the sibling availabilities GRID, which manages slots (generate,
 * close, delete). This screen only READS, and it colours slots from real
 * occupancy rather than BookingStatusId.
 *
 * A location must be chosen -- it defaults to the first clinic rather than "All
 * locations" -- because the chips carry patient names and the PHI on screen
 * should stay proportionate to the task.
 *
 * ViewEncapsulation.None is REQUIRED: FullCalendar builds its DOM imperatively,
 * so those elements never receive Angular's scoping attribute and emulated
 * styles would not reach them. Every rule this component owns is therefore
 * nested under `.sched-page` to keep it from leaking.
 */
@Component({
  selector: 'app-internal-schedule',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  imports: [CommonModule, FormsModule, FullCalendarModule],
  templateUrl: './internal-schedule.component.html',
  styleUrls: ['./internal-schedule.component.scss'],
})
export class InternalScheduleComponent implements OnInit {
  private readonly service = inject(DoctorAvailabilityService);
  private readonly router = inject(Router);

  protected readonly loading = signal(false);
  protected readonly locations = signal<LocationOption[]>([]);
  protected readonly locationId = signal<string>('');
  protected readonly slots = signal<ScheduleSlotDto[]>([]);
  protected readonly loadFailed = signal(false);

  /** Visible range, published by FullCalendar's datesSet as the user navigates. */
  private readonly range = signal<DateRange | null>(null);

  protected readonly events = computed<EventInput[]>(() => {
    const slots = this.slots();
    // Backgrounds first so the chips paint on top of their slot band.
    return [...toBackgroundEvents(slots), ...toAppointmentEvents(slots)];
  });

  protected readonly hasLocations = computed(() => this.locations().length > 0);

  /**
   * Built once and never replaced: FullCalendar deep-checks this input, so a new
   * object each cycle would churn the calendar. Only `events` is data-bound.
   */
  protected readonly calendarOptions: CalendarOptions = {
    plugins: [timeGridPlugin, classicThemePlugin],
    initialView: 'timeGridWeek',
    headerToolbar: {
      left: 'prev,next today',
      center: 'title',
      right: 'timeGridWeek,timeGridDay',
    },
    // Slots are clinic-local wall-clock times, so no all-day lane and no
    // slotMinTime/slotMaxTime: clipping the day could HIDE a real slot, which
    // for a staff schedule is a correctness bug, not a cosmetic one.
    allDaySlot: false,
    nowIndicator: true,
    scrollTime: '07:00:00',
    expandRows: true,
    height: 'auto',
    eventDisplay: 'block',
    displayEventEnd: false,
    datesSet: (info: DatesSetInfo) => this.onDatesSet(info),
    eventClick: (info: EventClickInfo) => this.onEventClick(info),
  };

  constructor() {
    // Reload whenever the visible range or the chosen clinic changes. An effect
    // (rather than a call inside datesSet) keeps the fetch out of FullCalendar's
    // render pass.
    effect(() => {
      const range = this.range();
      const locationId = this.locationId();
      if (!range || !locationId) {
        return;
      }
      this.load(locationId, range);
    });
  }

  ngOnInit(): void {
    this.service.getLocationLookup({ maxResultCount: 100, skipCount: 0, filter: '' }).subscribe({
      next: (res) => {
        const options = (res.items ?? []).map((l) => ({
          id: l.id ?? '',
          name: l.displayName ?? '',
        }));
        this.locations.set(options);
        // Default to the FIRST clinic, not "All locations": the endpoint requires
        // a location and this screen shows patient names.
        if (options.length > 0) {
          this.locationId.set(options[0].id);
        }
      },
      error: () => this.locations.set([]),
    });
  }

  private load(locationId: string, range: DateRange): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.service
      .getSchedule({ locationId, fromDate: range.from, toDate: range.to })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (slots) => this.slots.set(slots ?? []),
        error: () => {
          this.slots.set([]);
          this.loadFailed.set(true);
        },
      });
  }

  /**
   * FullCalendar reports the visible window with an EXCLUSIVE end, while
   * GetScheduleInput treats both bounds as inclusive -- so the last day is
   * `end - 1`. Getting this wrong pulls in an extra day of slots.
   */
  private onDatesSet(info: DatesSetInfo): void {
    const lastDay = new Date(info.end);
    lastDay.setDate(lastDay.getDate() - 1);
    const next: DateRange = {
      from: `${isoDate(info.start)}T00:00:00`,
      to: `${isoDate(lastDay)}T00:00:00`,
    };
    const current = this.range();
    if (current && current.from === next.from && current.to === next.to) {
      return;
    }
    this.range.set(next);
  }

  /**
   * Chips route to the appointment; slot backgrounds are inert. FullCalendar does
   * not fire clicks for background events, so the kind check is a guard rather
   * than the primary mechanism.
   */
  private onEventClick(info: EventClickInfo): void {
    const props = info.event.extendedProps as { kind?: string; appointmentId?: string };
    if (props?.kind !== 'appointment') {
      return;
    }
    const appointmentId = props.appointmentId ?? info.event.id;
    if (appointmentId) {
      void this.router.navigateByUrl(`/appointments/view/${appointmentId}`);
    }
  }

  protected onLocationChange(id: string): void {
    this.locationId.set(id);
  }
}
