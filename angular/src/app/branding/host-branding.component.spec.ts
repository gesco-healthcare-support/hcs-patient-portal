import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';

import { HostBrandingComponent } from './host-branding.component';
import { BrandingService } from '../shared/branding/branding.service';

/**
 * Host-scope branding: the IT Admin's table of every office, with the display name and logo
 * editable per row.
 *
 * <p>It had NO spec and sat at 1 of 38 lines covered.</p>
 *
 * <p>It is the host-side twin of `office-branding`, which is covered separately, and it is the
 * SIMPLER of the two: two collaborators rather than four, and no authenticated blob preview, so
 * none of the object-URL machinery applies here.</p>
 *
 * <p>WHAT MAKES THIS ONE DIFFERENT IS SCOPE. The office component acts on the caller's own
 * office and sends no id at all. This one acts on whichever ROW the user clicked, so every call
 * carries `row.officeId`. Drop that argument and each method still compiles, still succeeds, and
 * writes to the wrong office -- the host's own scope -- with a success toast either way. Three
 * tests below exist only to pin the id onto the row.</p>
 *
 * <p>Every subscribe in this component has an error handler, so all of its failure paths are
 * exercised below.</p>
 *
 * <p>All office names and identifiers are synthetic.</p>
 */
describe('HostBrandingComponent', () => {
  let service: Record<string, jasmine.Spy>;
  let toaster: { success: jasmine.Spy; error: jasmine.Spy };

  interface Probe {
    [key: string]: any;
  }

  /** Two offices, so a test cannot pass by acting on "the only row there is". */
  const ROW_A = {
    officeId: 'office-a',
    officeName: 'First Practice',
    displayName: 'First',
    hasLogo: false,
  };
  const ROW_B = {
    officeId: 'office-b',
    officeName: 'Second Practice',
    displayName: null,
    hasLogo: true,
  };

  function create(): Probe {
    service = {
      getOfficesPaged: jasmine
        .createSpy('getOfficesPaged')
        .and.returnValue(of({ items: [ROW_A, ROW_B], totalCount: 2 })),
      setDisplayName: jasmine.createSpy('setDisplayName').and.returnValue(of({})),
      uploadLogo: jasmine.createSpy('uploadLogo').and.returnValue(of({})),
      removeLogo: jasmine.createSpy('removeLogo').and.returnValue(of(undefined)),
    };
    toaster = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };

    TestBed.configureTestingModule({
      providers: [
        { provide: BrandingService, useValue: service },
        { provide: ToasterService, useValue: toaster },
      ],
    });

    return TestBed.createComponent(HostBrandingComponent).componentInstance as unknown as Probe;
  }

  /** A file-picker change event carrying one file. */
  function pickEvent(name = 'logo.png'): Event {
    const input = document.createElement('input');
    input.type = 'file';
    Object.defineProperty(input, 'files', {
      value: [new File(['x'], name, { type: 'image/png' })],
      configurable: true,
    });
    return { target: input } as unknown as Event;
  }

  /** Count emissions on the table's refresh subject. */
  function watchReload(c: Probe): () => number {
    let count = 0;
    c.reload$.subscribe(() => (count += 1));
    return () => count;
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('the table', () => {
    it('declares the three columns the host needs, and sorts on the two text ones', () => {
      const c = create();
      const keys = (c.columns as { key: string }[]).map((col) => col.key);

      expect(keys).toEqual(['officeName', 'displayName', 'hasLogo']);
      expect((c.columns as { sortable?: boolean }[])[0].sortable).toBeTrue();
      expect((c.columns as { sortable?: boolean }[])[2].sortable)
        .withContext('a logo flag has no meaningful order')
        .toBeUndefined();
    });

    it('feeds the table straight from the paged endpoint', () => {
      const c = create();
      const q = { search: '', sorting: '', skipCount: 0, maxResultCount: 10 };

      c.dataSource(q);

      expect(service['getOfficesPaged']).toHaveBeenCalledWith(q);
    });
  });

  describe('the editable display-name cell', () => {
    it("shows the office's saved name when the user has not edited it", () => {
      const c = create();
      expect(c.displayNameFor(ROW_A)).toBe('First');
    });

    it('PREFERS an in-progress edit over the saved value', () => {
      // The table owns the rows and refetches; the buffer is the only place an unsaved
      // keystroke lives, so the saved value must not win it back.
      const c = create();
      c.names['office-a'] = 'Edited';

      expect(c.displayNameFor(ROW_A)).toBe('Edited');
    });

    it("keeps each office's edit separate", () => {
      const c = create();
      c.names['office-a'] = 'Edited';

      expect(c.displayNameFor(ROW_B)).withContext('B has no edit and no saved name').toBe('');
    });

    it('shows an empty box, not the word null, for an office with no display name', () => {
      const c = create();
      expect(c.displayNameFor(ROW_B)).toBe('');
    });

    it('treats a deliberately emptied box as an edit, not as absent', () => {
      const c = create();
      c.names['office-a'] = '';
      expect(c.displayNameFor(ROW_A)).toBe('');
    });
  });

  describe('saving a display name', () => {
    it('saves against the ROW the user clicked', () => {
      const c = create();
      c.names['office-b'] = 'Second';

      c.saveName(ROW_B);

      const [, officeId] = service['setDisplayName'].calls.mostRecent().args;
      expect(officeId).toBe('office-b');
    });

    it('sends the trimmed edit', () => {
      const c = create();
      c.names['office-a'] = '  Renamed  ';

      c.saveName(ROW_A);

      expect(service['setDisplayName']).toHaveBeenCalledWith('Renamed', 'office-a');
    });

    it('falls back to the saved value when the user never touched the box', () => {
      const c = create();
      c.saveName(ROW_A);
      expect(service['setDisplayName']).toHaveBeenCalledWith('First', 'office-a');
    });

    it('sends NULL for a blank name rather than an empty string', () => {
      // Null clears the override so the office falls back to its tenant name; an empty
      // string stores a blank display name and leaves the office unnamed everywhere.
      const c = create();
      c.names['office-a'] = '   ';

      c.saveName(ROW_A);

      expect(service['setDisplayName']).toHaveBeenCalledWith(null, 'office-a');
    });

    it('confirms and refreshes the table', () => {
      const c = create();
      const reloads = watchReload(c);

      c.saveName(ROW_A);

      expect(toaster.success).toHaveBeenCalledWith('Display name saved.');
      expect(reloads()).withContext('the table must refetch to show the new name').toBe(1);
      expect(c.busy()).toBeFalse();
    });

    it('does nothing while another write is running', () => {
      const c = create();
      c.busy.set(true);

      c.saveName(ROW_A);

      expect(service['setDisplayName']).not.toHaveBeenCalled();
    });

    it('releases the button and does NOT refresh when the save fails', () => {
      const c = create();
      const reloads = watchReload(c);
      service['setDisplayName'].and.returnValue(throwError(() => ({ status: 500 })));

      c.saveName(ROW_A);

      expect(c.busy()).withContext('finalize still runs').toBeFalse();
      expect(toaster.success).not.toHaveBeenCalled();
      expect(reloads()).withContext('nothing changed, so nothing to refetch').toBe(0);
    });
  });

  describe('uploading a logo', () => {
    it('uploads against the ROW the user clicked', () => {
      const c = create();

      c.onLogoSelected(ROW_B, pickEvent());

      const [, officeId] = service['uploadLogo'].calls.mostRecent().args;
      expect(officeId).toBe('office-b');
    });

    it('sends the chosen file and confirms', () => {
      const c = create();
      const reloads = watchReload(c);

      c.onLogoSelected(ROW_A, pickEvent('practice-logo.png'));

      expect(service['uploadLogo'].calls.mostRecent().args[0].name).toBe('practice-logo.png');
      expect(toaster.success).toHaveBeenCalledWith('Logo uploaded.');
      expect(reloads()).toBe(1);
      expect(c.busy()).toBeFalse();
    });

    it('does nothing when the picker was dismissed', () => {
      const c = create();
      const input = document.createElement('input');
      Object.defineProperty(input, 'files', { value: [], configurable: true });

      c.onLogoSelected(ROW_A, { target: input } as unknown as Event);

      expect(service['uploadLogo']).not.toHaveBeenCalled();
    });

    it('does nothing while another write is running', () => {
      const c = create();
      c.busy.set(true);

      c.onLogoSelected(ROW_A, pickEvent());

      expect(service['uploadLogo']).not.toHaveBeenCalled();
    });

    it('CLEARS the picker on success so the same file can be chosen again', () => {
      // Re-picking an identical file fires no change event unless the input is cleared.
      const c = create();
      const event = pickEvent();

      c.onLogoSelected(ROW_A, event);

      expect((event.target as HTMLInputElement).value).toBe('');
    });

    it('clears the picker on failure too, and releases the button', () => {
      const c = create();
      service['uploadLogo'].and.returnValue(throwError(() => ({ status: 413 })));
      const event = pickEvent();

      c.onLogoSelected(ROW_A, event);

      expect((event.target as HTMLInputElement).value).toBe('');
      expect(c.busy()).toBeFalse();
      expect(toaster.success).not.toHaveBeenCalled();
    });
  });

  describe('removing a logo', () => {
    it('removes against the ROW the user clicked', () => {
      const c = create();

      c.removeLogo(ROW_B);

      expect(service['removeLogo']).toHaveBeenCalledWith('office-b');
    });

    it('confirms and refreshes the table', () => {
      const c = create();
      const reloads = watchReload(c);

      c.removeLogo(ROW_A);

      expect(toaster.success).toHaveBeenCalledWith('Logo removed.');
      expect(reloads()).toBe(1);
      expect(c.busy()).toBeFalse();
    });

    it('does nothing while another write is running', () => {
      const c = create();
      c.busy.set(true);

      c.removeLogo(ROW_A);

      expect(service['removeLogo']).not.toHaveBeenCalled();
    });

    it('releases the button when the removal fails', () => {
      const c = create();
      const reloads = watchReload(c);
      service['removeLogo'].and.returnValue(throwError(() => ({ status: 500 })));

      c.removeLogo(ROW_A);

      expect(c.busy()).toBeFalse();
      expect(toaster.success).not.toHaveBeenCalled();
      expect(reloads()).toBe(0);
    });
  });
});
