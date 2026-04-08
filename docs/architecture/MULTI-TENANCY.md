[Home](../INDEX.md) > [Architecture](./) > Multi-Tenancy

# Multi-Tenancy Strategy

The HCS Case Evaluation Portal uses ABP Framework's multi-tenancy infrastructure with a **doctor-per-tenant** model. Each doctor in the system is their own ABP tenant (organization), providing full data isolation at the database level.

Multi-tenancy is enabled globally via `MultiTenancyConsts.IsEnabled = true` in `Domain.Shared`.

## Core Concept: Doctor-Per-Tenant

When `DoctorTenantAppService` creates a doctor, it performs the following steps in sequence:

1. Creates a new **ABP Tenant** (via the SaaS module)
2. Creates a **Doctor** entity linked to that tenant
3. Creates a **user account** within the new tenant
4. Assigns the appropriate **role** to the user

This means every doctor operates within their own isolated tenant context. Appointments, availability, and related records are scoped to that tenant.

### Tenant Resolution

ABP resolves the current tenant from the incoming request using its built-in resolution strategy:

- Request headers (`__tenant`)
- Cookies
- Route values

Once resolved, `ICurrentTenant` is set for the request lifetime, and ABP's global query filters automatically append `WHERE TenantId = @currentTenant` to all `IMultiTenant` entity queries.

## Entity Classification

Entities fall into two categories based on whether they implement `IMultiTenant`.

### Host-Only Entities

These entities have **no TenantId** and do **not** implement `IMultiTenant`. They live exclusively in the host database and are shared across all tenants.

| Entity | Purpose |
|---|---|
| **Patient** | Global patient records (has an optional TenantId for association but does not implement `IMultiTenant`) |
| **Location** | Physical office locations |
| **State** | US states reference data |
| **WcabOffice** | WCAB office locations |
| **AppointmentType** | Types of medical exams |
| **AppointmentStatus** | Status reference data |
| **AppointmentLanguage** | Language reference data |

### Multi-Tenant Entities

These entities implement `IMultiTenant` and carry a `TenantId` column. They are stored in per-tenant databases (or filtered by TenantId in a shared database).

| Entity | Purpose |
|---|---|
| **Doctor** | The tenant owner entity |
| **DoctorAvailability** | Time slots per doctor/tenant |
| **Appointment** | Bookings within a doctor's tenant |
| **AppointmentAccessor** | Who can view/edit appointments |
| **AppointmentEmployerDetail** | Employer info per appointment |
| **ApplicantAttorney** | Attorney records within tenant |
| **AppointmentApplicantAttorney** | Attorney-appointment links |

```mermaid
flowchart LR
    subgraph HostOnly["Host-Only Entities"]
        Patient
        Location
        State
        WcabOffice
        AppointmentType
        AppointmentStatus
        AppointmentLanguage
    end

    subgraph MultiTenant["Multi-Tenant Entities (IMultiTenant)"]
        Doctor
        DoctorAvailability
        Appointment
        AppointmentAccessor
        AppointmentEmployerDetail
        ApplicantAttorney
        AppointmentApplicantAttorney
    end

    HostOnly --- |"Shared across all tenants"| HostDB[(Host Database)]
    MultiTenant --- |"Filtered by TenantId"| TenantDB[(Tenant Database)]
```

## Dual DbContext Strategy

The application uses two `DbContext` classes that both inherit from a common base. This allows host-only entities to be managed in one context while tenant-scoped entities live in another.

### Inheritance Hierarchy

```mermaid
classDiagram
    class AbpDbContext~T~ {
        <<ABP Framework>>
    }
    class CaseEvaluationDbContextBase~T~ {
        +Configures ABP module tables
        +Identity, OpenIddict, SaaS
        +PermissionManagement, etc.
    }
    class CaseEvaluationDbContext {
        +SetMultiTenancySide(Both)
        +ALL entity configurations
        +Uses IsHostDatabase() guards
        +Connection string: Default
    }
    class CaseEvaluationTenantDbContext {
        +SetMultiTenancySide(Tenant)
        +Tenant-side entities only
        +Per-tenant connection string
        +Migrations in /TenantMigrations/
    }

    AbpDbContext~T~ <|-- CaseEvaluationDbContextBase~T~
    CaseEvaluationDbContextBase~T~ <|-- CaseEvaluationDbContext
    CaseEvaluationDbContextBase~T~ <|-- CaseEvaluationTenantDbContext
```

