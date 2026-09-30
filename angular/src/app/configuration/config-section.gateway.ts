import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { AppointmentTypeService } from '../proxy/appointment-types/appointment-type.service';
import { AppointmentStatusService } from '../proxy/appointment-statuses/appointment-status.service';
import { AppointmentDocumentTypeService } from '../proxy/appointment-document-types/appointment-document-type.service';
import { AppointmentLanguageService } from '../proxy/appointment-languages/appointment-language.service';
import { StateService } from '../proxy/states/state.service';
import type { ConfigFormState, ConfigRow, ConfigSectionKey } from './cf-config.util';

const PAGE = { maxResultCount: 200, skipCount: 0 };

/**
 * Maps a Configuration section to the right generated proxy service so the hub
 * component stays free of per-section CRUD branching. Each section returns rows
 * normalized to {@link ConfigRow}; create/update accept the shared
 * {@link ConfigFormState} and forward only the fields that section's DTO carries.
 */
@Injectable({ providedIn: 'root' })
export class ConfigSectionGateway {
  private readonly types = inject(AppointmentTypeService);
  private readonly statuses = inject(AppointmentStatusService);
  private readonly docTypes = inject(AppointmentDocumentTypeService);
  private readonly languages = inject(AppointmentLanguageService);
  private readonly states = inject(StateService);

  list(section: ConfigSectionKey): Observable<ConfigRow[]> {
    switch (section) {
      case 'types':
        return this.types.getList(PAGE).pipe(
          map((r) =>
            (r.items ?? []).map((t) => ({
              id: t.id ?? '',
              name: t.name ?? '',
              description: t.description ?? '',
              usageCount: t.usageCount ?? null,
              isSystem: !!t.isSystem,
            })),
          ),
        );
      case 'statuses':
        return this.statuses.getList(PAGE).pipe(
          map((r) =>
            (r.items ?? []).map((s) => ({
              id: s.id ?? '',
              name: s.name ?? '',
              usageCount: s.usageCount ?? null,
              isSystem: !!s.isSystem,
            })),
          ),
        );
      case 'doctypes':
        return this.docTypes.getList(PAGE).pipe(
          map((r) =>
            (r.items ?? []).map((d) => ({
              id: d.id ?? '',
              name: d.name ?? '',
              usageCount: d.usageCount ?? null,
              isSystem: !!d.isSystem,
              isActive: d.isActive ?? true,
              appointmentTypeIds: d.appointmentTypeIds ?? [],
              appliesToAll: !!d.appliesToAll,
            })),
          ),
        );
      case 'languages':
        return this.languages.getList(PAGE).pipe(
          map((r) =>
            (r.items ?? []).map((l) => ({
              id: l.id ?? '',
              name: l.name ?? '',
              usageCount: l.usageCount ?? null,
              isSystem: !!l.isSystem,
            })),
          ),
        );
      case 'states':
        return this.states.getList(PAGE).pipe(
          map((r) =>
            (r.items ?? []).map((s) => ({
              id: s.id ?? '',
              name: s.name ?? '',
              usageCount: s.usageCount ?? null,
              isSystem: !!s.isSystem,
              concurrencyStamp: s.concurrencyStamp,
            })),
          ),
        );
    }
  }

  create(section: ConfigSectionKey, form: ConfigFormState): Observable<unknown> {
    const name = form.name.trim();
    switch (section) {
      case 'types':
        return this.types.create({ name, description: form.description.trim() || null });
      case 'statuses':
        return this.statuses.create({ name });
      case 'doctypes':
        return this.docTypes.create({
          name,
          isActive: form.isActive,
          appliesToAll: form.appliesToAll,
          appointmentTypeIds: form.appliesToAll ? [] : form.appointmentTypeIds,
        });
      case 'languages':
        return this.languages.create({ name });
      case 'states':
        return this.states.create({ name });
    }
  }

  update(section: ConfigSectionKey, form: ConfigFormState): Observable<unknown> {
    const id = form.id as string;
    const name = form.name.trim();
    switch (section) {
      case 'types':
        return this.types.update(id, { name, description: form.description.trim() || null });
      case 'statuses':
        return this.statuses.update(id, { name });
      case 'doctypes':
        return this.docTypes.update(id, {
          name,
          isActive: form.isActive,
          appliesToAll: form.appliesToAll,
          appointmentTypeIds: form.appliesToAll ? [] : form.appointmentTypeIds,
        });
      case 'languages':
        return this.languages.update(id, { name });
      case 'states':
        return this.states.update(id, { name, concurrencyStamp: form.concurrencyStamp });
    }
  }

  delete(section: ConfigSectionKey, id: string): Observable<void> {
    switch (section) {
      case 'types':
        return this.types.delete(id);
      case 'statuses':
        return this.statuses.delete(id);
      case 'doctypes':
        return this.docTypes.delete(id);
      case 'languages':
        return this.languages.delete(id);
      case 'states':
        return this.states.delete(id);
    }
  }
}
