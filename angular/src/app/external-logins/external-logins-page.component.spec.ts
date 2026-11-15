import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { Subject, of, throwError } from 'rxjs';
import { ExternalLoginsPageComponent } from './external-logins-page.component';
import { ExternalLoginsService } from './external-logins.service';
import type { ExternalLoginDto } from './external-logins.models';

const row = (over: Partial<ExternalLoginDto> = {}): ExternalLoginDto => ({
  id: 'u1',
  name: 'TEST',
  surname: 'Paralegal',
  email: 'test.paralegal@example.test',
  roles: ['Applicant Paralegal'],
  firmName: 'TEST Firm',
  isActive: true,
  emailConfirmed: true,
  creationTime: '2026-11-10T18:00:00Z',
  ...over,
});

describe('ExternalLoginsPageComponent', () => {
  let service: jasmine.SpyObj<ExternalLoginsService>;
  let toaster: jasmine.SpyObj<ToasterService>;
  let fixture: ComponentFixture<ExternalLoginsPageComponent>;

  function setup(): void {
    TestBed.configureTestingModule({
      providers: [
        { provide: ExternalLoginsService, useValue: service },
        { provide: ToasterService, useValue: toaster },
        {
          provide: LocalizationService,
          useValue: { instant: (k: string, ...p: string[]) => [k, ...p].join('|') },
        },
      ],
    });
    fixture = TestBed.createComponent(ExternalLoginsPageComponent);
    fixture.detectChanges();
  }

  const el = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const q = (sel: string): HTMLElement | null => el().querySelector(sel);
  const click = (sel: string): void => {
    (q(sel) as HTMLElement).click();
    fixture.detectChanges();
  };
  const type = (sel: string, value: string): void => {
    const input = q(sel) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };
  const escape = (): void => {
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
  };

  beforeEach(() => {
    service = jasmine.createSpyObj<ExternalLoginsService>('ExternalLoginsService', [
      'getList',
      'setActive',
    ]);
    toaster = jasmine.createSpyObj<ToasterService>('ToasterService', ['success']);
    service.getList.and.returnValue(of({ totalCount: 1, items: [row()] }));
    service.setActive.and.returnValue(of(row({ isActive: false })));
  });

  afterEach(() => TestBed.resetTestingModule());

  it('lists the logins with the paralegal role shown by its display label', () => {
    setup();
    const first = service.getList.calls.mostRecent().args[0];
    expect(first.skipCount).toBe(0);
    expect(first.maxResultCount).toBe(20);
    expect(first.isActive).toBeNull();
    const rows = el().querySelectorAll('[data-testid="login-row"]');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('TEST Paralegal');
    expect(rows[0].textContent).toContain('test.paralegal@example.test');
    expect(rows[0].textContent).toContain('Paralegal / Case Manager (Applicant side)');
    expect(rows[0].textContent).toContain('TEST Firm');
  });

  it('shows an unconfirmed login as such', () => {
    service.getList.and.returnValue(of({ totalCount: 1, items: [row({ emailConfirmed: false })] }));
    setup();
    expect(q('[data-testid="status-cell"]')?.textContent).toContain(
      '::ExternalLogins:Page:NotConfirmed',
    );
  });

  it('filters on the server and goes back to the first page', () => {
    setup();
    service.getList.calls.reset();
    type('[data-testid="filter"]', ' burgis ');
    const last = service.getList.calls.mostRecent().args[0];
    expect(last.filterText).toBe('burgis');
    expect(last.skipCount).toBe(0);
  });

  it('asks the server for only the switched-off or only the active logins', () => {
    setup();
    const select = q('[data-testid="status"]') as HTMLSelectElement;
    select.value = 'off';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(service.getList.calls.mostRecent().args[0].isActive).toBe(false);
    select.value = 'active';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(service.getList.calls.mostRecent().args[0].isActive).toBe(true);
  });

  it('asks first, naming the email and saying emails are not stopped, then switches off', () => {
    setup();
    click('[data-testid="toggle"]');
    expect(service.setActive).not.toHaveBeenCalled();
    expect(q('[data-testid="confirm-off-text"]')?.textContent).toContain(
      'test.paralegal@example.test',
    );
    expect(q('[data-testid="confirm-off-emails"]')?.textContent).toContain(
      '::ExternalLogins:Confirm:OffEmails',
    );
    click('[data-testid="confirm"]');
    expect(service.setActive).toHaveBeenCalledOnceWith('u1', { isActive: false });
    expect(toaster.success).toHaveBeenCalledWith(
      '::ExternalLogins:Toast:Off|test.paralegal@example.test',
    );
    expect(q('[data-testid="confirm"]')).toBeNull();
    expect(q('[data-testid="status-cell"]')?.textContent).toContain(
      '::ExternalLogins:Page:StatusOff',
    );
  });

  it('switches a switched-off login back on', () => {
    service.getList.and.returnValue(of({ totalCount: 1, items: [row({ isActive: false })] }));
    service.setActive.and.returnValue(of(row({ isActive: true })));
    setup();
    click('[data-testid="toggle"]');
    expect(q('[data-testid="confirm-on-text"]')).not.toBeNull();
    expect(q('[data-testid="confirm-off-emails"]')).toBeNull();
    click('[data-testid="confirm"]');
    expect(service.setActive).toHaveBeenCalledOnceWith('u1', { isActive: true });
  });

  it('Escape closes the dialog without calling the server', () => {
    setup();
    click('[data-testid="toggle"]');
    escape();
    expect(q('[data-testid="confirm"]')).toBeNull();
    expect(service.setActive).not.toHaveBeenCalled();
  });

  it('Escape and Cancel leave the dialog open while a save is running', () => {
    // A save that never answers keeps the page busy.
    service.setActive.and.returnValue(new Subject<ExternalLoginDto>());
    setup();
    click('[data-testid="toggle"]');
    click('[data-testid="confirm"]');
    escape();
    expect(q('[data-testid="confirm"]')).not.toBeNull();
    expect((q('[data-testid="confirm"]') as HTMLButtonElement).disabled).toBeTrue();
    click('[data-testid="cancel"]');
    expect(q('[data-testid="confirm"]')).not.toBeNull();
  });

  it('keeps the dialog open and unlocks it when the server refuses', () => {
    service.setActive.and.returnValue(throwError(() => new Error('refused')));
    setup();
    click('[data-testid="toggle"]');
    click('[data-testid="confirm"]');
    expect(toaster.success).not.toHaveBeenCalled();
    expect(q('[data-testid="confirm"]')).not.toBeNull();
    expect((q('[data-testid="confirm"]') as HTMLButtonElement).disabled).toBeFalse();
  });
});
