import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { finalize } from 'rxjs/operators';
import { forkJoin } from 'rxjs';
import {
  DirectoryDescriptorService,
  FileDescriptorService,
} from '@volo/abp.ng.file-management/proxy';
import type {
  CreateFileInputWithStream,
  DirectoryContentDto,
} from '@volo/abp.ng.file-management/proxy';
import { IconComponent } from '../shared/ui/icon/icon.component';

/** One crumb in the breadcrumb path (null id = storage root). */
interface Crumb {
  id: string | null;
  name: string;
}

/** A delete/rename target captured from the content row. */
interface FileTarget {
  id: string;
  name: string;
  concurrencyStamp?: string;
}

const PAGE = { maxResultCount: 500, skipCount: 0 };

/**
 * File Management (Prompt 16, Part B) -- a custom blob-storage explorer over
 * the Volo DirectoryDescriptor + FileDescriptor services, replacing the ABP
 * file-management module page. Navigation is breadcrumb + folder-row drill-in
 * (the recursive sidebar tree in the prototype is deferred as a fidelity
 * refinement). Supports new folder, upload, download, rename, and delete.
 * IT Admin / host only.
 */
@Component({
  selector: 'app-file-management',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './file-management.component.html',
})
export class FileManagementComponent {
  private readonly directories = inject(DirectoryDescriptorService);
  private readonly files = inject(FileDescriptorService);
  private readonly permission = inject(PermissionService);
  private readonly toaster = inject(ToasterService);

  protected readonly content = signal<DirectoryContentDto[]>([]);
  protected readonly path = signal<Crumb[]>([{ id: null, name: 'storage' }]);
  protected readonly query = signal('');
  protected readonly loading = signal(true);
  protected readonly isBusy = signal(false);
  protected readonly canManage = signal(false);

  // Modals: 'newfolder' | 'rename' | 'delete' | null
  protected readonly modal = signal<'newfolder' | 'rename' | 'delete' | null>(null);
  protected readonly folderName = signal('');
  protected readonly target = signal<FileTarget | null>(null);

  protected readonly current = computed<Crumb>(() => {
    const p = this.path();
    return p[p.length - 1];
  });
  protected readonly shown = computed(() => {
    const q = this.query().trim().toLowerCase();
    const rows = this.content();
    return q ? rows.filter((r) => (r.name ?? '').toLowerCase().includes(q)) : rows;
  });
  protected readonly isEmpty = computed(() => !this.loading() && this.shown().length === 0);

  constructor() {
    this.canManage.set(this.permission.getGrantedPolicy('FileManagement.FileDescriptor.Create'));
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.directories
      .getContent({ id: this.current().id ?? undefined, ...PAGE })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (r) => this.content.set(r.items ?? []),
        error: () => this.content.set([]),
      });
  }

  // ---- navigation ----
  protected open(row: DirectoryContentDto): void {
    if (!row.isDirectory) {
      return;
    }
    this.path.set([...this.path(), { id: row.id ?? null, name: row.name ?? '' }]);
    this.query.set('');
    this.load();
  }
  protected goTo(index: number): void {
    this.path.set(this.path().slice(0, index + 1));
    this.query.set('');
    this.load();
  }

  // ---- formatting ----
  protected sizeLabel(bytes: number | null | undefined): string {
    const value = bytes ?? 0;
    if (value < 1024) {
      return value + ' B';
    }
    if (value < 1024 * 1024) {
      return (value / 1024).toFixed(0) + ' KB';
    }
    return (value / (1024 * 1024)).toFixed(1) + ' MB';
  }
  protected typeLabel(row: DirectoryContentDto): string {
    if (row.isDirectory) {
      return 'folder';
    }
    const name = row.name ?? '';
    const dot = name.lastIndexOf('.');
    return dot >= 0 ? name.slice(dot + 1).toLowerCase() : 'file';
  }

  // ---- new folder ----
  protected openNewFolder(): void {
    this.folderName.set('');
    this.modal.set('newfolder');
  }
  protected createFolder(): void {
    const name = this.folderName().trim();
    if (!name) {
      this.toaster.warn('Folder name is required.');
      return;
    }
    if (this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.directories
      .create({ parentId: this.current().id ?? undefined, name })
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Folder created.');
          this.closeModal();
          this.load();
        },
        error: () => undefined,
      });
  }

  // ---- upload (direct file picker -> per-file create) ----
  protected onUpload(event: Event): void {
    const input = event.target as HTMLInputElement;
    const picked = input.files ? Array.from(input.files) : [];
    input.value = '';
    if (!picked.length || this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    const uploads = picked.map((file) =>
      this.files.create(this.current().id ?? '', {
        name: file.name,
        // ABP's RestService serializes a Blob/File body as multipart; the typed
        // CreateFileInputWithStream.file slot takes the browser File at runtime.
        file: file as unknown as CreateFileInputWithStream['file'],
        overrideExisting: true,
      }),
    );
    forkJoin(uploads)
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success(
            picked.length + (picked.length === 1 ? ' file uploaded.' : ' files uploaded.'),
          );
          this.load();
        },
        error: () => undefined,
      });
  }

  // ---- download ----
  protected download(row: DirectoryContentDto): void {
    if (!row.id || this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.files
      .getDownloadToken(row.id)
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: (res) => {
          const token = res.token ?? '';
          this.files.download(row.id as string, token).subscribe({
            next: (blob) => this.saveBlob(blob, row.name ?? 'download'),
            error: () => undefined,
          });
        },
        error: () => undefined,
      });
  }
  private saveBlob(blob: Blob, name: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = name;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  // ---- rename ----
  protected openRename(row: DirectoryContentDto): void {
    this.target.set({
      id: row.id ?? '',
      name: row.name ?? '',
      concurrencyStamp: row.concurrencyStamp,
    });
    this.folderName.set(row.name ?? '');
    this.modal.set('rename');
  }
  protected doRename(): void {
    const t = this.target();
    const name = this.folderName().trim();
    if (!t || !name || this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.files
      .rename(t.id, { name, concurrencyStamp: t.concurrencyStamp })
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('Renamed.');
          this.closeModal();
          this.load();
        },
        error: () => undefined,
      });
  }

  // ---- delete ----
  protected openDelete(row: DirectoryContentDto): void {
    this.target.set({ id: row.id ?? '', name: row.name ?? '' });
    this.modal.set('delete');
  }
  protected doDelete(): void {
    const t = this.target();
    if (!t || this.isBusy()) {
      return;
    }
    this.isBusy.set(true);
    this.files
      .delete(t.id)
      .pipe(finalize(() => this.isBusy.set(false)))
      .subscribe({
        next: () => {
          this.toaster.success('"' + t.name + '" deleted.');
          this.closeModal();
          this.load();
        },
        error: () => undefined,
      });
  }

  protected closeModal(): void {
    if (!this.isBusy()) {
      this.modal.set(null);
      this.target.set(null);
      this.folderName.set('');
    }
  }
}
