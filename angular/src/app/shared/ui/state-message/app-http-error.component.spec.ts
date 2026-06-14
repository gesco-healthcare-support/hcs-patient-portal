import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Subject } from 'rxjs';
import { AppHttpErrorComponent } from './app-http-error.component';

describe('AppHttpErrorComponent', () => {
  let fixture: ComponentFixture<AppHttpErrorComponent>;
  let component: AppHttpErrorComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppHttpErrorComponent],
      providers: [provideRouter([])],
    }).compileComponents();
    fixture = TestBed.createComponent(AppHttpErrorComponent);
    component = fixture.componentInstance;
  });

  function renderStatus(status: number): HTMLElement {
    component.status.set(status);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('maps 404 to the not-found screen (blue, back-to-home)', () => {
    const el = renderStatus(404);
    expect(el.querySelector('.pp-card h1')?.textContent).toContain('Page not found');
    expect(el.querySelector('.pp-ic')?.classList.contains('blue')).toBe(true);
    expect(el.querySelector('.ap-btn')?.textContent).toContain('Back to home');
  });

  it('maps 403 to the access-denied screen (red)', () => {
    const el = renderStatus(403);
    expect(el.querySelector('.pp-card h1')?.textContent).toContain("don't have access");
    expect(el.querySelector('.pp-ic')?.classList.contains('red')).toBe(true);
  });

  it('maps 401 to the session-timeout screen (amber, sign in again)', () => {
    const el = renderStatus(401);
    expect(el.querySelector('.pp-card h1')?.textContent).toContain('session has expired');
    expect(el.querySelector('.pp-ic')?.classList.contains('amber')).toBe(true);
    expect(el.querySelector('.ap-btn')?.textContent).toContain('Sign in again');
  });

  it('maps 500 to the generic error screen with a retry action', () => {
    const el = renderStatus(500);
    expect(el.querySelector('.pp-card h1')?.textContent).toContain('Something went wrong');
    expect(el.querySelector('.ap-btn')?.textContent).toContain('Try again');
  });

  it('falls back to the generic error for an unmapped status', () => {
    const el = renderStatus(418);
    expect(el.querySelector('.pp-card h1')?.textContent).toContain('Something went wrong');
  });

  it('navigates home and dismisses the overlay when the action is clicked', () => {
    const router = TestBed.inject(Router);
    const navSpy = spyOn(router, 'navigateByUrl').and.returnValue(Promise.resolve(true));
    const destroy$ = new Subject<void>();
    let dismissed = false;
    destroy$.subscribe(() => {
      dismissed = true;
    });
    component.destroy$ = destroy$;

    const el = renderStatus(404);
    (el.querySelector('.ap-btn') as HTMLButtonElement).click();

    expect(navSpy).toHaveBeenCalled();
    expect(navSpy.calls.mostRecent().args[0]).toBe('/');
    expect(dismissed).toBe(true);
  });
});
