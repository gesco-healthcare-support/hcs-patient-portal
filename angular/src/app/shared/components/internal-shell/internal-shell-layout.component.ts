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
import { ToasterService } from '@abp/ng.theme.shared';
import { Subscription, filter } from 'rxjs';
import { IconComponent } from '../../ui/icon/icon.component';
import { OfficeNamePipe } from '../../pipes/office-name.pipe';
import { NotificationBellComponent } from '../notification-bell/notification-bell.component';
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
import { IntakeAssignmentsService } from '../../../proxy/host-operators/intake-assignments.service';
import type { LookupDto } from '../../../proxy/shared/models';
import { NavBadgeKey, resolveNavGroups } from './internal-nav.config';
import {
  clearPendingOfficeSwitch,
  readPendingOfficeSwitch,
  storePendingOfficeSwitch,
} from './pending-office-switch';
import { OAuthService } from 'angular-oauth2-oidc';

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
  imports: [RouterOutlet, RouterLink, IconComponent, OfficeNamePipe, NotificationBellComponent],
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
  private readonly intakeAssignments = inject(IntakeAssignmentsService);
  private readonly title = inject(Title);
  private readonly toaster = inject(ToasterService);

  /** AuthServer Razor "manage my account" page, resolved from the runtime OAuth
   *  issuer so the host:port is correct per tenant subdomain (and on shifted
   *  worktree ports, where the build-time environment is overridden). */
  protected get manageAccountUrl(): string {
    let issuer = '';
    try {
      issuer = this.injector.get(OAuthService).issuer ?? '';
    } catch {
      issuer = '';
    }
    return buildAuthServerUrl(issuer, '/Account/Manage');
  }

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
   * an office user (ABP sets currentUser.impersonatorUserId). Inside an office it
   * makes the navbar switcher show the "Management" exit (the office user lacks host
   * permissions and our custom shell replaces ABP's default "back to impersonator"
   * user-menu item), so no operator is stranded after the banner's removal.
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

  /**
   * Drives the navbar office-switcher pill. At HOST scope, supervisors/admins open
   * it to switch INTO an office (Intake Staff is tenant-locked and uses its landing
   * page). INSIDE an office, anyone who is impersonating opens it to return to host
   * ("Management") -- this replaces the removed impersonation banner so no operator
   * is stranded. Office A -> office B direct switching is a follow-up (custom
   * AuthServer grant); until then the in-office dropdown offers only the host exit.
   */
  protected readonly canSwitch = computed<boolean>(() =>
    this.hostScope() ? this.roleKey() !== 'intake' : this.impersonating(),
  );

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
   * Toggle the office-switcher dropdown, lazily loading its targets on first open
   * (see loadSwitchTargets). At host scope a supervisor/IT Admin opens it to switch
   * INTO an office; inside an office (F Half 2) any impersonating operator opens it
   * to hop directly to another office or return to host. Intake Staff at host scope
   * is tenant-locked (canSwitch() is false) and uses its landing page instead.
   */
  protected toggleSwitcher(): void {
    if (!this.canSwitch()) {
      return;
    }
    const open = !this.switcherOpen();
    this.switcherOpen.set(open);
    if (open && this.offices().length === 0) {
      this.loadSwitchTargets();
    }
  }

  /**
   * Load the offices the switcher may hop into. At host scope (or for a supervisor
   * impersonating as an office admin) that is every active office (getTenantOptions,
   * AllowAnonymous + host-scoped). An in-office Intake operator instead gets only
   * their assigned offices (getSwitchableOffices, resolved server-side from the
   * impersonation claim -- the in-office shadow user does not hold IntakeImpersonation).
   * The current office is excluded so the menu reads "where else can I go". The grant
   * + assignment gate remain the boundary; this list is convenience.
   */
  private loadSwitchTargets(): void {
    const inOfficeIntake = !this.hostScope() && this.roleKey() === 'intake';
    const targets$ = inOfficeIntake
      ? this.intakeAssignments.getSwitchableOffices()
      : this.internalUsers.getTenantOptions();
    targets$.subscribe({
      next: (res) => this.offices.set(this.excludeCurrentOffice(res.items ?? [])),
      error: () => this.offices.set([]),
    });
  }

  /** Drop the office the caller is already in, so it is not offered as a target. */
  private excludeCurrentOffice(items: LookupDto<string>[]): LookupDto<string>[] {
    const currentId = this.tenant()?.id;
    return currentId ? items.filter((o) => o.id !== currentId) : items;
  }

  protected closeSwitcher(): void {
    this.switcherOpen.set(false);
  }

  /**
   * Switch into an office. A supervisor / IT Admin enters as the office `admin`
   * (same path as the Offices list's "Switch into tenant" button) and gets the
   * admin's full powers; an Intake operator sends an empty username so the custom
   * grant forces their own limited shadow user.
   *
   * From host scope this is a single direct impersonation. From INSIDE an office
   * (F Half 2 office -> office), ABP forbids nested impersonation, so we instead
   * stash the target, de-impersonate to host, and let maybeResumePendingSwitch()
   * finish the tenant hop after the reload -- two stock operations, one click.
   */
  protected switchInto(officeId: string | undefined): void {
    if (this.switching() || !officeId) {
      return;
    }
    const userName = this.roleKey() === 'intake' ? '' : 'admin';
    this.switching.set(true);
    this.closeSwitcher();

    if (this.impersonating()) {
      // Office -> office: persist the target across the de-impersonation reload,
      // then return to host. The second leg runs in maybeResumePendingSwitch().
      storePendingOfficeSwitch({ officeId, userName });
      this.toaster.info('Switching offices...');
      this.impersonation.impersonate({}).subscribe({
        // The back-to-host grant runs through the OAuth token path (no RestService),
        // so a failure surfaces only here. Drop the pending record so the operator is
        // not auto-bounced on the next load.
        error: () => {
          clearPendingOfficeSwitch();
          this.switching.set(false);
          this.toaster.error(
            'Could not switch into that office. Please try again, or contact an administrator.',
          );
        },
      });
      return;
    }

    // Host scope: a direct single-hop impersonation (no nesting), unchanged.
    this.impersonation.impersonateTenant(officeId, userName).subscribe({
      // Impersonation runs through the OAuth token grant, NOT RestService, so ABP's
      // global HTTP error dialog never fires -- this handler is the only place a
      // failed switch can surface. Without it the spinner just stops (looks frozen).
      error: () => {
        this.switching.set(false);
        this.toaster.error(
          'Could not switch into that office. Please try again, or contact an administrator.',
        );
      },
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
      // Same OAuth-grant path as switchInto: a failed exit surfaces only here.
      error: () => {
        this.switching.set(false);
        this.toaster.error(
          'Could not return to Management. Please try again, or sign out and back in.',
        );
      },
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

    // F Half 2: complete a pending office -> office hop. switchInto() stashes the
    // target and de-impersonates to host (ABP forbids nested impersonation); once we
    // are back at host scope, finish the second leg by impersonating the target.
    this.maybeResumePendingSwitch();
  }

  /**
   * Finish a pending office -> office hop stashed by switchInto(). Runs on every
   * identity refresh; acts only when we are back at host scope (de-impersonation
   * completed) and a target is pending. Clears the record FIRST so a failed second
   * leg or a config re-emit cannot loop -- a failure simply leaves the operator at
   * host.
   */
  private maybeResumePendingSwitch(): void {
    if (!this.hostScope() || this.impersonating()) {
      return;
    }
    const pending = readPendingOfficeSwitch();
    if (!pending) {
      return;
    }
    clearPendingOfficeSwitch();
    this.switching.set(true);
    this.toaster.info('Switching offices...');
    this.impersonation.impersonateTenant(pending.officeId, pending.userName).subscribe({
      error: () => {
        this.switching.set(false);
        this.toaster.error(
          'Could not switch into that office. Please try again, or contact an administrator.',
        );
      },
    });
  }
}

/** Build an AuthServer Razor URL from the runtime OAuth issuer (carries the
 *  correct host:port for the current tenant subdomain). Falls back to the bare
 *  path if the issuer is unavailable. */
function buildAuthServerUrl(issuer: string, path: string): string {
  const base = (issuer ?? '').replace(/\/+$/, '');
  return base ? `${base}${path}` : path;
}
