import { Directive, HostBinding, HostListener, Input, output } from '@angular/core';

/**
 * Item H (2026-08-22) -- drag-and-drop for the document uploaders.
 *
 * <p>A directive rather than three copies of the same handlers, because the queue groups H with G
 * precisely to stop the upload surfaces drifting apart. All three (booking wizard, appointment
 * detail, anonymous upload-by-code) get identical behaviour from one place.</p>
 *
 * <p>It deliberately does NOT validate. The drop zone is a convenience for choosing a file; the
 * format and size rules live where they already are -- the shared validator client-side, and the
 * server authoritatively. Re-checking here would be a third copy of the allow-list and the first to
 * drift.</p>
 *
 * <p>`dragover` must call preventDefault or the browser navigates away to the dropped file, which
 * silently loses whatever the user was filling in.</p>
 */
@Directive({
  selector: '[appFileDropZone]',
  standalone: true,
})
export class FileDropZoneDirective {
  /** Suppresses drop handling while the surface is busy or read-only. */
  @Input() dropDisabled = false;

  readonly filesDropped = output<File[]>();

  @HostBinding('class.is-drag-over')
  isDragOver = false;

  @HostListener('dragover', ['$event'])
  onDragOver(event: DragEvent): void {
    if (this.dropDisabled) {
      return;
    }
    // Without this the browser opens the file instead of letting us have it.
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = true;
  }

  @HostListener('dragleave', ['$event'])
  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = false;
  }

  @HostListener('drop', ['$event'])
  onDrop(event: DragEvent): void {
    if (this.dropDisabled) {
      return;
    }
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = false;

    const dropped = event.dataTransfer?.files;
    if (!dropped || dropped.length === 0) {
      return;
    }
    this.filesDropped.emit(Array.from(dropped));
  }
}
