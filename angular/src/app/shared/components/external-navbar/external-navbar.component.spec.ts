import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ExternalNavbarComponent } from './external-navbar.component';

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
