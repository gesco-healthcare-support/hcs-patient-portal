import { Directive, OnDestroy, OnInit, inject } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { ListService } from '@abp/ng.core';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { bookingStatusOptions } from '../../../proxy/enums/booking-status.enum';
import type { DoctorAvailabilityWithNavigationPropertiesDto } from '../../../proxy/doctor-availabilities/models';
import type { DoctorAvailabilityGroupedRow } from '../services/doctor-availability.abstract.service';
import { DoctorAvailabilityViewService } from '../services/doctor-availability.service';
import { DoctorAvailabilityDetailViewService } from '../services/doctor-availability-detail.service';

export const ChildTabDependencies = [];
export const ChildComponentDependencies = [];

@Directive()
export abstract class AbstractDoctorAvailabilityComponent implements OnInit, OnDestroy {
  public readonly list = inject(ListService);
  public readonly service = inject(DoctorAvailabilityViewService);
  public readonly serviceDetail = inject(DoctorAvailabilityDetailViewService);
  private readonly router = inject(Router);
  private readonly subscriptions = new Subscription();

  bookingStatusOptions = bookingStatusOptions;

  protected title = '::DoctorAvailabilities';
  private readonly expandedRows = new Set<string>();
  page = 1;
  pageSize = 10;
  pageSizeOptions = [10, 25, 50, 100];

  ngOnInit() {
    this.service.hookToQuery();
    this.subscriptions.add(
      this.router.events
        .pipe(filter(event => event instanceof NavigationEnd))
        .subscribe(event => {
          const navigation = event as NavigationEnd;
          if (navigation.urlAfterRedirects.startsWith('/doctor-management/doctor-availabilities')) {
            this.list.get();
          }
        }),
    );
  }

  ngOnDestroy() {
    this.subscriptions.unsubscribe();
  }

  clearFilters() {
    this.service.clearFilters();
  }

  create() {
    this.router.navigate(['/doctor-management/doctor-availabilities/add']);
  }

  deleteGroup(row: DoctorAvailabilityGroupedRow) {
    this.service.deleteGroup(row);
  }

  deleteSlot(record: DoctorAvailabilityWithNavigationPropertiesDto) {
    this.service.deleteSlot(record);
  }

  toggleRow(id: string) {
    if (this.expandedRows.has(id)) {
      this.expandedRows.delete(id);
    } else {
      this.expandedRows.add(id);
    }
  }

  isExpanded(id: string) {
    return this.expandedRows.has(id);
  }

  get totalGroups() {
    return this.service.groupedData.length;
  }

  get pagedGroupedData(): DoctorAvailabilityGroupedRow[] {
    const total = this.totalGroups;
    if (total === 0) {
      return [];
    }

    const totalPages = Math.max(1, Math.ceil(total / this.pageSize));
    if (this.page > totalPages) {
      this.page = totalPages;
    }

    const start = (this.page - 1) * this.pageSize;
    return this.service.groupedData.slice(start, start + this.pageSize);
  }

  get pageStartIndex() {
    if (this.totalGroups === 0) {
      return 0;
    }

    return (this.page - 1) * this.pageSize + 1;
  }

  get pageEndIndex() {
    if (this.totalGroups === 0) {
      return 0;
    }

    return Math.min(this.page * this.pageSize, this.totalGroups);
  }

  formatTime(value: string | null | undefined) {
    if (!value) {
      return '';
    }

    return value.length >= 5 ? value.slice(0, 5) : value;
  }

}
