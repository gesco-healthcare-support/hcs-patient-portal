import { TestBed } from '@angular/core/testing';

import { TopHeaderNavbarComponent } from './top-header-navbar.component';
import { BrandingService } from '../../branding/branding.service';

/**
 * The top header bar: it only raises its three clicks; the host decides what they do.
 *
 * <p>It had no spec. It injects the branding service for its logo, so it is built in an injection
 * context with a stub, and never rendered.</p>
 */
describe('TopHeaderNavbarComponent', () => {
  function create(): TopHeaderNavbarComponent {
    TestBed.configureTestingModule({
      providers: [{ provide: BrandingService, useValue: { displayName: () => null } }],
    });
    return TestBed.runInInjectionContext(() => new TopHeaderNavbarComponent());
  }

  afterEach(() => TestBed.resetTestingModule());

  it('raises each click on its own output and no other', () => {
    const c = create();
    const seen: string[] = [];
    c.profileClick.subscribe(() => seen.push('profile'));
    c.helpClick.subscribe(() => seen.push('help'));
    c.logoutClick.subscribe(() => seen.push('logout'));

    c.onHelpClick();
    c.onLogoutClick();
    c.onProfileClick();

    expect(seen).toEqual(['help', 'logout', 'profile']);
  });

  it('shows all three actions by default', () => {
    const c = create();
    expect(c.showProfile).toBeTrue();
    expect(c.showHelp).toBeTrue();
    expect(c.showLogout).toBeTrue();
  });
});
