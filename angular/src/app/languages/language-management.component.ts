import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { finalize } from 'rxjs/operators';
import { LanguageService } from '@volo/abp.ng.language-management/proxy';
import type { LanguageDto } from '@volo/abp.ng.language-management/proxy';
import { IconComponent } from '../shared/ui/icon/icon.component';

/** Working copy for the new/edit-language modal. */
interface LanguageDraft {
  displayName: string;
  cultureName: string;
  uiCultureName: string;
  flagIcon: string;
  isEnabled: boolean;
  concurrencyStamp?: string;
}

/**
 * Language Management (Prompt 16, Part B) -- the Languages list. A custom
 * standalone rebuild over the stock Volo LanguageService, replacing the ABP
 * language-management module page. The Language Texts editor is intentionally
 * out of scope (cancelled 2026-06-16). IT Admin / host adds languages, sets the
 * default, toggles availability, and removes non-default languages.
 */
@Component({
  selector: 'app-language-management',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './language-management.component.html',
})
export class LanguageManagementComponent {
  private readonly languages = inject(LanguageService);
  private readonly permission = inject(PermissionService);
  private readonly toaster = inject(ToasterService);

  protected readonly rows = signal<LanguageDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly isBusy = signal(false);
  /** null = closed; 'new' = create; otherwise the id being edited. */
  protected readonly editing = signal<string | null>(null);
  protected readonly draft = signal<LanguageDraft | null>(null);
  protected readonly canManage = signal(false);

  constructor() {
    this.canManage.set(this.permission.getGrantedPolicy('LanguageManagement.Languages.Update'));
    this.load();
  }

  /** Two/three-letter chip label derived from the culture (en -> EN, zh-Hans -> ZH). */
  protected flagLabel(culture: string | null | undefined): string {
    return (culture ?? '').split('-')[0].slice(0, 2).toUpperCase() || '?';
  }

  private load(): void {
    this.loading.set(true);
    this.languages
      .getAllList()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (r) => this.rows.set(r.items ?? []),
        error: () => this.rows.set([]),
      });
  }

  protected openNew(): void {
    this.editing.set('new');
    this.draft.set({
      displayName: '',
      cultureName: '',
      uiCultureName: '',
      flagIcon: '',
      isEnabled: true,
    });
  }
  protected openEdit(row: LanguageDto): void {
    this.editing.set(row.id ?? null);
    this.draft.set({
      displayName: row.displayName ?? '',
      cultureName: row.cultureName ?? '',
      uiCultureName: row.uiCultureName ?? '',
      flagIcon: row.flagIcon ?? '',
      isEnabled: row.isEnabled,
      concurrencyStamp: row.concurrencyStamp,
    });
  }
  protected patchDraft(partial: Partial<LanguageDraft>): void {
    const current = this.draft();
    if (current) {
      this.draft.set({ ...current, ...partial });
    }
  }
  protected close(): void {
    if (!this.isBusy()) {
      this.editing.set(null);
      this.draft.set(null);
    }
  }

  protected save(): void {
    const form = this.draft();
    const mode = this.editing();
    if (!form || this.isBusy()) {
      return;
    }
    if (!form.displayName.trim() || (mode === 'new' && !form.cultureName.trim())) {
      this.toaster.warn('Display name and culture are required.');
      return;
    }
    this.isBusy.set(true);
    // Create takes the immutable culture; update only changes display/flag/enabled.
    const request$ =
      mode === 'new'
        ? this.languages.create({
            displayName: form.displayName.trim(),
            cultureName: form.cultureName.trim(),
            uiCultureName: form.uiCultureName.trim() || form.cultureName.trim(),
            flagIcon: form.flagIcon.trim() || undefined,
            isEnabled: form.isEnabled,
          })
        : this.languages.update(mode as string, {
            displayName: form.displayName.trim(),
            flagIcon: form.flagIcon.trim() || undefined,
            isEnabled: form.isEnabled,
            concurrencyStamp: form.concurrencyStamp,
          });
    request$.pipe(finalize(() => this.isBusy.set(false))).subscribe({
      next: () => {
        this.toaster.success('Language saved.');
        this.editing.set(null);
        this.draft.set(null);
        this.load();
      },
      error: () => undefined,
    });
  }

  protected setDefault(row: LanguageDto): void {
    if (!row.id || this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.languages
      .setAsDefault(row.id)
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Default language updated.');
          this.load();
        },
        error: () => undefined,
      });
  }

  protected remove(row: LanguageDto): void {
    if (!row.id || row.isDefaultLanguage || this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.languages
      .delete(row.id)
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Language removed.');
          this.load();
        },
        error: () => undefined,
      });
  }
}
