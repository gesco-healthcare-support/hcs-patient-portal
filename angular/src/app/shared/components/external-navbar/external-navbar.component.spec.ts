import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ExternalNavbarComponent } from './external-navbar.component';
import { BrandingService } from '../../branding/branding.service';

// Stub the branding service so the navbar's DI does not pull in RestService
// (and its ABP CORE_OPTIONS) for this pure initials-logic spec.
const brandingStub = {
  displayName: () => null,
  logoUrl: () => null,
} as unknown as BrandingService;

/**
 * F-008 regression: the external navbar avatar initials. A firm name must use
 * its first two words ("Stone & Perez Defense LLP" -> "SP"), not the trailing
 * suffix ("...LLP"); a person keeps first + last initial. The "&" connector and
 * leading punctuation must never become an initial.
 */
describe('ExternalNavbarComponent initials (F-008)', () => {
  let fixture: ComponentFixture<ExternalNavbarComponent>;
  let component: ExternalNavbarComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExternalNavbarComponent],
      providers: [{ provide: BrandingService, useValue: brandingStub }],
    }).compileComponents();
    fixture = TestBed.createComponent(ExternalNavbarComponent);
    component = fixture.componentInstance;
  });

  function initialsFor(userName: string, orgName: string | null): string {
    component.userName = userName;
    component.orgName = orgName;
    return (component as unknown as { initials: string }).initials;
  }

  it('uses the first two words for a firm name (not the suffix)', () => {
    // Firm avatar: display name equals the org name.
    expect(initialsFor('Stone & Perez Defense LLP', 'Stone & Perez Defense LLP')).toBe('SP');
  });

  it('uses first + last initial for a person', () => {
    expect(initialsFor('Marcus James Bennett', null)).toBe('MB');
  });

  it('falls back to a single initial for a one-word name', () => {
    expect(initialsFor('Cher', null)).toBe('C');
  });

  it('returns a placeholder when there is no usable name', () => {
    expect(initialsFor('', null)).toBe('?');
  });
});

/**
 * Sweep #640: the avatar colour hash moved from `charCodeAt(0)` to `codePointAt(0)`.
 *
 * <p>The loop is `for...of`, which yields whole code points, so `charCodeAt(0)` was reading
 * only the lead surrogate of an astral character -- two names differing solely in an emoji
 * hashed identically. (The opposite call is right in `users-hub.util.ts`, which steps by code
 * UNIT and refuses the same rule.)</p>
 */
describe('ExternalNavbarComponent avatar colour (sweep #640)', () => {
  function colourFor(name: string): string {
    const fixture = TestBed.createComponent(ExternalNavbarComponent);
    const cmp = fixture.componentInstance as unknown as {
      userName: string;
      avatarColor: string;
    };
    cmp.userName = name;
    return cmp.avatarColor;
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExternalNavbarComponent],
      providers: [{ provide: BrandingService, useValue: brandingStub }],
    }).compileComponents();
  });

  it('returns a palette colour', () => {
    expect(colourFor('Ada Lovelace')).toMatch(/^#[0-9a-f]{6}$/);
  });

  it('is deterministic for the same name', () => {
    expect(colourFor('Ada Lovelace')).toBe(colourFor('Ada Lovelace'));
  });

  it('distinguishes two names that differ only by an astral character', () => {
    // Under charCodeAt(0) both hashed on the same lead surrogate and collided.
    expect(colourFor('A\u{1F600}')).not.toBe(colourFor('A\u{1F602}'));
  });

  it('copes with an empty name', () => {
    expect(colourFor('')).toMatch(/^#[0-9a-f]{6}$/);
  });
});
