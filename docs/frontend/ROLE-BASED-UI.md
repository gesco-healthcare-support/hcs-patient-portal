# Role-Based UI

[Home](../INDEX.md) > [Frontend](./) > Role-Based UI

## Overview

The application renders different UI layouts and content based on the current user's role. The primary distinction is between **external users** (Patient, Applicant Attorney, Defense Attorney) who see a simplified portal, and **internal/admin users** who see the full ABP management interface with LeptonX sidebar navigation.

## External vs Internal Users

| Aspect | External Users | Internal/Admin Users |
|--------|---------------|---------------------|
| Roles | Patient, Applicant Attorney, Defense Attorney | Admin, and any role without external designation |
| Sidebar | Hidden | Visible (LeptonX side menu) |
| Topbar | Hidden (LeptonX), replaced by `TopHeaderNavbarComponent` | Visible (LeptonX topbar) |
| Content width | Full width (no sidebar margin) | Standard width with sidebar offset |
| Home page | Simplified portal with booking buttons + appointment table | ABP default landing with login prompt |
| Navigation | Custom header with Profile/Help/Logout buttons | LeptonX sidebar menu |

## Role Detection

### AppComponent (app.component.ts)

Role detection happens in the root `AppComponent` and runs on every `NavigationEnd` event:

```typescript
private updatePatientRoleClass(): void {
  const currentUser = this.configState.getOne('currentUser');
  const roles = (currentUser?.roles ?? []).map(role => role.toLowerCase().trim());
  const externalUserRoles = ['patient', 'applicant attorney', 'defense attorney'];
  const isExternalUser = externalUserRoles.some(role => roles.includes(role));

  document.body.classList.toggle('externaluser-role', isExternalUser);
  document.documentElement.classList.toggle('externaluser-role', isExternalUser);
  this.applySidebarVisibility(isExternalUser);
}
```

- Uses ABP `ConfigStateService` to read `currentUser.roles` from the application configuration
- Checks if any role matches the external user roles list (case-insensitive)
- Toggles CSS classes on both `<body>` and `<html>` elements
- Directly manipulates DOM elements to hide/show sidebar and expand content

### HomeComponent (home.component.ts)

The home page also performs role detection for rendering:

```typescript
get isPatientUser(): boolean {
  if (!this.hasLoggedIn) return false;
  const roles = this.currentUser?.roles ?? [];
  const externalUserRoles = ['patient', 'applicant attorney', 'defense attorney'];
  return roles.some(role => externalUserRoles.includes(role?.toLowerCase() ?? ''));
}

private get isAttorneyUser(): boolean {
  const roles = this.currentUser?.roles ?? [];
  const attorneyRoles = ['applicant attorney', 'defense attorney'];
  return roles.some(role => attorneyRoles.includes(role?.toLowerCase() ?? ''));
}
```

## CSS Class Toggles

When an external user is detected, the following changes are applied:

### Global Styles (styles.scss)

```scss
body.externaluser-role, html.externaluser-role {
  // Hide LeptonX topbar
  .lpx-topbar-container, .lpx-topbar {
    display: none !important;
  }

  // Hide sidebar
  .lpx-sidebar-container, .lpx-sidebar,
  aside, .externaluser-sidebar-hidden {
    display: none !important;
  }

  // Full-width content
  .lpx-content-container, main, .externaluser-main-full {
    margin-left: 0 !important;
    padding-left: 0 !important;
  }

  // Hide user text next to avatar
  lpx-avatar + .lpx-menu-item-text {
    display: none !important;
  }
}
```

### DOM Class Manipulation (AppComponent)

In addition to the CSS body class, `applySidebarVisibility()` directly toggles classes on DOM elements:

- **Sidebar selectors:** `.lpx-sidebar-container`, `.lpx-sidebar`, `.lpx-menu-container`, `.lpx-menu`, `aside` -- get `externaluser-sidebar-hidden` class
- **Main content selectors:** `.lpx-content-container`, `.lpx-main-container`, `.lpx-main-content`, `.lpx-page`, `main` -- get `externaluser-main-full` class

## TopHeaderNavbarComponent

Custom header component for external users, replacing the LeptonX topbar:

```typescript
@Component({
  selector: 'app-top-header-navbar',
  standalone: true,
  imports: [CommonModule],
})
export class TopHeaderNavbarComponent {
  @Input() tenantName = '';    // e.g., "ABC Medical Group"
  @Input() userName = '';      // e.g., "John Doe"
  @Input() roleName = '';      // e.g., "Patient"
  @Input() showProfile = true;
  @Input() showHelp = true;
  @Input() showLogout = true;

  @Output() profileClick = new EventEmitter<void>();
  @Output() helpClick = new EventEmitter<void>();
  @Output() logoutClick = new EventEmitter<void>();
}
```

Used in:
- `HomeComponent` -- with `profileClick` navigating to `/doctor-management/patients/my-profile`
- `AppointmentAddComponent` -- with same profile navigation

