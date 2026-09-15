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

  /**
   * Production hardening (task 1.2, 2026-09-01) -- Sonar typescript:S6268.
   * This component calls bypassSecurityTrustHtml, so Angular does NOT sanitize its
   * output and every interpolated value has to be provably markup-free on its own.
   *
   * `name` reaches here from server data: DashboardActivityItemDto.icon (string) ->
   * internal-dashboard.component.ts:291 casts it to IconName unchecked ->
   * [name]="icon(a.icon)". `size` and `label` have no live data path today; their
   * cases below are defence-in-depth on a latent one, not fixes for live defects.
   */
  describe('S6268 -- nothing outside the registry reaches the trusted markup', () => {
    /** Nothing may render for a name that is not an own key of the registry. */
    function expectEmptyShell(): void {
      const svg = svgEl();
      expect(svg).toBeTruthy();
      expect(svg!.innerHTML).toBe('');
      expect(svg!.textContent).toBe('');
    }

    it('renders nothing for a script payload as the name', () => {
      fixture.componentRef.setInput('name', '<script>alert(1)</script>');
      fixture.detectChanges();

      expectEmptyShell();
      expect(fixture.nativeElement.querySelector('script')).toBeNull();
      expect(fixture.nativeElement.innerHTML).not.toContain('alert(1)');
    });

    it('renders nothing for an unknown but harmless name', () => {
      fixture.componentRef.setInput('name', 'nosuchicon');
      fixture.detectChanges();

      expectEmptyShell();
    });

    /**
     * ICON_PATHS is a plain object literal, so it inherits from Object.prototype.
     * A lookup keyed on an inherited member returns a function or object rather than
     * undefined, which `?? ''` cannot catch -- it is not null or undefined. Each of
     * these renders native-code text into the svg before the fix.
     */
    ['constructor', '__proto__', 'toString', 'valueOf', 'hasOwnProperty'].forEach((key) => {
      it(`renders nothing for the inherited member "${key}" as the name`, () => {
        fixture.componentRef.setInput('name', key);
        fixture.detectChanges();

        expectEmptyShell();
        expect(fixture.nativeElement.innerHTML).not.toContain('native code');
        expect(fixture.nativeElement.innerHTML).not.toContain('[object Object]');
      });
    });

    /**
     * `size` is interpolated into width/height with no escaping at all. The `number`
     * annotation is erased at runtime, so a caller binding it from data could close
     * the attribute and add its own. No call site does today; this pins that it
     * cannot matter if one ever does.
     */
    it('cannot inject an attribute through a non-numeric size', () => {
      fixture.componentRef.setInput('name', 'bell');
      fixture.componentRef.setInput('size', '18" onload="alert(1)');
      fixture.detectChanges();

      const svg = svgEl()!;
      expect(svg.hasAttribute('onload')).toBe(false);
      expect(fixture.nativeElement.innerHTML).not.toContain('onload');
      expect(svg.getAttribute('width')).toBe('18');
      expect(svg.getAttribute('height')).toBe('18');
    });

    it('falls back to the default size for values that are not usable lengths', () => {
      fixture.componentRef.setInput('name', 'bell');

      for (const bad of ['abc', Number.NaN, 0, -5, Number.POSITIVE_INFINITY]) {
        fixture.componentRef.setInput('size', bad);
        fixture.detectChanges();
        expect(svgEl()!.getAttribute('width')).toBe('18');
      }
    });

    it('still honours a legitimate numeric size given as a string', () => {
      fixture.componentRef.setInput('name', 'bell');
      fixture.componentRef.setInput('size', '24');
      fixture.detectChanges();

      expect(svgEl()!.getAttribute('width')).toBe('24');
    });

    it('escapes a label so it cannot close its attribute', () => {
      fixture.componentRef.setInput('name', 'bell');
      fixture.componentRef.setInput('label', '" onload="alert(1)');
      fixture.detectChanges();

      const svg = svgEl()!;
      expect(svg.hasAttribute('onload')).toBe(false);
      expect(svg.getAttribute('aria-label')).toBe('" onload="alert(1)');
    });
  });
});
