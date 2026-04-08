import { inject } from '@angular/core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { ABP, ListService, PagedResultDto } from '@abp/ng.core';
import { filter, switchMap } from 'rxjs/operators';
import type {
  GetAppointmentStatusesInput,
  AppointmentStatusDto,
} from '../../../proxy/appointment-statuses/models';
import { AppointmentStatusService } from '../../../proxy/appointment-statuses/appointment-status.service';

export abstract class AbstractAppointmentStatusViewService {
  protected readonly proxyService = inject(AppointmentStatusService);
  protected readonly confirmationService = inject(ConfirmationService);
  protected readonly list = inject(ListService);

  data: PagedResultDto<AppointmentStatusDto> = {
    items: [],
    totalCount: 0,
  };

  filters = {} as GetAppointmentStatusesInput;

  delete(record: AppointmentStatusDto) {
    this.confirmationService
      .warn('::DeleteConfirmationMessage', '::AreYouSure', { messageLocalizationParams: [] })
      .pipe(
        filter(status => status === Confirmation.Status.confirm),
        switchMap(() => this.proxyService.delete(record.id)),
      )
      .subscribe(this.list.get);
  }

  hookToQuery() {
    const getData = (query: ABP.PageQueryParams) =>
      this.proxyService.getList({
        ...query,
        ...this.filters,
        filterText: query.filter,
      });

    const setData = (list: PagedResultDto<AppointmentStatusDto>) => {
      this.data = list;
    };

    this.list.hookToQuery(getData).subscribe(setData);
  }

  clearFilters() {
    this.filters = {} as GetAppointmentStatusesInput;
    this.list.get();
  }
}
