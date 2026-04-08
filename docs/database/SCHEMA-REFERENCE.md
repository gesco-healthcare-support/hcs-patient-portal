# Schema Reference

[Home](../INDEX.md) > [Database](./) > Schema Reference

---

For per-entity column details, see the feature CLAUDE.md files linked in [Domain Model](../backend/DOMAIN-MODEL.md). This page covers database-level concerns not found in the entity documentation.

## Table Naming

All application tables use the prefix `App` (from `CaseEvaluationConsts.DbTablePrefix`):

| Entity | Table Name |
|--------|-----------|
| Appointment | `AppAppointments` |
| Doctor | `AppDoctors` |
| DoctorAvailability | `AppDoctorAvailabilities` |
| Patient | `AppPatients` |
| Location | `AppLocations` |
| State | `AppStates` |
| WcabOffice | `AppWcabOffices` |
| AppointmentType | `AppAppointmentTypes` |
| (etc.) | `App{PluralEntityName}` |

Join tables: `AppDoctorAppointmentType`, `AppDoctorLocation`

## SQL Type Conventions

EF Core maps C# types to SQL Server types:

| C# Type | SQL Type | Notes |
|---------|---------|-------|
| `string` with `HasMaxLength(N)` | `nvarchar(N)` | Unicode by default |
| `string` without max length | `nvarchar(max)` | State.Name has no max length |
| `Guid` | `uniqueidentifier` | All PKs and FKs |
| `Guid?` | `uniqueidentifier NULL` | Optional FKs |
| `DateTime` | `datetime2` | ABP default |
| `decimal` | `decimal(18,2)` | Location.ParkingFee |
| `bool` | `bit` | Location.IsActive, WcabOffice.IsActive |
| `int` (enum) | `int` | All enum-backed fields |

## ABP System Tables

ABP Framework creates its own tables (not prefixed with `App`):

| Module | Tables | Purpose |
|--------|--------|---------|
| Identity | `AbpUsers`, `AbpRoles`, `AbpUserRoles`, `AbpClaimTypes`, ... | User/role management |
| OpenIddict | `OpenIddictApplications`, `OpenIddictScopes`, `OpenIddictTokens`, `OpenIddictAuthorizations` | OAuth2 clients and tokens |
| SaaS | `SaasTenants`, `SaasEditions`, `SaasTenantConnectionStrings` | Multi-tenancy |
| PermissionManagement | `AbpPermissionGrants`, `AbpPermissionGroups` | Permission storage |
| SettingManagement | `AbpSettings` | Application settings |
| AuditLogging | `AbpAuditLogs`, `AbpAuditLogActions`, `AbpEntityChanges`, `AbpEntityPropertyChanges` | Audit trail |
| BackgroundJobs | `AbpBackgroundJobs` | Job queue |
| FeatureManagement | `AbpFeatureGroups`, `AbpFeatureValues` | Feature flags |

## Dual Database Strategy

The project uses two DbContexts that produce separate migration sets:

| Context | Migrations Path | Contains |
|---------|----------------|----------|
| `CaseEvaluationDbContext` | `Migrations/` | All entities (host + tenant via `IsHostDatabase()` guards) |
| `CaseEvaluationTenantDbContext` | `TenantMigrations/` | Tenant-scoped entities only |

See [EF Core Design](EF-CORE-DESIGN.md) for full details on the dual DbContext strategy.

---

**Related:**
- [Domain Model](../backend/DOMAIN-MODEL.md) -- entity index with links to per-entity details
- [EF Core Design](EF-CORE-DESIGN.md) -- dual DbContext strategy
- [Migration Guide](MIGRATION-GUIDE.md) -- creating and applying migrations
- [Entity Relationships](../backend/ENTITY-RELATIONSHIPS.md) -- FK diagram
