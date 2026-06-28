import {
  Component,
  HostListener,
  Injector,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { ConfigStateService, PermissionService } from '@abp/ng.core';
import { Subscription, filter } from 'rxjs';
import { IconComponent } from '../../ui/icon/icon.component';
import { performFullLogout } from '../../auth/full-logout';
import {
  InternalRoleKey,
  isHostScope,
  resolveInternalRoleKey,
} from '../../auth/internal-user-roles';
import { avatarColor } from '../../ui/avatar.util';
import { InternalNavBadgeService } from '../../services/internal-nav-badge.service';
import { BrandingService } from '../../branding/branding.service';
import { ImpersonationService } from '@volo/abp.commercial.ng.ui/config';
import { InternalUsersService } from '../../../proxy/internal-users/internal-users.service';
import type { LookupDto } from '../../../proxy/shared/models';
import { NavBadgeKey, resolveNavGroups } from './internal-nav.config';

const AUTH_SERVER_PORT = '44368';

const ROLE_LABELS: Record<InternalRoleKey, string> = {
  itadmin: 'IT Admin',
  supervisor: 'Staff Supervisor',
  intake: 'Intake Staff',
  admin: 'Administrator',
};

interface ShellUser {
  name?: string;
  surname?: string;
  userName?: string;
  email?: string;
  roles?: string[];
}

interface ShellTenant {
  id?: string | null;
  name?: string | null;
}

/**
 * Internal staff shell (plan Tasks 2 + 4): a collapsible navy sidebar + topbar
 * that wraps the internal routes via a child `<router-outlet>`. Ported from the
 * design handoff (design_handoff_appointment_portal/components/in-shell.jsx).
 *
 * Mounted by app.routes.ts as the component of a canMatch-gated parent route
 * (internalUserOnlyMatchGuard), so it only renders for staff; external users
 * fall through to their own chrome-less pages. The shell sources identity from
 * ConfigStateService and reacts to auth-state changes. Nav items are filtered
 * by the resolved role key + host scope (see resolveNavGroups), and the active
 * item is derived from the router URL (longest route-prefix wins).
 */
@Component({
  selector: 'app-internal-shell-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, IconComponent],
  templateUrl: './internal-shell-layout.component.html',
  // Default (not OnPush) on purpose: the shell hosts legacy default-CD pages
  // (appointment detail/add extend AppointmentViewComponent/AppointmentAddComponent,
  // which load via manual .subscribe + plain-property assignment -- no async pipe,
  // markForCheck, or signals). Under an OnPush shell their async updates would not
  // render. OnPush+signal pages (dashboard, list) keep their own change-detection
  // optimization regardless of the parent strategy; the shell chrome is cheap to check.
})
export class InternalShellLayoutComponent implements OnInit, OnDestroy {
  private readonly configState = inject(ConfigStateService);
  private readonly permission = inject(PermissionService);
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);
  protected readonly badge = inject(InternalNavBadgeService);
  /** Per-office branding (display name); subdomain-resolved, so used as the
   *  primary office label with a "Dr. {tenant}" fallback for the impersonation
   *  path (host subdomain -> branding is null even when a tenant is active). */
  protected readonly branding = inject(BrandingService);
  private readonly impersonation = inject(ImpersonationService);
  private readonly internalUsers = inject(InternalUsersService);
  private readonly title = inject(Title);

  /** AuthServer Razor "manage my account" page on the current tenant host. */
  protected readonly manageAccountUrl = buildAuthServerUrl('/Account/Manage');

  private readonly user = signal<ShellUser | null>(null);
  private readonly tenant = signal<ShellTenant | null>(null);
  private readonly currentUrl = signal<string>(this.router.url);

  protected readonly collapsed = signal(false);
  protected readonly acctOpen = signal(false);

  /** Office-switcher dropdown (host operators). Loaded lazily on first open. */
  protected readonly switcherOpen = signal(false);
  protected readonly offices = signal<LookupDto<string>[]>([]);
  protected readonly switching = signal(false);

  /**
   * True when the current session is an impersonation -- a host operator acting as
   * an office user (ABP sets currentUser.impersonatorUserId). Drives the "Acting
   * as ... / Back to Evaluators" banner: without it the operator is stranded in
   * the office, since the office user lacks host permissions and our custom shell
   * replaces ABP's default "back to impersonator" user-menu item.
   */
  protected readonly impersonating = signal(false);

  /**
   * Per-group accordion state: section name -> explicitly toggled open/closed.
   * Unset sections fall back to "open only when they contain the active route"
   * (see isSectionOpen), so the rail is collapsed-by-default except where the
   * user currently is; an explicit click then sticks for the session.
   */
  private readonly sectionToggles = signal<Record<string, boolean>>({});

  private configSub: Subscription | null = null;
  private routerSub: Subscription | null = null;

  /** Resolved internal role key, or null while config is still loading. */
  protected readonly roleKey = computed<InternalRoleKey | null>(() =>
    resolveInternalRoleKey(this.user()?.roles ?? null),
  );

  /** Host scope (no current tenant) -- set from the canonical isHostScope check. */
  protected readonly hostScope = signal(true);

  /** True when the platform (host) nav + chrome should show: host scope + IT Admin/superuser. */
  protected readonly platform = computed<boolean>(() => {
    const rk = this.roleKey();
    return this.hostScope() && (rk === 'itadmin' || rk === 'admin');
  });

  /**
   * Role/host-filtered nav groups for the current user, further gated by the
   * granted ABP permission so a visible item always resolves (no click-into-403).
   * Recomputes on identity/config change via roleKey()/hostScope(); at that point
   * the granted policies are loaded (they arrive with currentUser in one config
   * payload), so getGrantedPolicy reads fresh values.
   */
  protected readonly groups = computed(() => {
    const rk = this.roleKey();
    return rk
      ? resolveNavGroups(rk, this.hostScope(), (policy) => this.permission.getGrantedPolicy(policy))
      : [];
  });

  /** Id of the nav item whose route is the longest prefix of the current URL. */
  protected readonly activeId = computed<string | null>(() => {
    const url = this.currentUrl().split('?')[0].split('#')[0];
    let bestId: string | null = null;
    let bestLen = -1;
    for (const group of this.groups()) {
      for (const item of group.items) {
        const r = item.route;
        if ((url === r || url.startsWith(r + '/')) && r.length > bestLen) {
          bestId = item.id;
          bestLen = r.length;
        }
      }
    }
    return bestId;
  });

  /** Section (group.sect) that contains the active item; drives default-open. */
  protected readonly activeSect = computed<string | null>(() => {
    const id = this.activeId();
    for (const group of this.groups()) {
      for (const item of group.items) {
        if (item.id === id) return group.sect;
      }
    }
    return null;
  });

  protected readonly crumb = computed<string>(() => {
    const id = this.activeId();
    for (const group of this.groups()) {
      for (const item of group.items) {
        if (item.id === id) return item.label;
      }
    }
    return this.platform() ? 'Overview' : 'Dashboard';
  });

  protected readonly userName = computed<string>(() => {
    const u = this.user();
    const full = [u?.name, u?.surname].filter(Boolean).join(' ').trim();
    return full || u?.userName || '';
  });

  protected readonly roleLabel = computed<string>(() => {
    const rk = this.roleKey();
    return rk ? ROLE_LABELS[rk] : '';
  });

  protected readonly initials = computed<string>(() => {
    const parts = this.userName().trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return '?';
    const first = parts[0][0] ?? '';
    const last = parts.length > 1 ? (parts[parts.length - 1][0] ?? '') : '';
    return (first + last).toUpperCase() || '?';
  });

  protected readonly avatar = computed<string>(() => avatarColor(this.userName() || '?'));

  /**
   * App brand text -- constant "Appointment Portal" everywhere (host + offices).
   * Identity is carried by the logo (the Evaluators crest at host, the office
   * logo at offices), not by this label (F3 follow-up 2026-06-28).
   */
  protected readonly brandName = computed<string>(() => 'Appointment Portal');
  protected readonly brandLogo = computed<string | null>(() =>
    this.hostScope() ? 'assets/branding/evaluators-logo.png' : null,
  );

  protected readonly tenantName = computed<string>(() => {
    // Prefer the office's branded display name ("Dr. Yuri Falkinstein");
    // fall back to "Dr. {tenant}" when branding is unresolved (impersonation
    // on the host subdomain), then the parent brand at true host scope.
    const display = this.branding.displayName()?.trim();
    if (display) return display;
    const name = this.tenant()?.name?.trim();
    return name ? `Dr. ${name}` : this.brandName();
  });

  protected readonly tenantInitial = computed<string>(() =>
    (this.tenantName().trim()[0] ?? 'A').toUpperCase(),
  );

  /** Intake Staff is tenant-locked; everyone else can (eventually) switch. */
  protected readonly canSwitch = computed<boolean>(() => this.roleKey() !== 'intake');

  protected readonly brandSubtitle = computed<string>(() =>
    this.hostScope() ? 'Platform administration' : this.tenantName(),
  );

  ngOnInit(): void {
    this.refreshIdentity();
    this.configSub = this.configState
      .createOnUpdateStream((state) => state)
      .subscribe(() => this.refreshIdentity());
    this.routerSub = this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => this.currentUrl.set(e.urlAfterRedirects));
    this.badge.start();
  }

  ngOnDestroy(): void {
    this.configSub?.unsubscribe();
    this.routerSub?.unsubscribe();
  }

  /** Live count behind a nav item's badge ('appointments' / 'changeRequests'). */
  protected badgeCount(key: NavBadgeKey | undefined): number {
    if (key === 'appointments') return this.badge.pendingAppointments();
    if (key === 'changeRequests') return this.badge.pendingChangeRequests();
    return 0;
  }

  protected toggleCollapse(): void {
    this.collapsed.update((v) => !v);
  }

  /**
   * Is a nav group expanded? Defaults to collapsed unless the group holds the
   * active route; an explicit toggle (toggleSection) overrides that default.
   * The icon-rail collapsed sidebar shows every item regardless, so this only
   * governs the expanded sidebar.
   */
  protected isSectionOpen(sect: string): boolean {
    return this.sectionToggles()[sect] ?? sect === this.activeSect();
  }

  protected toggleSection(sect: string): void {
    const open = this.isSectionOpen(sect);
    this.sectionToggles.update((m) => ({ ...m, [sect]: !open }));
  }

  protected toggleAcct(): void {
    this.acctOpen.update((v) => !v);
  }

  protected closeAcct(): void {
    this.acctOpen.set(false);
  }

  /**
   * Toggle the office-switcher dropdown. On first open, lazily load the offices
   * the operator may switch into. Host operators (IT Admin / Staff Supervisor)
   * get the full active-office list (getTenantOptions, gated by InternalUsers);
   * Intake Staff is tenant-locked (canSwitch() is false) and uses its own
   * landing page instead, so this never opens for them.
   */
  protected toggleSwitcher(): void {
    if (!this.canSwitch()) {
      return;
    }
    const open = !this.switcherOpen();
    this.switcherOpen.set(open);
    if (open && this.offices().length === 0) {
      this.internalUsers.getTenantOptions().subscribe({
        next: (res) => this.offices.set(res.items ?? []),
        error: () => this.offices.set([]),
      });
    }
  }

  protected closeSwitcher(): void {
    this.switcherOpen.set(false);
  }

  /**
   * Switch into an office. Uses stock tenant impersonation as the office `admin`
   * (same path as the Offices list's "Switch into tenant" button) -- a host
   * Supervisor / IT Admin gets the office admin's full powers once switched in.
   */
  protected switchInto(officeId: string | undefined): void {
    if (this.switching() || !officeId) {
      return;
    }
    this.switching.set(true);
    this.closeSwitcher();
    this.impersonation.impersonateTenant(officeId, 'admin').subscribe({
      error: () => this.switching.set(false),
    });
  }

  /**
   * Exit impersonation and return to the host operator's own session. Calls ABP's
   * Impersonation grant with no target (the "back to impersonator" path), which
   * restores the original user and reloads on the host (admin.localhost). Needs no
   * app permission -- it is an AuthServer grant, so it works even though the
   * impersonated office user lacks host rights.
   */
  protected backToHost(): void {
    if (this.switching()) {
      return;
    }
    this.switching.set(true);
    this.impersonation.impersonate({}).subscribe({
      error: () => this.switching.set(false),
    });
  }

  protected signOut(): void {
    this.closeAcct();
    void performFullLogout(this.injector);
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.closeAcct();
    this.closeSwitcher();
  }

  private refreshIdentity(): void {
    this.user.set((this.configState.getOne('currentUser') as ShellUser) ?? null);
    this.tenant.set((this.configState.getOne('currentTenant') as ShellTenant) ?? null);
    // Host scope follows the active tenant context (null currentTenant = host).
    // Previously hardcoded true (pre-tenant-switch MVP); now reactive so the nav
    // flips to the tenant view when IT Admin impersonates into a clinic
    // (currentTenant becomes the impersonated tenant) and back on exit.
    this.hostScope.set(isHostScope(this.configState));
    this.impersonating.set(this.impersonation.isImpersonatorVisible());
    // Brand text is constant "Appointment Portal" everywhere (F3 follow-up); set the
    // host tab title to match. Offices keep their per-office tab title (set by
    // BrandingService from the display name) so multiple office tabs stay tellable apart.
    if (this.hostScope() && !this.branding.displayName()) {
      this.title.setTitle('Appointment Portal');
    }
  }
}

/** Build an AuthServer Razor URL on the current tenant subdomain (port 44368). */
function buildAuthServerUrl(path: string): string {
  if (typeof window === 'undefined') {
    return path;
  }
  const { protocol, hostname } = window.location;
  return `${protocol}//${hostname}:${AUTH_SERVER_PORT}${path}`;
}
