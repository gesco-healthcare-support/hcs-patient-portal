# Component Patterns

> Purpose: Describes the two Angular component patterns (ABP Suite abstract/concrete and custom hand-written) used across the patient portal frontend. Audience: frontend developers. Last verified: 2026-06-01 vs main.

[Home](../INDEX.md) > [Frontend](./) > Component Patterns

## Overview

The Angular frontend follows two distinct component patterns: **ABP Suite-generated** components for standard CRUD entities and **custom components** for specialized workflows. All components are standalone (no NgModules).

## ABP Suite Abstract/Concrete Pattern

For each entity, ABP Suite generates a set of paired files that follow an abstract/concrete inheritance pattern. Only the Doctor list page (`doctor-management/doctors`) still uses it: the other entities' generated pages were replaced by custom screens, and their unused generated view services were removed in #1062.

### Generated File Structure

For an entity named `Doctor`, Suite generates:

| File | Purpose | Modify? |
|------|---------|---------|
| `doctor.abstract.component.ts` | Base class: list management, CRUD actions, filter handling | **DO NOT MODIFY** -- overwritten on regeneration |
| `doctor.component.ts` | Concrete class extending abstract; place customizations here | Safe to modify |
| `doctor-detail.component.ts` | Detail/edit modal component | Safe to modify |
| `doctor.abstract.service.ts` | Abstract service: ListService hooks, proxy calls | **DO NOT MODIFY** |
| `doctor.service.ts` | Concrete service extending abstract | Safe to modify |
| `doctor-detail.abstract.service.ts` | Abstract detail service | **DO NOT MODIFY** |
| `doctor-detail.service.ts` | Concrete detail service | Safe to modify |

### Inheritance Diagram

```mermaid
classDiagram
    class AbstractDoctorComponent {
        <<Directive - DO NOT MODIFY>>
        +list: ListService
        +service: DoctorViewService
        +serviceDetail: DoctorDetailViewService
        +permissionService: PermissionService
        #title: string
        #isActionButtonVisible: boolean
        +ngOnInit()
        +clearFilters()
        +showForm()
        +create()
        +update(record)
        +delete(record)
        +checkActionButtonVisibility()
    }

    class DoctorComponent {
        <<Safe to Modify>>
        -- customizations go here --
    }

    class AbstractDoctorViewService {
        <<DO NOT MODIFY>>
        #proxyService: DoctorService
        #confirmationService: ConfirmationService
        #list: ListService
        +data: PagedResultDto
        +filters: GetDoctorsInput
        +delete(record)
        +hookToQuery()
        +clearFilters()
    }

    class DoctorViewService {
        <<Safe to Modify>>
    }

    class DoctorService {
        <<Proxy - Auto-generated>>
        +create(input)
        +delete(id)
        +get(id)
        +getList(input)
        +update(id, input)
    }

    AbstractDoctorComponent <|-- DoctorComponent
    AbstractDoctorViewService <|-- DoctorViewService
    AbstractDoctorViewService --> DoctorService : uses
    AbstractDoctorComponent --> DoctorViewService : injects
```

### Key Points

- The `@Directive()` decorator on abstract components makes them injectable containers without a template
- Abstract components use `inject()` for dependency injection (Angular 20 pattern, no constructor injection)
- The concrete class (`DoctorComponent`) extends the abstract and is the class referenced in routes
- Permission checks (e.g., `CaseEvaluation.Doctors.Edit`, `CaseEvaluation.Doctors.Delete`) determine action button visibility

## Service Layer Flow

```mermaid
flowchart LR
    A[Component<br/>e.g. DoctorComponent] --> B[ViewService<br/>e.g. DoctorViewService]
    B --> C[ProxyService<br/>e.g. DoctorService]
    C --> D[ABP RestService]
    D --> E[HTTP Request]
    E --> F[Backend Controller]
    F --> G[Application Service]

    style A fill:#e1f5fe
    style B fill:#f3e5f5
    style C fill:#fff3e0
    style D fill:#e8f5e9
```

