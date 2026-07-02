import { TestBed, fakeAsync, tick, discardPeriodicTasks } from '@angular/core/testing';
import { of } from 'rxjs';
import { Router } from '@angular/router';
import { NotificationBellComponent } from './notification-bell.component';
import { AppNotificationService } from '../../../proxy/notifications/app-notification.service';
import { AppNotificationType } from '../../../proxy/notifications/app-notification-type.enum';
import type { AppNotificationDto } from '../../../proxy/notifications/models';

/**
 * QA item 7: the notification bell. Logic tests (cast past `protected`, mirroring
 * the internal-dashboard spec) cover the poll, open-to-load, mark-read + navigate,
 * and mark-all. The poll test uses fakeAsync because timer(0, interval) emits on
 * the async scheduler.
 */
describe('NotificationBellComponent (item 7)', () => {
  let svc: {
    getMyUnreadCount: jasmine.Spy;
    getMyNotifications: jasmine.Spy;
    markRead: jasmine.Spy;
    markAllRead: jasmine.Spy;
  };
  let router: { navigateByUrl: jasmine.Spy };

  function sampleNotification(overrides: Partial<AppNotificationDto> = {}): AppNotificationDto {
    return {
      id: 'n1',
      notificationType: AppNotificationType.AppointmentRequested,
      title: 'New appointment request',
      body: 'Request A00099 was submitted and needs review.',
      url: '/appointments/view/a1',
      isRead: false,
      ...overrides,
    } as AppNotificationDto;
  }

  function createFixture() {
    svc = {
      getMyUnreadCount: jasmine.createSpy('getMyUnreadCount').and.returnValue(of(3)),
      getMyNotifications: jasmine
        .createSpy('getMyNotifications')
        .and.returnValue(of({ items: [sampleNotification()], totalCount: 1 })),
      markRead: jasmine.createSpy('markRead').and.returnValue(of(void 0)),
      markAllRead: jasmine.createSpy('markAllRead').and.returnValue(of(void 0)),
    };
    router = { navigateByUrl: jasmine.createSpy('navigateByUrl') };

    TestBed.configureTestingModule({
      imports: [NotificationBellComponent],
      providers: [
        { provide: AppNotificationService, useValue: svc },
        { provide: Router, useValue: router },
      ],
    });
    return TestBed.createComponent(NotificationBellComponent);
  }

  afterEach(() => TestBed.resetTestingModule());

  it('polls the unread count on init', fakeAsync(() => {
    const fixture = createFixture();
    fixture.detectChanges(); // runs ngOnInit -> starts the poll
    tick(); // let timer(0) emit its first value
    expect(svc.getMyUnreadCount).toHaveBeenCalled();
    expect((fixture.componentInstance as any).unread()).toBe(3);
    discardPeriodicTasks();
  }));

  it('loads the list when the dropdown is opened', () => {
    const c = createFixture().componentInstance as any;
    c.toggle();
    expect(c.open()).toBeTrue();
    expect(svc.getMyNotifications).toHaveBeenCalled();
    expect(c.items().length).toBe(1);
  });

  it('marks a row read and navigates on click', () => {
    const c = createFixture().componentInstance as any;
    c.unread.set(3);
    const n = sampleNotification();
    c.items.set([n]);
    c.onClick(n);
    expect(svc.markRead).toHaveBeenCalledWith('n1');
    expect(router.navigateByUrl).toHaveBeenCalledWith('/appointments/view/a1');
    expect(c.unread()).toBe(2);
    expect(c.items()[0].isRead).toBeTrue();
  });

  it('mark all read clears the badge and flags every row read', () => {
    const c = createFixture().componentInstance as any;
    c.unread.set(5);
    c.items.set([sampleNotification(), sampleNotification({ id: 'n2' })]);
    c.markAll();
    expect(svc.markAllRead).toHaveBeenCalled();
    expect(c.unread()).toBe(0);
    expect(c.items().every((x: AppNotificationDto) => x.isRead)).toBeTrue();
  });
});
