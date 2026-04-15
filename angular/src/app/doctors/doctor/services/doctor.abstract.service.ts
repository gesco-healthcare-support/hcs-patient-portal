import { inject } from '@angular/core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { ABP, ListService, PagedResultDto } from '@abp/ng.core';
import { filter, switchMap } from 'rxjs/operators';
import type {
  GetDoctorsInput,
  DoctorWithNavigationPropertiesDto,
} from '../../../proxy/doctors/models';
import { DoctorService } from '../../../proxy/doctors/doctor.service';

export abstract class AbstractDoctorViewService {
  protected readonly proxyService = inject(DoctorService);
  protected readonly confirmationService = inject(ConfirmationService);
  protected readonly list = inject(ListService);

  public readonly getWithNavigationProperties = this.proxyService.getWithNavigationProperties;

  data: PagedResultDto<DoctorWithNavigationPropertiesDto> = {
    items: [],
    totalCount: 0,
  };

  filters = {} as GetDoctorsInput;

  delete(record: DoctorWithNavigationPropertiesDto) {
    this.confirmationService
      .warn('::DeleteConfirmationMessage', '::AreYouSure', { messageLocalizationParams: [] })
      .pipe(
        filter((status) => status === Confirmation.Status.confirm),
        switchMap(() => this.proxyService.delete(record.doctor.id)),
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

    const setData = (list: PagedResultDto<DoctorWithNavigationPropertiesDto>) => {
      this.data = list;
    };

    this.list.hookToQuery(getData).subscribe(setData);
  }

  clearFilters() {
    this.filters = {} as GetDoctorsInput;
    this.list.get();
  }
}
