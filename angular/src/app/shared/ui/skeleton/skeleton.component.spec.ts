import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SkeletonComponent } from './skeleton.component';

describe('SkeletonComponent', () => {
  let fixture: ComponentFixture<SkeletonComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [SkeletonComponent] }).compileComponents();
    fixture = TestBed.createComponent(SkeletonComponent);
  });

  function sk(): HTMLElement {
    return fixture.nativeElement.querySelector('.sk') as HTMLElement;
  }

  it('renders a .sk shimmer with the given dimensions', () => {
    fixture.componentRef.setInput('width', '90px');
    fixture.componentRef.setInput('height', '26px');
    fixture.detectChanges();

    expect(sk()).toBeTruthy();
    expect(sk().style.width).toBe('90px');
    expect(sk().style.height).toBe('26px');
  });

  it('applies the circle modifier', () => {
    fixture.componentRef.setInput('circle', true);
    fixture.detectChanges();
    expect(sk().classList.contains('sk--circle')).toBe(true);
  });
});
