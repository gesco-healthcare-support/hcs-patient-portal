import { inject } from '@angular/core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { ABP, ListService, PagedResultDto } from '@abp/ng.core';
import { filter, switchMap } from 'rxjs/operators';
import type { GetStatesInput, StateDto } from '../../../proxy/states/models';
import { StateService } from '../../../proxy/states/state.service';

export abstract class AbstractStateViewService {
  protected readonly proxyService = inject(StateService);
  protected readonly confirmationService = inject(ConfirmationService);
  protected readonly list = inject(ListService);

  data: PagedResultDto<StateDto> = {
    items: [],
    totalCount: 0,
  };

  filters = {} as GetStatesInput;

  delete(record: StateDto) {
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

    const setData = (list: PagedResultDto<StateDto>) => {
      this.data = list;
    };

    this.list.hookToQuery(getData).subscribe(setData);
  }

  clearFilters() {
    this.filters = {} as GetStatesInput;
    this.list.get();
  }
}
