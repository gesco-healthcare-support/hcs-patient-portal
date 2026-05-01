import { inject } from '@angular/core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { ABP, ListService, PagedResultDto } from '@abp/ng.core';
import { filter, switchMap } from 'rxjs/operators';
import type {
  GetDefenseAttorneysInput,
  DefenseAttorneyWithNavigationPropertiesDto,
} from '../../../proxy/defense-attorneys/models';
import { DefenseAttorneyService } from '../../../proxy/defense-attorneys/defense-attorney.service';

export abstract class AbstractDefenseAttorneyViewService {
  protected readonly proxyService = inject(DefenseAttorneyService);
  protected readonly confirmationService = inject(ConfirmationService);
  protected readonly list = inject(ListService);

  data: PagedResultDto<DefenseAttorneyWithNavigationPropertiesDto> = {
    items: [],
    totalCount: 0,
  };

  filters = {} as GetDefenseAttorneysInput;

  delete(record: DefenseAttorneyWithNavigationPropertiesDto) {
    this.confirmationService
      .warn('::DeleteConfirmationMessage', '::AreYouSure', { messageLocalizationParams: [] })
      .pipe(
        filter((status) => status === Confirmation.Status.confirm),
        switchMap(() => this.proxyService.delete(record.defenseAttorney.id)),
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

    const setData = (list: PagedResultDto<DefenseAttorneyWithNavigationPropertiesDto>) => {
      this.data = list;
    };

    this.list.hookToQuery(getData).subscribe(setData);
  }

  clearFilters() {
    this.filters = {} as GetDefenseAttorneysInput;
    this.list.get();
  }
}