**Detailed flow for a list query:**

1. **Component** calls `service.hookToQuery()` in `ngOnInit()`
2. **ViewService** (`AbstractDoctorViewService.hookToQuery()`) connects ABP `ListService` to the proxy
3. **ListService** manages pagination, sorting, and filter state; calls `proxyService.getList()` on changes
4. **ProxyService** (`DoctorService`) uses `RestService.request()` to make the HTTP call
5. **RestService** resolves the API URL from `environment.apis.default.url` and adds auth headers
6. Response flows back: HTTP -> RestService -> ProxyService -> ViewService -> Component template

## List Page Pattern

All ABP Suite list pages follow a consistent structure:

- **ABP ListService** for pagination state (skipCount, maxResultCount, sorting, filter)
- **ngx-datatable** (`@swimlane/ngx-datatable`) for table rendering with `[list]` directive binding
- **ABP NgxDatatableDefaultDirective** and **NgxDatatableListDirective** for ABP integration
- **Sidebar or modal** for create/edit forms (via `DoctorDetailViewService.showForm()`)
- **Filter panel** with collapsible accordion for advanced filtering
- **ABP ConfirmationService** for delete confirmations

## Custom (Non-Suite) Components

These components are hand-written and do not follow the abstract/concrete pattern:

| Component | Location | Purpose |
|-----------|----------|---------|
| `AppointmentAddComponent` | `appointments/appointment-add.component.ts` | Multi-section booking form (most complex component) |
| `AppointmentViewComponent` | `appointments/appointment/components/appointment-view.component.ts` | Read-only appointment detail |
| `DoctorAvailabilityGenerateComponent` | `doctor-availabilities/.../doctor-availability-generate.component.ts` | Bulk generate availability slots |
| `PatientProfileComponent` | `patients/patient/components/patient-profile.component.ts` | Self-service patient profile editing |
| `TopHeaderNavbarComponent` | `shared/components/top-header-navbar/` | Custom header for external users |
| `HomeComponent` | `home/home.component.ts` | Landing page with role-based rendering |
| `DashboardComponent` | `dashboard/dashboard.component.ts` | Admin dashboard |
| `AppointmentPacketComponent` | `appointment-packet/appointment-packet.component.ts` | Per-kind packet status display with download and regenerate actions; polls every 5 s while any packet is Generating |

## Shared Components

### TopHeaderNavbarComponent

A standalone component used in the external-user (Patient/Attorney) layout:

```typescript
@Component({
  selector: 'app-top-header-navbar',
  standalone: true,
  imports: [CommonModule],
})
export class TopHeaderNavbarComponent {
  @Input() tenantName = '';
  @Input() userName = '';
  @Input() roleName = '';
  @Input() showProfile = true;
  @Input() showHelp = true;
  @Input() showLogout = true;

  @Output() profileClick = new EventEmitter<void>();
  @Output() helpClick = new EventEmitter<void>();
  @Output() logoutClick = new EventEmitter<void>();
}
```

Used by `HomeComponent` and `AppointmentAddComponent` to provide a simplified navigation header for external users, replacing the full LeptonX topbar.

## Entities Using Suite Pattern

Only **Doctors** (`doctor-management/doctors`) still uses the full abstract/concrete pattern.

The other Suite-generated entities (Appointments, Patients, Doctor Availabilities, Locations, WCAB Offices,
States, Appointment Types, Statuses and Languages, Applicant and Defense Attorneys) had their generated
list and detail pages replaced by custom screens. Their unused generated view services were removed in
#1062. Their `*-routes.ts` files remain and load the custom screens; for example, States load
`InternalConfigurationComponent`.

---

**Related Documentation:**

- [Angular Architecture](ANGULAR-ARCHITECTURE.md)
- [ABP Framework](../architecture/ABP-FRAMEWORK.md)
