import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import {
  DirectoryDescriptorService,
  FileDescriptorService,
} from '@volo/abp.ng.file-management/proxy';

import { FileManagementComponent } from './file-management.component';

/**
 * File Management -- the blob-storage explorer over the Volo directory and file services.
 *
 * <p>It sat at 82 of 125 lines uncovered. An existing spec (sweep #657) covers the Escape
 * handler and the breadcrumb `current` computed; neither is repeated here.</p>
 *
 * <p>What this covers is the explorer itself: breadcrumb navigation, the search filter, and the
 * four write operations. Two details are worth stating. `sizeLabel` accepts null deliberately --
 * the component's comment records that a default parameter would be a regression, because a
 * default applies only to `undefined` and an explicit null would render as "null B"; that is
 * pinned here rather than left to the comment. And every modal inherits the in-flight guard, so
 * a rename or upload cannot be dismissed out from under itself.</p>
 *
 * <p>The component loads in its CONSTRUCTOR, so the root listing is already requested by the
 * time a test runs.</p>
 *
 * <p>All folder names, file names and identifiers below are synthetic.</p>
 */
describe('FileManagementComponent surfaces', () => {
  let directories: Record<string, jasmine.Spy>;
  let files: Record<string, jasmine.Spy>;
  let toaster: { success: jasmine.Spy; warn: jasmine.Spy; error: jasmine.Spy };
  let granted: boolean;

  interface Probe {
    [key: string]: any;
  }

  function row(over: Record<string, unknown> = {}) {
    return {
      id: 'f-1',
      name: 'notes.txt',
      isDirectory: false,
      size: 2048,
      concurrencyStamp: 'stamp-1',
      ...over,
    };
  }

  function create(options: { canManage?: boolean } = {}): Probe {
    granted = options.canManage ?? true;

    directories = {
      getContent: jasmine.createSpy('getContent').and.returnValue(of({ items: [] })),
      create: jasmine.createSpy('createDirectory').and.returnValue(of({})),
    };
    files = {
      create: jasmine.createSpy('createFile').and.returnValue(of({})),
      getDownloadToken: jasmine
        .createSpy('getDownloadToken')
        .and.returnValue(of({ token: 'tok-1' })),
      download: jasmine.createSpy('download').and.returnValue(of(new Blob(['x']))),
      rename: jasmine.createSpy('rename').and.returnValue(of({})),
      delete: jasmine.createSpy('deleteFile').and.returnValue(of(undefined)),
    };
    toaster = {
      success: jasmine.createSpy('success'),
      warn: jasmine.createSpy('warn'),
      error: jasmine.createSpy('error'),
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: DirectoryDescriptorService, useValue: directories },
        { provide: FileDescriptorService, useValue: files },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => granted } },
        { provide: ToasterService, useValue: toaster },
      ],
    });

    return TestBed.createComponent(FileManagementComponent).componentInstance as unknown as Probe;
  }

  /** A file-picker change event carrying the given files. */
  function pickEvent(names: string[]): Event {
    const input = document.createElement('input');
    input.type = 'file';
    const list = names.map((n) => new File(['x'], n, { type: 'text/plain' }));
    Object.defineProperty(input, 'files', { value: list, configurable: true });
    return { target: input } as unknown as Event;
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('the initial listing', () => {
    it('loads the storage root on construction', () => {
      const c = create();
      expect(directories['getContent']).toHaveBeenCalled();
      expect(directories['getContent'].calls.mostRecent().args[0].id).toBeUndefined();
      expect(c.path().length).toBe(1);
    });

    it('stores the rows and clears the loading flag', () => {
      const c = create();
      directories['getContent'].and.returnValue(of({ items: [row()] }));
      // goTo re-enters load() with the stubbed response; the constructor already ran.
      c.goTo(0);
      expect(c.content().length).toBe(1);
      expect(c.loading()).toBeFalse();
    });

    it('empties the listing and still clears loading when the request fails', () => {
      const c = create();
      directories['getContent'].and.returnValue(throwError(() => ({ status: 500 })));
      c.goTo(0);
      expect(c.content()).toEqual([]);
      expect(c.loading()).toBeFalse();
    });

    it('treats a payload with no items as an empty folder', () => {
      const c = create();
      directories['getContent'].and.returnValue(of({}));
      c.goTo(0);
      expect(c.content()).toEqual([]);
    });

    it('reads the manage permission once, at construction', () => {
      expect(create({ canManage: true }).canManage()).toBeTrue();
      TestBed.resetTestingModule();
      expect(create({ canManage: false }).canManage()).toBeFalse();
    });
  });

  describe('navigating', () => {
    it('drills into a folder and extends the breadcrumb', () => {
      const c = create();
      c.open(row({ id: 'd-1', name: 'reports', isDirectory: true }));
      expect(c.path().map((p: { name: string }) => p.name)).toEqual(['storage', 'reports']);
      expect(c.current().id).toBe('d-1');
      expect(directories['getContent'].calls.mostRecent().args[0].id).toBe('d-1');
    });

    it('does nothing when the row is a file', () => {
      const c = create();
      const before = c.path().length;
      c.open(row({ isDirectory: false }));
      expect(c.path().length).toBe(before);
    });

    it('substitutes empty values for a folder row missing its id or name', () => {
      const c = create();
      c.open({ isDirectory: true });
      expect(c.current().id).toBeNull();
      expect(c.current().name).toBe('');
    });

    it('clears the search when the folder changes', () => {
      // The query belongs to the folder it was typed in; carrying it would show an
      // unexplained empty folder on arrival.
      const c = create();
      c.query.set('notes');
      c.open(row({ id: 'd-1', name: 'reports', isDirectory: true }));
      expect(c.query()).toBe('');
    });

    it('truncates the breadcrumb back to the clicked crumb', () => {
      const c = create();
      c.open(row({ id: 'd-1', name: 'a', isDirectory: true }));
      c.open(row({ id: 'd-2', name: 'b', isDirectory: true }));
      expect(c.path().length).toBe(3);

      c.goTo(1);

      expect(c.path().map((p: { name: string }) => p.name)).toEqual(['storage', 'a']);
      expect(c.current().id).toBe('d-1');
    });

    it('clears the search when a crumb is clicked', () => {
      const c = create();
      c.query.set('notes');
      c.goTo(0);
      expect(c.query()).toBe('');
    });
  });

  describe('the search filter', () => {
    function seeded(): Probe {
      const c = create();
      c.content.set([
        row({ id: '1', name: 'Quarterly notes.txt' }),
        row({ id: '2', name: 'budget.xlsx' }),
        row({ id: '3', name: null }),
      ]);
      c.loading.set(false);
      return c;
    }

    it('returns everything when nothing is typed', () => {
      const c = seeded();
      expect(c.shown().length).toBe(3);
    });

    it('matches on a case-insensitive substring', () => {
      const c = seeded();
      c.query.set('NOTES');
      expect(c.shown().map((r: { id: string }) => r.id)).toEqual(['1']);
    });

    it('ignores surrounding whitespace in the query', () => {
      const c = seeded();
      c.query.set('  budget  ');
      expect(c.shown().map((r: { id: string }) => r.id)).toEqual(['2']);
    });

    it('excludes a row with no name instead of failing on it', () => {
      const c = seeded();
      c.query.set('budget');
      expect(c.shown().map((r: { id: string }) => r.id))
        .withContext('the null-named row is simply not a match')
        .toEqual(['2']);
    });

    it('returns nothing when the query matches no row at all', () => {
      const c = seeded();
      c.query.set('no-such-file');
      expect(c.shown()).toEqual([]);
    });

    it('reports empty only once the load has finished', () => {
      // While loading, an empty list is "not known yet" rather than "nothing here".
      const c = create();
      c.content.set([]);
      c.loading.set(true);
      expect(c.isEmpty()).toBeFalse();

      c.loading.set(false);
      expect(c.isEmpty()).toBeTrue();
    });

    it('reports empty when a search matches nothing', () => {
      const c = seeded();
      c.query.set('nothing-matches-this');
      expect(c.isEmpty()).toBeTrue();
    });
  });

  describe('row display', () => {
    it('scales a byte count to the right unit', () => {
      const c = create();
      expect(c.sizeLabel(512)).toBe('512 B');
      expect(c.sizeLabel(2048)).toBe('2 KB');
      expect(c.sizeLabel(5 * 1024 * 1024)).toBe('5.0 MB');
    });

    it('renders an ABSENT size as zero, not as the word null', () => {
      /**
       * The component keeps `?? 0` rather than a default parameter, and its comment says
       * why: a default applies only to undefined, so an explicit null would flow through
       * and render as "null B". Both are pinned because only one of them is the trap.
       */
      const c = create();
      expect(c.sizeLabel(null)).toBe('0 B');
      expect(c.sizeLabel(undefined)).toBe('0 B');
    });

    it('names a folder a folder', () => {
      const c = create();
      expect(c.typeLabel(row({ isDirectory: true }))).toBe('folder');
    });

    it('takes the extension from the file name, lowercased', () => {
      const c = create();
      expect(c.typeLabel(row({ name: 'REPORT.PDF' }))).toBe('pdf');
      expect(c.typeLabel(row({ name: 'archive.tar.gz' }))).toBe('gz');
    });

    it('calls a file with no extension a file', () => {
      const c = create();
      expect(c.typeLabel(row({ name: 'README' }))).toBe('file');
      expect(c.typeLabel(row({ name: null }))).toBe('file');
    });
  });

  describe('creating a folder', () => {
    it('opens with an empty name', () => {
      const c = create();
      c.folderName.set('left over');
      c.openNewFolder();
      expect(c.folderName()).toBe('');
      expect(c.modal()).toBe('newfolder');
    });

    it('requires a name', () => {
      const c = create();
      c.openNewFolder();
      c.folderName.set('   ');
      c.createFolder();
      expect(directories['create']).not.toHaveBeenCalled();
      expect(toaster.warn).toHaveBeenCalledWith('Folder name is required.');
    });

    it('creates it inside the folder currently open', () => {
      const c = create();
      c.open(row({ id: 'd-1', name: 'reports', isDirectory: true }));
      c.openNewFolder();
      c.folderName.set('  2026  ');

      c.createFolder();

      expect(directories['create']).toHaveBeenCalledWith({ parentId: 'd-1', name: '2026' });
    });

    it('reports and reloads on success, but does NOT close the dialog', () => {
      /**
       * PINNED AS FOUND, NOT AS INTENDED -- this is a defect, logged to the backlog.
       *
       * The success handler calls closeModal(), but it runs while isBusy is still true:
       * `finalize` fires on COMPLETE, which is after `next`. So closeModal's own
       * in-flight guard refuses, and the dialog stays open behind the success toast.
       * The same shape affects rename and delete below.
       *
       * Not fixed here: this PR carries exactly one approved product change and it is
       * not this one.
       */
      const c = create();
      c.openNewFolder();
      c.folderName.set('2026');
      directories['getContent'].calls.reset();

      c.createFolder();

      expect(toaster.success).toHaveBeenCalledWith('Folder created.');
      expect(directories['getContent']).toHaveBeenCalled();
      expect(c.isBusy()).toBeFalse();
      expect(c.modal())
        .withContext('closeModal is blocked by its own guard on the success path')
        .toBe('newfolder');
    });

    it('does nothing while another operation is running', () => {
      const c = create();
      c.openNewFolder();
      c.folderName.set('2026');
      c.isBusy.set(true);
      c.createFolder();
      expect(directories['create']).not.toHaveBeenCalled();
    });

    it('releases the button when the create fails', () => {
      const c = create();
      directories['create'].and.returnValue(throwError(() => ({ status: 409 })));
      c.openNewFolder();
      c.folderName.set('2026');

      c.createFolder();

      expect(c.isBusy()).toBeFalse();
    });
  });

  describe('uploading', () => {
    it('does nothing when the picker was dismissed', () => {
      const c = create();
      c.onUpload(pickEvent([]));
      expect(files['create']).not.toHaveBeenCalled();
    });

    it('does nothing while another operation is running', () => {
      const c = create();
      c.isBusy.set(true);
      c.onUpload(pickEvent(['a.txt']));
      expect(files['create']).not.toHaveBeenCalled();
    });

    it('clears the picker so the same file can be chosen again', () => {
      // Without this, re-picking an identical file fires no change event at all.
      const c = create();
      const event = pickEvent(['a.txt']);
      c.onUpload(event);
      expect((event.target as HTMLInputElement).value).toBe('');
    });

    it('sends one create per chosen file, into the open folder', () => {
      const c = create();
      c.open(row({ id: 'd-1', name: 'reports', isDirectory: true }));

      c.onUpload(pickEvent(['a.txt', 'b.txt']));

      expect(files['create']).toHaveBeenCalledTimes(2);
      expect(files['create'].calls.first().args[0]).toBe('d-1');
      expect(files['create'].calls.first().args[1].name).toBe('a.txt');
      expect(files['create'].calls.first().args[1].overrideExisting).toBeTrue();
    });

    it('uploads into the root as an empty parent id', () => {
      const c = create();
      c.onUpload(pickEvent(['a.txt']));
      expect(files['create'].calls.mostRecent().args[0]).toBe('');
    });

    it('reports one file and several files differently', () => {
      const c = create();
      c.onUpload(pickEvent(['a.txt']));
      expect(toaster.success).toHaveBeenCalledWith('1 file uploaded.');

      c.onUpload(pickEvent(['a.txt', 'b.txt']));
      expect(toaster.success).toHaveBeenCalledWith('2 files uploaded.');
    });

    it('reloads the folder afterwards', () => {
      const c = create();
      directories['getContent'].calls.reset();
      c.onUpload(pickEvent(['a.txt']));
      expect(directories['getContent']).toHaveBeenCalled();
      expect(c.isBusy()).toBeFalse();
    });

    it('releases the button when an upload fails', () => {
      const c = create();
      files['create'].and.returnValue(throwError(() => ({ status: 413 })));
      c.onUpload(pickEvent(['a.txt']));
      expect(c.isBusy()).toBeFalse();
    });
  });

  describe('downloading', () => {
    it('does nothing for a row with no id', () => {
      const c = create();
      c.download(row({ id: null }));
      expect(files['getDownloadToken']).not.toHaveBeenCalled();
    });

    it('does nothing while another operation is running', () => {
      const c = create();
      c.isBusy.set(true);
      c.download(row());
      expect(files['getDownloadToken']).not.toHaveBeenCalled();
    });

    it('takes a token first, then fetches with it', () => {
      // The download endpoint is token-gated; fetching without one returns 401.
      const c = create();
      c.download(row({ id: 'f-9' }));
      expect(files['getDownloadToken']).toHaveBeenCalledWith('f-9');
      expect(files['download']).toHaveBeenCalledWith('f-9', 'tok-1');
      expect(c.isBusy()).toBeFalse();
    });

    it('sends an empty token rather than undefined when the response carries none', () => {
      const c = create();
      files['getDownloadToken'].and.returnValue(of({}));
      c.download(row({ id: 'f-9' }));
      expect(files['download']).toHaveBeenCalledWith('f-9', '');
    });

    it('releases the button when the token request fails', () => {
      const c = create();
      files['getDownloadToken'].and.returnValue(throwError(() => ({ status: 403 })));
      c.download(row());
      expect(files['download']).not.toHaveBeenCalled();
      expect(c.isBusy()).toBeFalse();
    });

    it('survives a failure fetching the bytes', () => {
      const c = create();
      files['download'].and.returnValue(throwError(() => ({ status: 500 })));
      expect(() => c.download(row())).not.toThrow();
      expect(c.isBusy()).toBeFalse();
    });
  });

  describe('renaming', () => {
    it('opens pre-filled with the current name and the row stamp', () => {
      const c = create();
      c.openRename(row({ id: 'f-9', name: 'old.txt', concurrencyStamp: 'stamp-9' }));
      expect(c.modal()).toBe('rename');
      expect(c.folderName()).toBe('old.txt');
      expect(c.target()).toEqual({ id: 'f-9', name: 'old.txt', concurrencyStamp: 'stamp-9' });
    });

    it('requires a name', () => {
      const c = create();
      c.openRename(row());
      c.folderName.set('   ');
      c.doRename();
      expect(files['rename']).not.toHaveBeenCalled();
    });

    it('does nothing with no target or while busy', () => {
      const c = create();
      c.doRename();
      expect(files['rename']).not.toHaveBeenCalled();

      c.openRename(row());
      c.isBusy.set(true);
      c.doRename();
      expect(files['rename']).not.toHaveBeenCalled();
    });

    it('sends the trimmed name and the concurrency stamp', () => {
      // Dropping the stamp turns a rename into a lost-update conflict.
      const c = create();
      c.openRename(row({ id: 'f-9', concurrencyStamp: 'stamp-9' }));
      c.folderName.set('  new.txt  ');

      c.doRename();

      expect(files['rename']).toHaveBeenCalledWith('f-9', {
        name: 'new.txt',
        concurrencyStamp: 'stamp-9',
      });
    });

    it('reports and reloads on success, with the same dialog defect as create', () => {
      const c = create();
      c.openRename(row());
      directories['getContent'].calls.reset();

      c.doRename();

      expect(toaster.success).toHaveBeenCalledWith('Renamed.');
      expect(directories['getContent']).toHaveBeenCalled();
      expect(c.modal()).withContext('see the create-folder note').toBe('rename');
    });

    it('releases the button when the rename fails', () => {
      const c = create();
      files['rename'].and.returnValue(throwError(() => ({ status: 409 })));
      c.openRename(row());

      c.doRename();

      expect(c.isBusy()).toBeFalse();
    });
  });

  describe('deleting', () => {
    it('opens against the chosen row', () => {
      const c = create();
      c.openDelete(row({ id: 'f-9', name: 'old.txt' }));
      expect(c.modal()).toBe('delete');
      expect(c.target().id).toBe('f-9');
    });

    it('does nothing with no target or while busy', () => {
      const c = create();
      c.doDelete();
      expect(files['delete']).not.toHaveBeenCalled();

      c.openDelete(row());
      c.isBusy.set(true);
      c.doDelete();
      expect(files['delete']).not.toHaveBeenCalled();
    });

    it('names the deleted file in the confirmation', () => {
      const c = create();
      c.openDelete(row({ id: 'f-9', name: 'old.txt' }));

      c.doDelete();

      expect(files['delete']).toHaveBeenCalledWith('f-9');
      expect(toaster.success.calls.mostRecent().args[0]).toContain('old.txt');
      expect(c.modal()).withContext('see the create-folder note').toBe('delete');
    });

    it('releases the button when the delete fails', () => {
      const c = create();
      files['delete'].and.returnValue(throwError(() => ({ status: 409 })));
      c.openDelete(row());

      c.doDelete();

      expect(c.isBusy()).toBeFalse();
    });
  });

  describe('closing a modal', () => {
    it('clears the target and the name field', () => {
      const c = create();
      c.openRename(row());
      c.closeModal();
      expect(c.modal()).toBeNull();
      expect(c.target()).toBeNull();
      expect(c.folderName()).toBe('');
    });

    it('refuses while an operation is in flight', () => {
      // Escape delegates here, so this guard is what stops a keypress discarding an
      // upload that is still running.
      const c = create();
      c.openRename(row());
      c.isBusy.set(true);

      c.closeModal();

      expect(c.modal()).toBe('rename');
    });
  });
});
