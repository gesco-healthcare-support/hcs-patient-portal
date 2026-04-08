# Domain Model

[Home](../INDEX.md) > [Backend](./) > Domain Model

---

This page provides a quick-reference index to all domain entities. For full details on any entity (fields, relationships, business rules, gotchas), see its dedicated CLAUDE.md file linked below.

## Entity Index

| Entity | Base Class | Multi-Tenant | Scope | CLAUDE.md |
|--------|-----------|-------------|-------|-----------|
| Appointment | `FullAuditedAggregateRoot<Guid>` | Yes (`IMultiTenant`) | Tenant | [CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md) |
| Doctor | `FullAuditedAggregateRoot<Guid>` | Yes (`IMultiTenant`) | Tenant | [CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/Doctors/CLAUDE.md) |
| DoctorAvailability | `FullAuditedAggregateRoot<Guid>` | Yes (`IMultiTenant`) | Tenant | [CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/CLAUDE.md) |
| Patient | `FullAuditedAggregateRoot<Guid>` | No (has TenantId, not interface) | Mixed | [CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/Patients/CLAUDE.md) |
| ApplicantAttorney | `FullAuditedAggregateRoot<Guid>` | Yes (`IMultiTenant`) | Tenant | [CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/ApplicantAttorneys/CLAUDE.md) |
| AppointmentAccessor | `FullAuditedEntity<Guid>` | Yes (`IMultiTenant`) | Tenant | [CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/AppointmentAccessors/CLAUDE.md) |
| AppointmentApplicantAttorney | `FullAuditedAggregateRoot<Guid>` | Yes (`IMultiTenant`) | Tenant | [CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/AppointmentApplicantAttorneys/CLAUDE.md) |
| AppointmentEmployerDetail | `FullAuditedAggregateRoot<Guid>` | Yes (`IMultiTenant`) | Tenant | [CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/AppointmentEmployerDetails/CLAUDE.md) |
| Location | `FullAuditedAggregateRoot<Guid>` | No | Host | [CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/Locations/CLAUDE.md) |
| State | `FullAuditedAggregateRoot<Guid>` | No | Host | [CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/States/CLAUDE.md) |
| WcabOffice | `FullAuditedAggregateRoot<Guid>` | No | Host | [CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/WcabOffices/CLAUDE.md) |
| AppointmentType | `FullAuditedEntity<Guid>` | No | Host | [CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/AppointmentTypes/CLAUDE.md) |
| AppointmentLanguage | `FullAuditedEntity<Guid>` | No | Host | [CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/AppointmentLanguages/CLAUDE.md) |
| AppointmentStatus | `FullAuditedEntity<Guid>` | No | Host | [CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/AppointmentStatuses/CLAUDE.md) |
| Book | `AuditedAggregateRoot<Guid>` | No | Host | [CLAUDE.md](../../src/HealthcareSupport.CaseEvaluation.Domain/Books/CLAUDE.md) |

## Join Tables (no standalone CLAUDE.md)

| Table | Connects | Key |
|-------|----------|-----|
| DoctorAppointmentType | Doctor ↔ AppointmentType | Composite {DoctorId, AppointmentTypeId} |
| DoctorLocation | Doctor ↔ Location | Composite {DoctorId, LocationId} |

## Entity Base Class Guide

| Base Class | Provides | Used By |
|-----------|----------|---------|
| `FullAuditedAggregateRoot<Guid>` | Id, CreationTime, CreatorId, LastModificationTime, LastModifierId, IsDeleted, DeleterId, DeletionTime, ExtraProperties, ConcurrencyStamp | Most entities |
| `FullAuditedEntity<Guid>` | Same as above but without ExtraProperties and ConcurrencyStamp | AppointmentType, AppointmentLanguage, AppointmentStatus, AppointmentAccessor |
| `AuditedAggregateRoot<Guid>` | Same as FullAudited but without soft-delete fields | Book (demo entity) |

For entity relationships, see [Entity Relationships](ENTITY-RELATIONSHIPS.md).

---

**Related:**
- [Entity Relationships](ENTITY-RELATIONSHIPS.md) -- FK diagram and relationship details
- [Multi-Tenancy](../architecture/MULTI-TENANCY.md) -- how tenant scoping works
- [DDD Layers](../architecture/DDD-LAYERS.md) -- where entities fit in the architecture
