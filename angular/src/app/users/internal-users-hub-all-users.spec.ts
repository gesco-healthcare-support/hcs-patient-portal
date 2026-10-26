import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ImpersonationService } from '@volo/abp.commercial.ng.ui/config';
import { of } from 'rxjs';
import { InternalUsersHubComponent } from './internal-users-hub.component';
import { UsersSectionGateway } from './users-section.gateway';
import { USERS_SECTIONS } from './users-hub.util';

/**
 * The All Users tab (2026-10-26): the identity Users screen shown inside the Users & Access hub, beside
 * the invites. The hub renders the rail and a router outlet; the route loads the identity screens into
 * it (pinned in app.routes.spec.ts). Rendered here, because the outlet and the rail item are template
 * facts the component class cannot show.
 */
describe('InternalUsersHubComponent All Users tab', () => {
  function render(section: string, granted: string[]): HTMLElement {
    TestBed.configureTestingModule({
      imports: [InternalUsersHubComponent],
      providers: [
        provideRouter([]),
        { provide: UsersSectionGateway, useValue: {} },
        {
          provide: PermissionService,
          useValue: { getGrantedPolicy: (p: string) => granted.includes(p) },
        },
        { provide: ToasterService, useValue: {} },
        { provide: ImpersonationService, useValue: {} },
        {
          provide: ActivatedRoute,
          useValue: { data: of({ section }), snapshot: { queryParamMap: { get: () => null } } },
        },
      ],
    });
    const fixture = TestBed.createComponent(InternalUsersHubComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  function railLabels(root: HTMLElement): string[] {
    return Array.from(root.querySelectorAll('.cf-railitem')).map((a) => a.textContent!.trim());
  }

  afterEach(() => TestBed.resetTestingModule());

  it('lists All Users right after the invites, gated on AbpIdentity.Users', () => {
    const all = USERS_SECTIONS.find((s) => s.key === 'all');
    expect(all?.policy).toBe('AbpIdentity.Users');
    expect(all?.route).toBe('/users/all');
    expect(USERS_SECTIONS.map((s) => s.key).slice(0, 3)).toEqual(['invite', 'pending', 'all']);
  });

  it('shows the tab, marks it current and renders the outlet the identity screen loads into', () => {
    const root = render('all', ['AbpIdentity.Users']);

    expect(railLabels(root)).toEqual(['All Users']);
    expect(root.querySelector('.cf-railitem[data-on="true"]')?.textContent?.trim()).toBe(
      'All Users',
    );
    expect(root.querySelector('.cf-content router-outlet')).not.toBeNull();
  });

  it('shows no All Users tab to a caller without AbpIdentity.Users', () => {
    // An office Supervisor's shape: the invites, not the user list. (The route itself is guarded on
    // the same policy, so such a caller never reaches this section; only the rail is under test.)
    const root = render('all', ['CaseEvaluation.UserManagement.InviteExternalUser']);

    expect(railLabels(root)).toEqual(['Invite External User', 'Pending Invites']);
  });
});
