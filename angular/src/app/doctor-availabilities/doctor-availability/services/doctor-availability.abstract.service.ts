import { inject } from '@angular/core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { ABP, ListService, PagedResultDto } from '@abp/ng.core';
import { filter, switchMap } from 'rxjs/operators';
import type {
  GetDoctorAvailabilitiesInput,
  DoctorAvailabilityWithNavigationPropertiesDto,
} from '../../../proxy/doctor-availabilities/models';
import { DoctorAvailabilityService } from '../../../proxy/doctor-availabilities/doctor-availability.service';

export interface DoctorAvailabilityGroupedRow {
  id: string;
  locationId: string;
  locationName: string;
  availableDate: string;
  minFromTime: string;
  maxToTime: string;
  availableCount: number;
  bookedCount: number;
  reservedCount: number;
  totalCount: number;
  slots: DoctorAvailabilityWithNavigationPropertiesDto[];
}

export abstract class AbstractDoctorAvailabilityViewService {
  protected readonly proxyService = inject(DoctorAvailabilityService);
  protected readonly confirmationService = inject(ConfirmationService);
  protected readonly list = inject(ListService);

  public readonly getWithNavigationProperties = this.proxyService.getWithNavigationProperties;

  data: PagedResultDto<DoctorAvailabilityWithNavigationPropertiesDto> = {
    items: [],
    totalCount: 0,
  };

  groupedData: DoctorAvailabilityGroupedRow[] = [];

  filters = {} as GetDoctorAvailabilitiesInput;

  deleteGroup(row: DoctorAvailabilityGroupedRow) {
    this.confirmationService
      .warn('::DeleteConfirmationMessage', '::AreYouSure', { messageLocalizationParams: [] })
      .pipe(
        filter(status => status === Confirmation.Status.confirm),
        switchMap(() =>
          this.proxyService.deleteByDate({
            locationId: row.locationId,
            availableDate: row.availableDate,
          }),
        ),
      )
      .subscribe(this.list.get);
  }

  deleteSlot(record: DoctorAvailabilityWithNavigationPropertiesDto) {
    const availability = record.doctorAvailability;
    if (!availability) {
      return;
    }

    this.confirmationService
      .warn('::DeleteConfirmationMessage', '::AreYouSure', { messageLocalizationParams: [] })
      .pipe(
        filter(status => status === Confirmation.Status.confirm),
        switchMap(() =>
          this.proxyService.deleteBySlot({
            locationId: availability.locationId ?? '',
            availableDate: availability.availableDate ?? '',
            fromTime: availability.fromTime ?? '',
            toTime: availability.toTime ?? '',
          }),
        ),
      )
      .subscribe(this.list.get);
  }

  hookToQuery() {
    const getData = (query: ABP.PageQueryParams) =>
      this.proxyService.getList({
        ...query,
        maxResultCount: Math.max(query.maxResultCount ?? 0, 1000),
        ...this.filters,
        filterText: query.filter,
      });

    const setData = (list: PagedResultDto<DoctorAvailabilityWithNavigationPropertiesDto>) => {
      this.data = list;
      this.groupedData = this.buildGroupedData(list.items ?? []);
    };

    this.list.hookToQuery(getData).subscribe(setData);
  }

  clearFilters() {
    this.filters = {} as GetDoctorAvailabilitiesInput;
    this.list.get();
  }

  private buildGroupedData(
    items: DoctorAvailabilityWithNavigationPropertiesDto[],
  ): DoctorAvailabilityGroupedRow[] {
    const groups = new Map<string, DoctorAvailabilityGroupedRow>();

    for (const item of items) {
      const availability = item.doctorAvailability;
      if (!availability) {
        continue;
      }

      const availableDate = availability.availableDate ?? '';
      const locationName = item.location?.name ?? '';
      const locationId = availability.locationId ?? 'unknown';
      const key = `${locationId}-${availableDate}`;

      if (!groups.has(key)) {
        groups.set(key, {
          id: key,
          locationId,
          locationName,
          availableDate,
          minFromTime: availability.fromTime ?? '',
          maxToTime: availability.toTime ?? '',
          availableCount: 0,
          bookedCount: 0,
          reservedCount: 0,
          totalCount: 0,
          slots: [],
        });
      }

      const group = groups.get(key);
      group.slots.push(item);
      group.totalCount += 1;

      if (availability.bookingStatusId === 8) {
        group.availableCount += 1;
      } else if (availability.bookingStatusId === 9) {
        group.bookedCount += 1;
      } else if (availability.bookingStatusId === 10) {
        group.reservedCount += 1;
      }

      if (availability.fromTime && this.toMinutes(availability.fromTime) < this.toMinutes(group.minFromTime)) {
        group.minFromTime = availability.fromTime;
      }

      if (availability.toTime && this.toMinutes(availability.toTime) > this.toMinutes(group.maxToTime)) {
        group.maxToTime = availability.toTime;
      }
    }

    return Array.from(groups.values()).sort((a, b) => a.availableDate.localeCompare(b.availableDate));
  }

  private toMinutes(value: string | null | undefined): number {
    if (!value) {
      return 0;
    }

    const parts = value.split(':').map(part => Number(part));
    const hours = parts[0] ?? 0;
    const minutes = parts[1] ?? 0;
    return hours * 60 + minutes;
  }
}
