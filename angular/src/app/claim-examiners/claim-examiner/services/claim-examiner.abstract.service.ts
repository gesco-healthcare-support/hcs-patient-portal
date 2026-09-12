import { inject } from '@angular/core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { ABP, ListService, PagedResultDto } from '@abp/ng.core';
import { filter, switchMap } from 'rxjs/operators';
import type {
  GetClaimExaminersInput,
  ClaimExaminerWithNavigationPropertiesDto,
} from '../../../proxy/claim-examiners/models';
import { ClaimExaminerService } from '../../../proxy/claim-examiners/claim-examiner.service';

export abstract class AbstractClaimExaminerViewService {
  protected readonly proxyService = inject(ClaimExaminerService);
  protected readonly confirmationService = inject(ConfirmationService);
  protected readonly list = inject(ListService);

  data: PagedResultDto<ClaimExaminerWithNavigationPropertiesDto> = {
    items: [],
    totalCount: 0,
  };

  filters = {} as GetClaimExaminersInput;

  delete(record: ClaimExaminerWithNavigationPropertiesDto) {
    this.confirmationService
      .warn('::DeleteConfirmationMessage', '::AreYouSure', { messageLocalizationParams: [] })
      .pipe(
        filter((status) => status === Confirmation.Status.confirm),
        switchMap(() => this.proxyService.delete(record.claimExaminer.id)),
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

    const setData = (list: PagedResultDto<ClaimExaminerWithNavigationPropertiesDto>) => {
      this.data = list;
    };

    this.list.hookToQuery(getData).subscribe(setData);
  }

  clearFilters() {
    this.filters = {} as GetClaimExaminersInput;
    this.list.get();
  }
}