### CaseEvaluationDbContext (Host)

- Configured with `SetMultiTenancySide(MultiTenancySides.Both)`
- Contains **all** entity configurations (host and tenant)
- Uses `builder.IsHostDatabase()` guards to conditionally configure host-only entities
- Connection string: `"Default"`

### CaseEvaluationTenantDbContext (Tenant)

- Configured with `SetMultiTenancySide(MultiTenancySides.Tenant)`
- Contains only **tenant-side** entity configurations (re-declares them)
- Each tenant can have its own connection string, or share the host database with TenantId filtering
- Migrations are stored in the `/TenantMigrations/` folder, separate from host migrations

## Cross-Tenant Data Access

Tenant entities frequently need to reference host-side data. For example, an `Appointment` (tenant-scoped) references both a `Patient` and a `Location` (both host-side).

```mermaid
flowchart TD
    subgraph TenantDB["Tenant Database"]
        Appointment["Appointment\n(IMultiTenant)"]
    end

    subgraph HostDB["Host Database"]
        Patient["Patient\n(Host-Only)"]
        Location["Location\n(Host-Only)"]
    end

    Appointment -- "PatientId (FK, NoAction)" --> Patient
    Appointment -- "LocationId (FK, NoAction)" --> Location
```

Key design decisions for cross-tenant references:

- **FK relationships** use `DeleteBehavior.NoAction` to prevent cross-database cascade issues. Since host and tenant data may live in different physical databases, cascade deletes cannot span that boundary.
- **`IDataFilter<IMultiTenant>`** can be used to temporarily disable tenant filtering when a service needs to read across tenants. For example, `DoctorsAppService` uses this to list all doctors from the host context regardless of the current tenant.

## Tenant Resolution Flow

The following sequence shows how an incoming request is resolved to a specific tenant and how data queries are automatically filtered.

```mermaid
sequenceDiagram
    participant Client
    participant Middleware as ABP Tenant Resolution Middleware
    participant ICurrentTenant
    participant DataFilter as IDataFilter<IMultiTenant>
    participant DbContext
    participant Database

    Client->>Middleware: HTTP Request with __tenant header/cookie/route
    Middleware->>Middleware: Resolve TenantId from request
    Middleware->>ICurrentTenant: Set current tenant
    ICurrentTenant-->>Middleware: TenantId active for request scope

    Client->>DbContext: Service calls repository method
    DbContext->>DataFilter: Check if IMultiTenant filter is enabled
    DataFilter-->>DbContext: Filter enabled, append TenantId = @current
    DbContext->>Database: SELECT ... WHERE TenantId = @currentTenantId
    Database-->>DbContext: Filtered results
    DbContext-->>Client: Return tenant-scoped data
```

## Tenant Data Seeding

Data seeding operates at two levels:

### Per-Tenant Seeding

- `ExternalUserRoleDataSeedContributor` creates roles within each tenant:
  - Patient
  - Claim Examiner
  - Applicant Attorney
  - Defense Attorney

### Migration and Seed Orchestration

- `CaseEvaluationDbMigrationService` iterates over all tenants and runs:
  1. Database migrations (using `CaseEvaluationTenantDbContext` and the `/TenantMigrations/` folder)
  2. Data seed contributors scoped to each tenant

This ensures every new tenant gets its schema and baseline data automatically upon creation.

## Related Documentation

- [Architecture Overview](OVERVIEW.md)
- [EF Core Design](../database/EF-CORE-DESIGN.md)
- [Domain Model](../backend/DOMAIN-MODEL.md)
- [Domain Overview](../business-domain/DOMAIN-OVERVIEW.md)
