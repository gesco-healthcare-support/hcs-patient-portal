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

  /** AuthServer Razor "manage my account" page on the current tenant host. */
  protected readonly manageAccountUrl = buildAuthServerUrl('/Account/Manage');

  private readonly user = signal<ShellUser | null>(null);
  private readonly tenant = signal<ShellTenant | null>(null);
  private readonly currentUrl = signal<string>(this.router.url);

  protected readonly collapsed = signal(false);
  protected readonly acctOpen = signal(false);

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

  protected readonly tenantName = computed<string>(
    () => this.tenant()?.name || 'Appointment Portal',
  );

  protected readonly tenantInitial = computed<string>(() =>
    (this.tenantName().trim()[0] ?? 'A').toUpperCase(),
  );

  /** Intake Staff is tenant-locked; everyone else can (eventually) switch. */
  protected readonly canSwitch = computed<boolean>(() => this.roleKey() !== 'intake');

  protected readonly brandSubtitle = computed<string>(() =>
    this.platform() ? 'Platform administration' : this.tenantName(),
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

  protected toggleAcct(): void {
    this.acctOpen.update((v) => !v);
  }

  protected closeAcct(): void {
    this.acctOpen.set(false);
  }

  protected signOut(): void {
    this.closeAcct();
    void performFullLogout(this.injector);
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.closeAcct();
  }

  private refreshIdentity(): void {
    this.user.set((this.configState.getOne('currentUser') as ShellUser) ?? null);
    this.tenant.set((this.configState.getOne('currentTenant') as ShellTenant) ?? null);
    // Host scope follows the active tenant context (null currentTenant = host).
    // Previously hardcoded true (pre-tenant-switch MVP); now reactive so the nav
    // flips to the tenant view when IT Admin impersonates into a clinic
    // (currentTenant becomes the impersonated tenant) and back on exit.
    this.hostScope.set(isHostScope(this.configState));
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
