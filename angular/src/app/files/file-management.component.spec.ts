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
        { provide: PermissionService, useValue: { getGrantedPolicy: () => false } },
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
});
