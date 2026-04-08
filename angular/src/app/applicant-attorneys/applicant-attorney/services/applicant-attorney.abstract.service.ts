import { inject } from '@angular/core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { ABP, ListService, PagedResultDto } from '@abp/ng.core';
import { filter, switchMap } from 'rxjs/operators';
import type {
  GetApplicantAttorneysInput,
  ApplicantAttorneyWithNavigationPropertiesDto,
} from '../../../proxy/applicant-attorneys/models';
import { ApplicantAttorneyService } from '../../../proxy/applicant-attorneys/applicant-attorney.service';

export abstract class AbstractApplicantAttorneyViewService {
  protected readonly proxyService = inject(ApplicantAttorneyService);
  protected readonly confirmationService = inject(ConfirmationService);
  protected readonly list = inject(ListService);

  data: PagedResultDto<ApplicantAttorneyWithNavigationPropertiesDto> = {
    items: [],
    totalCount: 0,
  };

  filters = {} as GetApplicantAttorneysInput;

  delete(record: ApplicantAttorneyWithNavigationPropertiesDto) {
    this.confirmationService
      .warn('::DeleteConfirmationMessage', '::AreYouSure', { messageLocalizationParams: [] })
      .pipe(
        filter(status => status === Confirmation.Status.confirm),
        switchMap(() => this.proxyService.delete(record.applicantAttorney.id)),
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

    const setData = (list: PagedResultDto<ApplicantAttorneyWithNavigationPropertiesDto>) => {
      this.data = list;
    };

    this.list.hookToQuery(getData).subscribe(setData);
  }

  clearFilters() {
    this.filters = {} as GetApplicantAttorneysInput;
    this.list.get();
  }
}