## UI Rendering Decision Tree

```mermaid
flowchart TD
    START[User navigates to page] --> AUTH{Is user<br/>authenticated?}
    AUTH -->|No| UNAUTH[Show login/register<br/>landing page<br/>with ABP page layout]
    AUTH -->|Yes| ROLE{User has role:<br/>Patient, Applicant Attorney,<br/>or Defense Attorney?}
    ROLE -->|Yes - External| EXT_SETUP[Apply externaluser-role class<br/>Hide sidebar + LeptonX topbar<br/>Show TopHeaderNavbar]
    ROLE -->|No - Admin/Internal| ADMIN_SETUP[Standard LeptonX layout<br/>Show sidebar + topbar<br/>Full admin navigation]

    EXT_SETUP --> EXT_HOME{Current page?}
    EXT_HOME -->|Home /| EXT_HOME_PAGE[Show patient portal:<br/>- TopHeaderNavbar<br/>- Book Appointment button<br/>- Book Re-evaluation button<br/>- My Appointments table]
    EXT_HOME -->|/appointments/add| EXT_BOOK[Show booking form:<br/>- TopHeaderNavbar<br/>- Multi-tab form<br/>- Calendar date picker]
    EXT_HOME -->|/appointments/view/:id| EXT_VIEW[Show appointment detail:<br/>- Read-only view]
    EXT_HOME -->|/doctor-management/patients/my-profile| EXT_PROF[Show patient profile:<br/>- Self-service editing]

    ADMIN_SETUP --> ADMIN_HOME{Current page?}
    ADMIN_HOME -->|Home /| ADMIN_LANDING[Show admin landing:<br/>- ABP getting started page<br/>- or dashboard link]
    ADMIN_HOME -->|Any admin route| ADMIN_PAGE[Show full management<br/>interface with sidebar<br/>navigation]

    subgraph "External User - Attorney Specific"
        ATT_CHECK{Is Attorney role?}
        ATT_CHECK -->|Yes| ATT_FILTER[Filter appointments by<br/>accessorIdentityUserId]
        ATT_CHECK -->|No - Patient| PAT_FILTER[Filter appointments by<br/>identityUserId]
    end

    EXT_HOME_PAGE --> ATT_CHECK
```

## HomeComponent Rendering by Role

### Unauthenticated Users

Shows a simple landing page wrapped in `<abp-page>`:
- "Appointment Scheduling Portal" heading
- "Click to login or register" message
- Login button that calls `authService.navigateToLogin()`

### Patient / Attorney Users (`isPatientUser === true`)

Shows the external user portal:

1. **TopHeaderNavbar** -- Displays tenant name, user name, role; profile button navigates to my-profile
2. **Action buttons row:**
   - "Book Appointment" -- navigates to `/appointments/add?type=1`
   - "Book Re-evaluation" -- button present but not yet wired
3. **My Appointments Requests table** -- ngx-datatable showing:
   - Appointment Type (name)
   - Patient (firstName + lastName)
   - Panel Number
   - Confirmation Number (clickable link to `/appointments/view/:id`)
   - Appointment Date
   - Appointment Status (localized enum display)
   - Location (name)

### Admin Users (not external)

Shows the standard ABP landing page within `<abp-page>`. If not logged in, shows login prompt. Otherwise, the sidebar provides navigation to all management screens.

## Appointment Filtering by Role

In `HomeComponent.ngOnInit()`:

```typescript
if (this.isAttorneyUser) {
  // Attorneys see appointments where they are an accessor
  this.service.filters.accessorIdentityUserId = currentUserId;
} else {
  // Patients see their own appointments
  this.service.filters.identityUserId = currentUserId;
}
```

This ensures:
- **Patients** see only appointments where they are the patient (`identityUserId` match)
- **Attorneys** see appointments where they have been granted access (`accessorIdentityUserId` match via `AppointmentAccessor`)

## Patient Self-Service Routes

External users have access to specific routes without requiring ABP permissions (only `authGuard`):

| Route | Purpose |
|-------|---------|
| `/` | Home with portal view |
| `/appointments/add` | Book new appointment |
| `/appointments/view/:id` | View appointment detail |
| `/doctor-management/patients/my-profile` | Edit own patient profile |

All other routes require `permissionGuard` and specific ABP policies, making them inaccessible to external users unless explicitly granted.

## Cleanup

`AppComponent.ngOnDestroy()` cleans up the role-based CSS modifications:

```typescript
ngOnDestroy(): void {
  this.subscription.unsubscribe();
  document.body.classList.remove('externaluser-role');
  document.documentElement.classList.remove('externaluser-role');
  this.applySidebarVisibility(false);
}
```

---

**Related Documentation:**
- [Routing & Navigation](ROUTING-AND-NAVIGATION.md)
- [Permissions](../backend/PERMISSIONS.md)
- [User Roles & Actors](../business-domain/USER-ROLES-AND-ACTORS.md)
