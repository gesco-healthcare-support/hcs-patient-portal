import { ComponentFixture, TestBed } from '@angular/core/testing';
import { IconComponent } from './icon.component';

describe('IconComponent', () => {
  let fixture: ComponentFixture<IconComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [IconComponent] }).compileComponents();
    fixture = TestBed.createComponent(IconComponent);
  });

  function svgEl(): SVGSVGElement | null {
    return fixture.nativeElement.querySelector('svg');
  }

  it('renders the registry markup for the given name inside an svg shell', () => {
    fixture.componentRef.setInput('name', 'bell');
    fixture.detectChanges();

    const svg = svgEl();
    expect(svg).toBeTruthy();
    expect(svg!.getAttribute('stroke')).toBe('currentColor');
    expect(svg!.getAttribute('stroke-width')).toBe('1.8');
    // The bell-specific path fragment must be present.
    expect(fixture.nativeElement.innerHTML).toContain('M18 8a6 6 0 1 0-12 0');
  });

  it('applies size to both width and height (default 18)', () => {
    fixture.componentRef.setInput('name', 'search');
    fixture.detectChanges();
    expect(svgEl()!.getAttribute('width')).toBe('18');

    fixture.componentRef.setInput('size', 28);
    fixture.detectChanges();
    const svg = svgEl()!;
    expect(svg.getAttribute('width')).toBe('28');
    expect(svg.getAttribute('height')).toBe('28');
  });

  it('is decorative by default, and exposed to AT when a label is set', () => {
    fixture.componentRef.setInput('name', 'logout');
    fixture.detectChanges();
    expect(svgEl()!.getAttribute('aria-hidden')).toBe('true');

    fixture.componentRef.setInput('label', 'Sign out');
    fixture.detectChanges();
    const svg = svgEl()!;
    expect(svg.getAttribute('role')).toBe('img');
    expect(svg.getAttribute('aria-label')).toBe('Sign out');
    expect(svg.getAttribute('aria-hidden')).toBeNull();
  });
});
