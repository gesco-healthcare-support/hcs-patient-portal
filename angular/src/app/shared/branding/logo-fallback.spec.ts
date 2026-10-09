import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { BrandingService } from './branding.service';
import { PLACEHOLDER_LOGO } from './brand-assets';
import { ExternalNavbarComponent } from '../components/external-navbar/external-navbar.component';
import { TopHeaderNavbarComponent } from '../components/top-header-navbar/top-header-navbar.component';
import { StateMessageComponent } from '../ui/state-message/state-message.component';

/**
 * Q12: an office with no uploaded logo must show the neutral placeholder, never a real
 * practice's logo; an uploaded logo still wins. Each former Falkinstein default is checked.
 */
describe('logo fallback', () => {
  const UPLOADED = 'https://api.example.test/api/app/branding/logo/o1';
  let logo: ReturnType<typeof signal<string | null>>;

  function brandingStub(): BrandingService {
    logo = signal<string | null>(null);
    return { logoUrl: logo, displayName: () => null } as unknown as BrandingService;
  }

  function img(fixture: { nativeElement: HTMLElement }, selector: string): HTMLImageElement {
    return fixture.nativeElement.querySelector(selector) as HTMLImageElement;
  }

  afterEach(() => TestBed.resetTestingModule());

  it('placeholder is a neutral asset, not a practice logo', () => {
    expect(PLACEHOLDER_LOGO).toBe('assets/branding/portal-placeholder.svg');
    expect(PLACEHOLDER_LOGO).not.toMatch(/falkinstein/i);
  });

  it('external navbar: placeholder, then uploaded logo', () => {
    TestBed.configureTestingModule({
      imports: [ExternalNavbarComponent],
      providers: [{ provide: BrandingService, useValue: brandingStub() }],
    });
    const fixture = TestBed.createComponent(ExternalNavbarComponent);
    fixture.detectChanges();
    expect(img(fixture, '.ext-brand img').getAttribute('src')).toBe(PLACEHOLDER_LOGO);
    expect(img(fixture, '.ext-brand img').classList).toContain('brand-logo');

    logo.set(UPLOADED);
    fixture.detectChanges();
    expect(img(fixture, '.ext-brand img').getAttribute('src')).toBe(UPLOADED);
  });

  it('top header navbar: placeholder, then uploaded logo', () => {
    TestBed.configureTestingModule({
      imports: [TopHeaderNavbarComponent],
      providers: [{ provide: BrandingService, useValue: brandingStub() }],
    });
    const fixture = TestBed.createComponent(TopHeaderNavbarComponent);
    fixture.detectChanges();
    expect(img(fixture, '.top-header-navbar__logo').getAttribute('src')).toBe(PLACEHOLDER_LOGO);

    logo.set(UPLOADED);
    fixture.detectChanges();
    expect(img(fixture, '.top-header-navbar__logo').getAttribute('src')).toBe(UPLOADED);
  });

  it('state message screens default to the placeholder and accept an override', () => {
    TestBed.configureTestingModule({ imports: [StateMessageComponent] });
    const fixture = TestBed.createComponent(StateMessageComponent);
    fixture.detectChanges();
    expect(img(fixture, '.pp-top img').getAttribute('src')).toBe(PLACEHOLDER_LOGO);

    fixture.componentRef.setInput('logoUrl', UPLOADED);
    fixture.detectChanges();
    expect(img(fixture, '.pp-top img').getAttribute('src')).toBe(UPLOADED);
  });

  it('the display box constrains every logo to one height and a max width', () => {
    TestBed.configureTestingModule({
      imports: [TopHeaderNavbarComponent],
      providers: [{ provide: BrandingService, useValue: brandingStub() }],
    });
    const fixture = TestBed.createComponent(TopHeaderNavbarComponent);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    const style = getComputedStyle(img(fixture, '.top-header-navbar__logo'));
    expect(style.objectFit).toBe('contain');
    expect(style.maxWidth).not.toBe('none');
    expect(parseFloat(style.height)).toBeGreaterThan(0);
    fixture.nativeElement.remove();
  });
});
