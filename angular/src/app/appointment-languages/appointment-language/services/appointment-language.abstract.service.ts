import { inject } from '@angular/core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { ABP, ListService, PagedResultDto } from '@abp/ng.core';
import { filter, switchMap } from 'rxjs/operators';
import type {
  GetAppointmentLanguagesInput,
  AppointmentLanguageDto,
} from '../../../proxy/appointment-languages/models';
import { AppointmentLanguageService } from '../../../proxy/appointment-languages/appointment-language.service';

export abstract class AbstractAppointmentLanguageViewService {
  protected readonly proxyService = inject(AppointmentLanguageService);
  protected readonly confirmationService = inject(ConfirmationService);
  protected readonly list = inject(ListService);

  data: PagedResultDto<AppointmentLanguageDto> = {
    items: [],
    totalCount: 0,
  };

  filters = {} as GetAppointmentLanguagesInput;

  delete(record: AppointmentLanguageDto) {
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

    const setData = (list: PagedResultDto<AppointmentLanguageDto>) => {
      this.data = list;
    };

    this.list.hookToQuery(getData).subscribe(setData);
  }

  clearFilters() {
    this.filters = {} as GetAppointmentLanguagesInput;
    this.list.get();
  }
}
