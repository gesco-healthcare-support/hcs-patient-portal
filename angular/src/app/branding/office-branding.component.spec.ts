import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { EnvironmentService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { OfficeBrandingComponent } from './office-branding.component';
import { BrandingService } from '../shared/branding/branding.service';

/**
 * Per-office branding: the display name and the office logo.
 *
 * <p>It had NO spec and sat at 1 of 63 lines covered.</p>
 *
 * <p>The interesting part is the logo preview, and the component doc says why it is not a plain
 * `<img src>`: the logo has to be fetched WITH the bearer token attached, so the server resolves
 * the impersonated office rather than the host. That makes it an authenticated blob fetch plus an
 * object URL, and object URLs leak unless they are revoked -- on replacement AND on destroy. Both
 * are pinned here, because a leak of this kind is invisible until a long session runs out of
 * memory.</p>
 *
 * <p>Every subscribe in this component has an error handler, so all of its failure paths are
 * safe to exercise -- unlike the two profile pages in this same tranche.</p>
 *
 * <p>All office names and URLs below are synthetic.</p>
 */
describe('OfficeBrandingComponent', () => {
  let branding: Record<string, jasmine.Spy>;
  let http: { get: jasmine.Spy };
  let toaster: { success: jasmine.Spy; error: jasmine.Spy };
  let created: string[];
  let revoked: string[];
  let current: unknown;
  let currentFails: boolean;

  interface Probe {
    [key: string]: any;
  }

  const API = 'https://api.test';

  /**
   * Builds the collaborators and the component.
   *
   * <p>It deliberately does NOT touch `current` or `currentFails`. Those are the fixture, and
   * the component loads its branding IN THE CONSTRUCTOR, so a test has to arm them before
   * calling this. Resetting them here made every such test silently get the default instead --
   * seven failures with seven different messages and one cause. Defaults live in beforeEach.</p>
   */
  function create(): Probe {
    branding = {
      getCurrent: jasmine
        .createSpy('getCurrent')
        .and.callFake(() => (currentFails ? throwError(() => ({ status: 500 })) : of(current))),
      setDisplayName: jasmine.createSpy('setDisplayName').and.returnValue(of({})),
      uploadLogo: jasmine
        .createSpy('uploadLogo')
        .and.returnValue(of({ displayName: 'Example Practice', hasLogo: true, logoUrl: 'l.png' })),
      removeLogo: jasmine.createSpy('removeLogo').and.returnValue(of(undefined)),
      load: jasmine.createSpy('load'),
    };
    http = { get: jasmine.createSpy('get').and.returnValue(of(new Blob(['x']))) };
    toaster = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };

    TestBed.configureTestingModule({
      providers: [
        { provide: BrandingService, useValue: branding },
        { provide: HttpClient, useValue: http },
        { provide: ToasterService, useValue: toaster },
        { provide: EnvironmentService, useValue: { getApiUrl: () => API } },
      ],
    });

    return TestBed.createComponent(OfficeBrandingComponent).componentInstance as unknown as Probe;
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

  beforeEach(() => {
    // The fixture, set here rather than in create() so a test can arm it BEFORE the
    // component is constructed -- which is when the branding load runs.
    created = [];
    revoked = [];
    current = { displayName: 'Example Practice', hasLogo: false };
    currentFails = false;

    // Track object URLs so the revoke discipline can be asserted without a real blob store.
    spyOn(URL, 'createObjectURL').and.callFake(() => {
      const url = `blob:test/${created.length}`;
      created.push(url);
      return url;
    });
    spyOn(URL, 'revokeObjectURL').and.callFake((u: string) => {
      revoked.push(u);
    });
  });

  afterEach(() => TestBed.resetTestingModule());

  describe('initial load', () => {
    it('reads the current branding and clears the loading flag', () => {
      const c = create();
      expect(branding['getCurrent']).toHaveBeenCalled();
      expect(c.displayName).toBe('Example Practice');
      expect(c.loading()).toBeFalse();
    });

    it('treats a null branding payload as no name and no logo', () => {
      current = null;
      const c = create();
      expect(c.displayName).toBe('');
      expect(c.hasLogo()).toBeFalse();
    });

    it('still clears loading when the branding request fails', () => {
      // finalize owns the flag, so a failed load must not leave the page spinning with
      // no controls. The failure is armed BEFORE construction, because the load runs in
      // the constructor.
      currentFails = true;
      const c = create();

      expect(c.loading()).toBeFalse();
      expect(c.hasLogo()).toBeFalse();
      expect(c.displayName).toBe('');
    });
  });

  describe('the authenticated logo preview', () => {
    it('does not fetch anything when the office has no logo', () => {
      create();
      expect(http.get).not.toHaveBeenCalled();
    });

    it('fetches the logo as a blob, with the bearer attached by the interceptor', () => {
      /**
       * A plain img src would go out unauthenticated and resolve the HOST office rather
       * than the impersonated one, so the wrong logo would appear with no error.
       */
      current = { displayName: 'Example Practice', hasLogo: true, logoUrl: 'logo.png' };
      const c = create();

      expect(http.get).toHaveBeenCalled();
      expect(http.get.calls.mostRecent().args[1].responseType).toBe('blob');
      expect(c.logoPreview()).toBe(created[0]);
    });

    it('joins a relative logo path onto the API base', () => {
      current = { displayName: 'x', hasLogo: true, logoUrl: '/logo.png' };
      create();
      expect(http.get.calls.mostRecent().args[0]).toBe(`${API}/logo.png`);
    });

    it('leaves an absolute logo URL alone', () => {
      current = { displayName: 'x', hasLogo: true, logoUrl: 'https://cdn.example.test/l.png' };
      create();
      expect(http.get.calls.mostRecent().args[0]).toBe('https://cdn.example.test/l.png');
    });

    it('shows no preview when the logo fetch fails', () => {
      current = { displayName: 'x', hasLogo: true, logoUrl: 'logo.png' };
      const c = create();
      http.get.and.returnValue(throwError(() => ({ status: 404 })));
      c.onLogoSelected(pickEvent());
      expect(c.logoPreview()).toBeNull();
    });

    it('REVOKES the previous object URL when the logo is replaced', () => {
      // Each preview allocates a blob URL; replacing without revoking leaks one per
      // upload for the lifetime of the page.
      current = { displayName: 'x', hasLogo: true, logoUrl: 'logo.png' };
      const c = create();
      const first = c.logoPreview();

      c.onLogoSelected(pickEvent());

      expect(revoked).toContain(first as string);
      expect(c.logoPreview()).not.toBe(first);
    });

    it('revokes the outstanding object URL when the page is destroyed', () => {
      current = { displayName: 'x', hasLogo: true, logoUrl: 'logo.png' };
      const c = create();
      const url = c.logoPreview();

      c.ngOnDestroy();

      expect(revoked).toContain(url as string);
    });

    it('has nothing to revoke when no preview was ever created', () => {
      const c = create();
      expect(() => c.ngOnDestroy()).not.toThrow();
      expect(revoked).toEqual([]);
    });
  });

  describe('saving the display name', () => {
    it('does nothing while another write is running', () => {
      const c = create();
      c.busy.set(true);
      c.saveName();
      expect(branding['setDisplayName']).not.toHaveBeenCalled();
    });

    it('sends the trimmed name', () => {
      const c = create();
      c.displayName = '  Example Practice  ';
      c.saveName();
      expect(branding['setDisplayName']).toHaveBeenCalledWith('Example Practice');
    });

    it('sends NULL for a blank name rather than an empty string', () => {
      // Null clears the override and falls back to the tenant name; an empty string
      // would store a blank display name and leave the office unnamed everywhere.
      const c = create();
      c.displayName = '   ';
      c.saveName();
      expect(branding['setDisplayName']).toHaveBeenCalledWith(null);
    });

    it('confirms and refreshes the shared branding state', () => {
      // branding.load() is what repaints the name in the shell chrome.
      const c = create();
      c.saveName();
      expect(toaster.success).toHaveBeenCalledWith('Display name saved.');
      expect(branding['load']).toHaveBeenCalled();
      expect(c.busy()).toBeFalse();
    });

    it('releases the button when the save fails', () => {
      const c = create();
      branding['setDisplayName'].and.returnValue(throwError(() => ({ status: 500 })));
      c.saveName();
      expect(c.busy()).toBeFalse();
      expect(toaster.success).not.toHaveBeenCalled();
    });
  });

  describe('uploading a logo', () => {
    it('does nothing when the picker was dismissed', () => {
      const c = create();
      const input = document.createElement('input');
      Object.defineProperty(input, 'files', { value: [], configurable: true });
      c.onLogoSelected({ target: input } as unknown as Event);
      expect(branding['uploadLogo']).not.toHaveBeenCalled();
    });

    it('does nothing while another write is running', () => {
      const c = create();
      c.busy.set(true);
      c.onLogoSelected(pickEvent());
      expect(branding['uploadLogo']).not.toHaveBeenCalled();
    });

    it('uploads the chosen file and confirms', () => {
      const c = create();
      c.onLogoSelected(pickEvent());
      expect(branding['uploadLogo']).toHaveBeenCalled();
      expect(branding['uploadLogo'].calls.mostRecent().args[0].name).toBe('logo.png');
      expect(toaster.success).toHaveBeenCalledWith('Logo uploaded.');
      expect(branding['load']).toHaveBeenCalled();
      expect(c.busy()).toBeFalse();
    });

    it('adopts the logo state from the upload response', () => {
      const c = create();
      expect(c.hasLogo()).toBeFalse();
      c.onLogoSelected(pickEvent());
      expect(c.hasLogo()).toBeTrue();
    });

    it('CLEARS the picker on success so the same file can be chosen again', () => {
      // Re-picking an identical file fires no change event unless the input is cleared.
      const c = create();
      const event = pickEvent();
      c.onLogoSelected(event);
      expect((event.target as HTMLInputElement).value).toBe('');
    });

    it('clears the picker on failure too, and releases the button', () => {
      const c = create();
      branding['uploadLogo'].and.returnValue(throwError(() => ({ status: 413 })));
      const event = pickEvent();

      c.onLogoSelected(event);

      expect((event.target as HTMLInputElement).value).toBe('');
      expect(c.busy()).toBeFalse();
    });
  });

  describe('removing the logo', () => {
    it('does nothing while another write is running', () => {
      const c = create();
      c.busy.set(true);
      c.removeLogo();
      expect(branding['removeLogo']).not.toHaveBeenCalled();
    });

    it('clears the flag, the preview and the object URL', () => {
      current = { displayName: 'x', hasLogo: true, logoUrl: 'logo.png' };
      const c = create();
      const url = c.logoPreview();

      c.removeLogo();

      expect(c.hasLogo()).toBeFalse();
      expect(c.logoPreview()).toBeNull();
      expect(revoked).toContain(url as string);
    });

    it('confirms and refreshes the shared branding state', () => {
      const c = create();
      c.removeLogo();
      expect(toaster.success).toHaveBeenCalledWith('Logo removed.');
      expect(branding['load']).toHaveBeenCalled();
      expect(c.busy()).toBeFalse();
    });

    it('releases the button when the removal fails', () => {
      const c = create();
      branding['removeLogo'].and.returnValue(throwError(() => ({ status: 500 })));
      c.removeLogo();
      expect(c.busy()).toBeFalse();
      expect(toaster.success).not.toHaveBeenCalled();
    });
  });
});
