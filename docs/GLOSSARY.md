[Home](INDEX.md) > Glossary

# HCS Case Evaluation Portal Glossary

A comprehensive glossary of terms used throughout the HCS Case Evaluation Portal, a healthcare workers' compensation IME scheduling application built with .NET 10, Angular 20, and ABP Framework.

---

## Business Domain Terms

| Term | Definition |
|------|------------|
| **IME (Independent Medical Examination)** | A medical examination conducted by a physician who has not previously treated the injured worker. IMEs are requested by an insurance company, employer, or attorney to obtain an independent opinion on the nature and extent of a work-related injury, the need for treatment, or the degree of permanent disability. The core business process that this portal schedules and manages. |
| **QME (Qualified Medical Evaluator)** | A physician certified by the California Division of Workers' Compensation (DWC) to perform medical-legal evaluations in workers' compensation cases. When the injured worker is unrepresented by an attorney, a QME is selected from a three-member panel provided by the DWC. QMEs must pass an exam and maintain their certification through continuing education. |
| **AME (Agreed Medical Evaluator)** | A physician agreed upon by both the applicant attorney and the defense attorney (or insurance carrier) to perform a medical-legal evaluation. AMEs are used when the injured worker has attorney representation and both parties can mutually agree on an evaluator, bypassing the QME panel process. |
| **WCAB (Workers' Compensation Appeals Board)** | The California state judicial body that adjudicates disputes in workers' compensation cases. The WCAB oversees hearings and appeals related to benefits, treatment, and disability ratings. The portal tracks WCAB office locations associated with cases. |
| **Panel Number** | The workers' compensation panel or case number assigned to an evaluation. This number identifies the specific workers' comp case or panel and is used to link appointments back to the underlying claim and legal proceedings. |
| **Confirmation Number** | An auto-generated appointment identifier in the format `A00001` (letter "A" followed by a zero-padded sequential number). This number is provided to all parties (attorneys, applicants, claim examiners) as the primary reference for locating and managing a specific appointment. |
| **Case Evaluation** | The overall examination process encompassing the scheduling, execution, and documentation of an independent medical examination. A case evaluation includes the appointment booking, the medical examination itself, and any associated administrative tasks such as record review and report generation. |
| **Applicant Attorney** | The attorney representing the injured worker (applicant) in a workers' compensation claim. The applicant attorney advocates for the worker's benefits and participates in scheduling evaluations, selecting AMEs, or requesting QME panels. The portal stores applicant attorney contact details and firm information linked to appointments. |
| **Defense Attorney** | The attorney representing the employer or insurance carrier in a workers' compensation claim. The defense attorney works to manage the employer's liability exposure and participates in evaluation scheduling, AME selection, and dispute resolution before the WCAB. |
| **Claim Examiner** | The insurance company representative (also called a claims adjuster) responsible for managing a workers' compensation claim. The claim examiner authorizes medical evaluations, coordinates with attorneys, and makes benefit determinations. They are often the party that initiates an IME request. |
| **Booking Status** | The availability state of a doctor's appointment slot. Uses integer codes: **Available (8)** indicates the slot is open for booking; **Booked (9)** indicates the slot has been reserved by a confirmed appointment; **Reserved (10)** indicates the slot is temporarily held (e.g., during the booking process) but not yet confirmed. |
| **Appointment Status** | The lifecycle state of an appointment, tracked through 13 discrete statuses. Statuses progress from **Pending** (initial creation) through various intermediate states (such as confirmed, rescheduled, or completed) up to **CancellationRequested** (a cancellation has been initiated). These statuses drive workflow logic, notifications, and reporting throughout the portal. |

---

## Technical Terms (DDD & Architecture)

| Term | Definition |
|------|------------|
| **Aggregate Root** | The entry-point entity for a cluster of related domain objects that are treated as a single unit for data changes. All external access to the cluster goes through the aggregate root, which enforces consistency rules and invariants. For example, `Doctor` is an aggregate root that owns `DoctorAppointmentType` and `DoctorLocation` as child entities within its aggregate boundary. |
| **Entity** | A domain object that is distinguished by its identity (typically a primary key) rather than its attributes. Two entities with the same attributes but different IDs are considered different objects. Entities have a lifecycle and are tracked across time. Examples include `Appointment`, `Patient`, and `Doctor`. |
| **Value Object** | An immutable domain object that is defined entirely by its attributes and has no distinct identity. Two value objects with the same properties are considered equal. Value objects are used for concepts like addresses, date ranges, or money amounts where identity is irrelevant. |
| **Domain Service / Manager** | A class containing business logic that does not naturally belong to a single entity or value object. In this codebase, domain services follow the naming convention `*Manager` (e.g., `AppointmentManager`, `DoctorManager`). They encapsulate complex domain rules, cross-entity operations, and invariant enforcement at the domain layer. |
| **Application Service / AppService** | The orchestration layer that coordinates domain objects and infrastructure services to fulfill use cases. Application services follow the naming convention `*AppService` and serve as the primary entry point for API operations. They handle transaction management, authorization, DTO mapping, and delegation to domain services, but contain no core business logic themselves. |
| **DTO (Data Transfer Object)** | A plain data class used to transport data between the API layer and clients. DTOs decouple the internal domain model from the external API contract, allowing each to evolve independently. Common patterns include `Create*Dto`, `Update*Dto`, and `*Dto` (for responses). DTOs contain no behavior and are typically flat structures optimized for serialization. |
| **Repository** | An abstraction over data access that provides a collection-like interface for retrieving and persisting aggregate roots. Repositories hide the underlying storage mechanism (e.g., Entity Framework Core, database) from the domain and application layers. Custom repository interfaces are defined in the domain layer and implemented in the infrastructure layer. |
| **Multi-Tenancy** | The architectural pattern used to isolate data on a per-doctor basis. Each doctor operates within their own tenant, meaning their appointments, availability, and configurations are logically separated from other doctors' data. The ABP Framework provides automatic tenant-level data filtering so queries only return data belonging to the current tenant. |
| **Host** | The system administration level that sits above all tenants. Host-level access is used for global configuration, managing tenants (doctors), seeding shared reference data (e.g., WCAB offices, states), and performing cross-tenant operations. Host users can see and manage all tenants. |
| **Tenant** | A doctor's isolated data space within the multi-tenant system. Each tenant has its own set of appointments, patients, availability schedules, and configurations. Tenant boundaries are enforced automatically by ABP's data filters so that one doctor's data is never visible to another doctor's context. |

---

## ABP Framework Terms

| Term | Definition |
|------|------------|
| **ABP Module** | A composable, self-contained application building block in the ABP Framework. Each module encapsulates its own domain, application, HTTP API, and database layers. Modules are defined by classes ending in `*Module.cs` that configure dependencies, services, and middleware. The portal is composed of multiple modules that are wired together at startup. |
| **FullAuditedAggregateRoot** | An ABP base class for aggregate roots that automatically tracks full audit information: `CreationTime`, `CreatorId`, `LastModificationTime`, `LastModifierId`, `DeletionTime`, `DeleterId`, and `IsDeleted`. It combines aggregate root identity with soft-delete support and complete audit trails, eliminating the need to manually manage these fields. |
| **IMultiTenant** | An ABP interface that adds a `TenantId` property (of type `Guid?`) to an entity. When an entity implements `IMultiTenant`, ABP's global query filters automatically scope all database queries to the current tenant's data, ensuring tenant isolation without explicit filtering in every query. |
| **DataSeedContributor** | A class that implements `IDataSeedContributor` to seed initial or reference data into the database during application startup or migration. Data seed contributors are used to populate lookup tables (e.g., appointment statuses, states, WCAB offices) and ensure that required baseline data exists in every environment. |
| **RemoteService** | An ABP attribute (`[RemoteService]`) applied to application service classes or interfaces to indicate that they should be exposed as HTTP API endpoints. ABP's auto API controller system uses this attribute to dynamically generate RESTful controllers from application services without manually writing controller code. |
| **ConfigureByConvention** | An ABP Entity Framework Core method called during `OnModelCreating` that auto-configures entity properties based on ABP conventions. It applies standard mappings for audit properties, soft-delete fields, multi-tenancy columns, and extra properties, reducing the amount of explicit Fluent API configuration required. |
| **LookupDto** | A lightweight DTO used to populate dropdown menus, select lists, and autocomplete controls. A `LookupDto` typically contains only an `Id` and a `DisplayName` (or similar text field), providing the minimum data needed for selection UI elements without transferring full entity details. |
| **PagedResultDto** | An ABP DTO that wraps paginated query results. It contains a `TotalCount` (the total number of matching records) and an `Items` collection (the current page of results). This standardized shape allows the frontend to render pagination controls consistently across all list views. |
| **ListService** | An Angular service provided by the ABP Framework (`@abp/ng.core`) that manages client-side list state including pagination, sorting, and filtering. `ListService` integrates with ABP's `PagedResultDto` responses and emits observable streams that drive Angular data tables and grid components. |
| **PermissionGroup** | An ABP organizational unit for grouping related permissions together. Permission groups appear as sections in the ABP permission management UI and allow administrators to grant or revoke related capabilities (e.g., all appointment-related permissions) as a logical set. |
| **LeptonX** | ABP's commercial enterprise UI theme providing a modern, responsive layout for both Angular and MVC applications. LeptonX includes pre-built page layouts, navigation menus, theme customization, and accessibility features. The portal uses LeptonX as its base visual framework. |

---

## Entity Glossary

| Entity | Description |
|--------|-------------|
| **Appointment** | The central entity representing a scheduled IME, linking a patient, doctor, location, appointment type, and time slot together with status tracking, attorney details, and confirmation number. |
| **Patient** | The injured worker (applicant) undergoing evaluation, storing personal and contact information, date of birth, and employer details. |
| **Doctor** | The physician performing evaluations; serves as the aggregate root for doctor-specific data and as the basis for tenant isolation. |
| **DoctorAvailability** | A time slot definition for a doctor at a specific location, specifying the date, start/end times, and booking status (Available, Booked, Reserved). |
| **Location** | A physical office or facility where evaluations take place, storing address details, contact information, and geographic coordinates. |
| **AppointmentType** | A classification of the evaluation type (e.g., IME, QME, AME, follow-up), determining the nature and requirements of the scheduled examination. |
| **AppointmentStatus** | A reference/lookup entity representing one of the 13 possible lifecycle states an appointment can be in, from Pending through CancellationRequested. |
| **AppointmentLanguage** | The language associated with an appointment, indicating the language in which the evaluation will be conducted or for which an interpreter is needed. |
| **WcabOffice** | A Workers' Compensation Appeals Board office location, used as reference data to associate cases with the appropriate WCAB jurisdiction. |
| **ApplicantAttorney** | An attorney entity representing the injured worker's legal representative, storing name, firm, contact details, and bar number. |
| **AppointmentAccessor** | An entity granting external parties (such as attorneys or claim examiners) controlled access to view or manage specific appointment details. |
| **AppointmentEmployerDetail** | Employer information associated with an appointment, capturing the employer name, address, insurance carrier, and claim number for the injured worker's case. |
| **AppointmentApplicantAttorney** | A join entity linking an appointment to its applicant attorney, enabling the many-to-many relationship between appointments and attorneys. |
| **DoctorAppointmentType** | A join entity within the Doctor aggregate that defines which appointment types a specific doctor offers, linking a doctor to their supported evaluation types. |
| **DoctorLocation** | A join entity within the Doctor aggregate that associates a doctor with the locations where they practice, enabling a doctor to operate across multiple offices. |
| **State** | A reference/lookup entity representing a U.S. state, used for address standardization and dropdown population across the portal. |

---

## Related Documentation

- [Domain Overview](business-domain/DOMAIN-OVERVIEW.md) - Business domain concepts and workflows
- [Architecture Overview](architecture/OVERVIEW.md) - System architecture and design decisions
- [Domain Model](backend/DOMAIN-MODEL.md) - Entity relationships and aggregate boundaries
- [Enums and Constants](backend/ENUMS-AND-CONSTANTS.md) - Status codes, enumerations, and constant values
