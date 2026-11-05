import { TestBed } from '@angular/core/testing';
import { LocalizationService } from '@abp/ng.core';
import { EmailStoppedTagComponent } from './email-stopped-tag.component';

describe('EmailStoppedTagComponent', () => {
  function render(email: string | null, stopped: string[]): HTMLElement {
    TestBed.configureTestingModule({
      providers: [{ provide: LocalizationService, useValue: { instant: (k: string) => k } }],
    });
    const fixture = TestBed.createComponent(EmailStoppedTagComponent);
    fixture.componentRef.setInput('email', email);
    fixture.componentRef.setInput('stoppedSet', new Set(stopped));
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  afterEach(() => TestBed.resetTestingModule());

  it('shows the tag when the email is stopped, ignoring case', () => {
    const el = render('Test.Party@Example.test', ['test.party@example.test']);
    expect(el.querySelector('[data-testid="emails-stopped-tag"]')?.textContent).toContain(
      '::EmailStops:Tag:Stopped',
    );
  });

  it('shows nothing for an email that is not stopped or is blank', () => {
    expect(render('other@example.test', ['test.party@example.test']).textContent?.trim()).toBe('');
    TestBed.resetTestingModule();
    expect(render(null, ['test.party@example.test']).textContent?.trim()).toBe('');
  });
});
