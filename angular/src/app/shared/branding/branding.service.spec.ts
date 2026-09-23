import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { EnvironmentService, RestService } from '@abp/ng.core';
import { Observable, of, throwError } from 'rxjs';
import { BrandingService } from './branding.service';

/**
 * Per-office branding: the boot-time fetch that names the tab and the navbars, and the host-side
 * manager's reads and writes.
 *
 * The boot fetch is best-effort by design -- a failure must leave the defaults in place rather than
 * block the shell -- so the failure path is pinned alongside the success path. The logo path comes
 * back RELATIVE and is resolved against the runtime API base; that join is where a doubled or
 * missing slash would silently break every logo, so it is pinned with a base that ends in one.
 */
describe('BrandingService', () => {
  let request: jasmine.Spy;
  let setTitle: jasmine.Spy;
  let apiUrl: string | undefined;

  interface Call {
    method: string;
    url: string;
    params?: Record<string, unknown>;
    body?: unknown;
  }

  function service(response: Observable<unknown> = of({ hasLogo: false })): BrandingService {
    request = jasmine.createSpy('request').and.returnValue(response);
    setTitle = jasmine.createSpy('setTitle');
    TestBed.configureTestingModule({
      providers: [
        { provide: RestService, useValue: { request } },
        { provide: EnvironmentService, useValue: { getApiUrl: () => apiUrl } },
        { provide: Title, useValue: { setTitle } },
      ],
    });
    return TestBed.inject(BrandingService);
  }

  function lastCall(): Call {
    return request.calls.mostRecent().args[0] as Call;
  }

  beforeEach(() => (apiUrl = 'https://api.example.test/'));
  afterEach(() => TestBed.resetTestingModule());

  describe('the boot-time fetch', () => {
    it('asks the anonymous current-office endpoint', () => {
      service().load();

      expect(lastCall().method).toBe('GET');
      expect(lastCall().url).toBe('/api/app/branding');
    });

    it('publishes the trimmed display name and names the tab after it', () => {
      const svc = service(of({ displayName: '  Example Clinic  ', hasLogo: false }));

      svc.load();

      expect(svc.displayName()).toBe('Example Clinic');
      expect(setTitle).toHaveBeenCalledWith('Example Clinic');
    });

    it('leaves the tab title alone when the display name is blank', () => {
      const svc = service(of({ displayName: '   ', hasLogo: false }));

      svc.load();

      expect(svc.displayName()).toBeNull();
      expect(setTitle).not.toHaveBeenCalled();
    });

    it('resolves a relative logo path against the API base without doubling the slash', () => {
      const svc = service(of({ hasLogo: true, logoUrl: '/api/app/branding/logo/office-1' }));

      svc.load();

      expect(svc.logoUrl()).toBe('https://api.example.test/api/app/branding/logo/office-1');
    });

    it('keeps an absolute logo URL as it is', () => {
      const svc = service(of({ hasLogo: true, logoUrl: 'https://cdn.example.test/logo.png' }));

      svc.load();

      expect(svc.logoUrl()).toBe('https://cdn.example.test/logo.png');
    });

    it('falls back to a root-relative logo path when no API base is configured', () => {
      apiUrl = undefined;
      const svc = service(of({ hasLogo: true, logoUrl: 'api/app/branding/logo/office-1' }));

      svc.load();

      expect(svc.logoUrl()).toBe('/api/app/branding/logo/office-1');
    });

    it('publishes no logo when the office has none', () => {
      const svc = service(of({ hasLogo: false, logoUrl: null }));

      svc.load();

      expect(svc.logoUrl()).toBeNull();
    });

    it('keeps the defaults when the fetch fails, rather than blocking the shell', () => {
      const svc = service(throwError(() => new Error('offline')));

      svc.load();

      expect(svc.displayName()).toBeNull();
      expect(svc.logoUrl()).toBeNull();
      expect(setTitle).not.toHaveBeenCalled();
    });
  });

  describe('the host-side manager', () => {
    it('lists every office with its branding', () => {
      service().getOffices();

      expect(lastCall().method).toBe('GET');
      expect(lastCall().url).toBe('/api/app/branding/offices');
    });

    it('pages offices with the trimmed search, the sort and the window', () => {
      service().getOfficesPaged({
        search: '  down ',
        sorting: 'officeName',
        skipCount: 20,
        maxResultCount: 10,
      } as never);

      expect(lastCall().url).toBe('/api/app/branding/offices-paged');
      expect(lastCall().params).toEqual({
        filter: 'down',
        sorting: 'officeName',
        skipCount: 20,
        maxResultCount: 10,
      });
    });

    it('sends no filter or sort when they are blank, and maps a missing page to an empty one', () => {
      const svc = service(of({ items: null, totalCount: null }));
      let page: unknown;

      svc
        .getOfficesPaged({ search: '   ', sorting: '', skipCount: 0, maxResultCount: 10 } as never)
        .subscribe((p) => (page = p));

      expect(lastCall().params?.['filter']).toBeUndefined();
      expect(lastCall().params?.['sorting']).toBeUndefined();
      expect(page).toEqual({ items: [], totalCount: 0 });
    });

    it('sets the display name for a named office', () => {
      service().setDisplayName('Example Clinic', 'office-1');

      expect(lastCall().method).toBe('PUT');
      expect(lastCall().url).toBe('/api/app/branding/display-name');
      expect(lastCall().params).toEqual({ officeId: 'office-1' });
      expect(lastCall().body).toEqual({ displayName: 'Example Clinic' });
    });

    it('targets the current office when no office is named', () => {
      service().setDisplayName(null);

      expect(lastCall().params).toEqual({});
      expect(lastCall().body).toEqual({ displayName: null });
    });

    it('uploads a logo as a multipart file for the named office', () => {
      const file = new File(['logo'], 'logo.png', { type: 'image/png' });

      service().uploadLogo(file, 'office-1');

      expect(lastCall().method).toBe('POST');
      expect(lastCall().url).toBe('/api/app/branding/logo');
      expect(lastCall().params).toEqual({ officeId: 'office-1' });
      expect((lastCall().body as FormData).get('file')).toBeInstanceOf(File);
    });

    it('removes the logo of the current office, or of a named one', () => {
      const svc = service();

      svc.removeLogo();
      expect(lastCall().method).toBe('DELETE');
      expect(lastCall().url).toBe('/api/app/branding/logo');
      expect(lastCall().params).toEqual({});

      svc.removeLogo('office-1');
      expect(lastCall().params).toEqual({ officeId: 'office-1' });
    });
  });
});
