import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import {
  DirectoryDescriptorService,
  FileDescriptorService,
} from '@volo/abp.ng.file-management/proxy';

import { FileManagementComponent } from './file-management.component';

/**
 * Covers the Escape-to-close handler added in sweep #657, and the breadcrumb
 * `.at(-1)` change in the same sweep.
 *
 * This component loads content from its constructor, so unlike the other sweep
 * specs the directory service has to return a real observable -- there is no
 * way to create the component without `load()` running.
 */
describe('FileManagementComponent (sweep #657)', () => {
  interface Probe {
    modal: { set(value: unknown): void };
    isBusy: { set(value: boolean): void };
    path: { set(value: unknown[]): void };
    onEscapeKey(): void;
  }

  function create() {
    TestBed.configureTestingModule({
      providers: [
        { provide: DirectoryDescriptorService, useValue: { getContent: () => of({ items: [] }) } },
        { provide: FileDescriptorService, useValue: {} },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
      ],
    });
    const fixture = TestBed.createComponent(FileManagementComponent);
    const inst = fixture.componentInstance as unknown as Probe & {
      modal(): unknown;
      current(): { name?: string };
    };
    return {
      fixture,
      probe: inst as Probe,
      readModal: () => inst.modal(),
      current: () => inst.current(),
    };
  }

  afterEach(() => TestBed.resetTestingModule());

  it('closes an open modal on Escape', () => {
    const c = create();
    c.probe.modal.set('newfolder');
    c.probe.onEscapeKey();
    expect(c.readModal()).toBeNull();
  });

  it('closes each of the three modals', () => {
    for (const kind of ['newfolder', 'rename', 'delete']) {
      const c = create();
      c.probe.modal.set(kind);
      c.probe.onEscapeKey();
      expect(c.readModal()).withContext(kind).toBeNull();
      TestBed.resetTestingModule();
    }
  });

  it('does not discard an operation in flight', () => {
    // The guard is inherited from closeModal rather than reimplemented.
    const c = create();
    c.probe.modal.set('rename');
    c.probe.isBusy.set(true);
    c.probe.onEscapeKey();
    expect(c.readModal()).toBe('rename');
  });

  it('is inert when no modal is open', () => {
    const c = create();
    expect(() => c.probe.onEscapeKey()).not.toThrow();
    expect(c.readModal()).toBeNull();
  });

  it('is wired to a real document Escape keypress, not just callable', () => {
    // Proves the @HostListener binding, which a direct method call cannot.
    const c = create();
    c.probe.modal.set('delete');
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(c.readModal()).toBeNull();
  });

  it('reads the last breadcrumb as the current folder', () => {
    // Pins the .at(-1) rewrite: the deepest crumb wins, not the first.
    const c = create();
    expect(c.current().name).toBe('storage');
    c.probe.path.set([
      { id: null, name: 'storage' },
      { id: 'a', name: '2026-07' },
      { id: 'b', name: 'intake' },
    ]);
    expect(c.current().name).toBe('intake');
  });

  /**
   * Escape must survive the real bubble path, not just the binding.
   *
   * The actions cell carries a stopPropagation guard so Enter on a row button
   * does not also fire the row's own (keydown.enter)="open(row)". openRename and
   * openDelete live inside that cell, and there is no focus management here, so
   * focus stays on the button after activation.
   *
   * A document-level HostListener sits at the END of the bubble path, so an
   * unconditional (keydown) guard swallows Escape before it arrives. Dispatching
   * straight at `document` cannot see that: it starts the event AT the listener
   * and skips the path a real keypress travels.
   *
   * The row must be a FILE -- the cell renders Open for a directory and the
   * rename/delete buttons only in the @else branch, behind canManage().
   */
  it('closes the rename modal on Escape pressed from inside the actions cell', () => {
    const c = create();
    // The constructor already ran load(), so content can be seeded directly.
    (c.probe as unknown as { content: { set(v: unknown[]): void } }).content.set([
      { id: 'f-1', name: 'intake.pdf', isDirectory: false, size: 2048 },
    ]);
    c.fixture.detectChanges();

    const host = c.fixture.nativeElement as HTMLElement;
    const button = host.querySelector('button[title="Rename"]') as HTMLButtonElement | null;
    expect(button).withContext('rename button should render for a file').not.toBeNull();

    c.probe.modal.set('rename');
    button!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(c.readModal())
      .withContext('Escape from inside the actions cell must reach the document')
      .toBeNull();
  });
});
