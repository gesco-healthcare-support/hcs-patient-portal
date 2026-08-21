import { inject } from '@angular/core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { ABP, ListService, PagedResultDto } from '@abp/ng.core';
import { filter, switchMap } from 'rxjs/operators';
import type {
  GetAppointmentDocumentTypesInput,
  AppointmentDocumentTypeDto,
} from '../../../proxy/appointment-document-types/models';
import { AppointmentDocumentTypeService } from '../../../proxy/appointment-document-types/appointment-document-type.service';
import type { AppointmentTypeDto } from '../../../proxy/appointment-types/models';
import { AppointmentTypeService } from '../../../proxy/appointment-types/appointment-type.service';

export abstract class AbstractAppointmentDocumentTypeViewService {
  protected readonly proxyService = inject(AppointmentDocumentTypeService);
  protected readonly appointmentTypeService = inject(AppointmentTypeService);
  protected readonly confirmationService = inject(ConfirmationService);
  protected readonly list = inject(ListService);

  data: PagedResultDto<AppointmentDocumentTypeDto> = {
    items: [],
    totalCount: 0,
  };

  filters = {} as GetAppointmentDocumentTypesInput;

  // Loaded so the list can render appointment-type labels (decision: the UI
  // shows the type name, never the stored id) and drive the type filter.
  appointmentTypes: AppointmentTypeDto[] = [];

  loadAppointmentTypes() {
    this.appointmentTypeService
      .getList({ maxResultCount: 1000, skipCount: 0 })
      .subscribe((res) => (this.appointmentTypes = res.items ?? []));
  }

  appointmentTypeName(id?: string | null): string {
    if (!id) {
      return '';
    }
    return this.appointmentTypes.find((t) => t.id === id)?.name ?? '';
  }

  delete(record: AppointmentDocumentTypeDto) {
    this.confirmationService
      .warn('::DeleteConfirmationMessage', '::AreYouSure', { messageLocalizationParams: [] })
      .pipe(
        filter((status) => status === Confirmation.Status.confirm),
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

    const setData = (list: PagedResultDto<AppointmentDocumentTypeDto>) => {
      this.data = list;
    };

    this.list.hookToQuery(getData).subscribe(setData);
  }

  applyTypeFilter() {
    this.list.get();
  }

  clearFilters() {
    this.filters = {} as GetAppointmentDocumentTypesInput;
    this.list.get();
  }
}
