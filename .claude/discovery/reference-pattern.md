# Reference Pattern — Appointments

The Appointments feature is the reference implementation. Trace this when adding a new feature.

## File Inventory

| Layer | File | Key Detail |
|---|---|---|
| Domain entity | `src/.../Domain/Appointments/Appointment.cs` | `FullAuditedAggregateRoot<Guid>, IMultiTenant` |
| Domain manager | `src/.../Domain/Appointments/AppointmentManager.cs` | `DomainService`, `CreateAsync` / `UpdateAsync` |
| Nav props wrapper | `src/.../Domain/Appointments/AppointmentWithNavigationProperties.cs` | Projection for eager-loaded queries |
| Repo interface | `src/.../Domain/Appointments/IAppointmentRepository.cs` | Extends `IRepository<Appointment, Guid>`; custom `GetListWithNavigationPropertiesAsync` |
| Domain.Shared consts | `src/.../Domain.Shared/Appointments/AppointmentConsts.cs` | MaxLength values, `GetDefaultSorting()` |
| Domain.Shared enum | `src/.../Domain.Shared/Enums/AppointmentStatusType.cs` | 13-state lifecycle |
| DTO — read | `src/.../Application.Contracts/Appointments/AppointmentDto.cs` | Extends `FullAuditedEntityDto<Guid>, IHasConcurrencyStamp` |
| DTO — create | `src/.../Application.Contracts/Appointments/AppointmentCreateDto.cs` | Flat; uses `[StringLength(AppointmentConsts.X)]` |
| DTO — update | `src/.../Application.Contracts/Appointments/AppointmentUpdateDto.cs` | Implements `IHasConcurrencyStamp` |
| DTO — filter | `src/.../Application.Contracts/Appointments/GetAppointmentsInput.cs` | Extends `PagedAndSortedResultRequestDto` |
| DTO — nav props | `src/.../Application.Contracts/Appointments/AppointmentWithNavigationPropertiesDto.cs` | Rich output with 6 nav props |
| Service interface | `src/.../Application.Contracts/Appointments/IAppointmentsAppService.cs` | Extends `IApplicationService` |
| Permissions | `src/.../Application.Contracts/Permissions/CaseEvaluationPermissions.cs` | Nested static class `Appointments` with `Default/Create/Edit/Delete` |
| App service | `src/.../Application/Appointments/AppointmentsAppService.cs` | `CaseEvaluationAppService, IAppointmentsAppService`; `[RemoteService(IsEnabled = false)]` |
| Mapper | `src/.../Application/CaseEvaluationApplicationMappers.cs` | `[Mapper]` partial classes: Entity->Dto, WithNavProps->WithNavPropsDto, Entity->LookupDto |
| EF Core repo | `src/.../EntityFrameworkCore/Appointments/EfCoreAppointmentRepository.cs` | 5-way LEFT JOIN with accessor subquery |
| Controller | `src/.../HttpApi/Controllers/Appointments/AppointmentController.cs` | `AbpController, IAppointmentsAppService`; 14 manually delegated methods |
| Angular proxy | `angular/src/app/proxy/appointments/appointment.service.ts` | `@Injectable({ providedIn: 'root' })`; `inject(RestService)` |
| Angular component | `angular/src/app/appointments/appointment/components/appointment.component.ts` | Standalone; extends `AbstractAppointmentComponent` |
| Angular abstract | `angular/src/app/appointments/appointment/components/appointment.abstract.component.ts` | `@Directive()` base with CRUD wiring |
| Angular route | `angular/src/app/appointments/appointment/appointment-routes.ts` | `authGuard + permissionGuard` |

## Request Flow

```
Browser -> Angular Component -> Proxy Service (RestService.request)
  -> HttpApi Controller (AbpController) -> AppService (IAppointmentsAppService)
    -> DomainManager (AppointmentManager.CreateAsync)
      -> Entity (Appointment constructor)
    -> Repository (IAppointmentRepository via EfCoreAppointmentRepository)
      -> EF Core DbContext -> SQL Server
```

## Entity Shape

```
Appointment : FullAuditedAggregateRoot<Guid>, IMultiTenant
  5 FKs: PatientId, IdentityUserId, AppointmentTypeId, LocationId, DoctorAvailabilityId
  Status field: AppointmentStatusType (13-state enum)
  Auto-generated: RequestConfirmationNumber ("A#####" format)
```

## DTO Mapping Chain (Riok.Mapperly)

```
Appointment -> AppointmentDto                    (AppointmentToAppointmentDtoMappers)
AppointmentWithNavigationProperties -> ...Dto    (AppointmentWithNavProps...Mapper)
Appointment -> LookupDto<Guid>                   (AppointmentToLookupDtoGuidMapper, AfterMap sets DisplayName = RequestConfirmationNumber)
```

## Permissions Pattern

```csharp
// CaseEvaluationPermissions.cs
public static class Appointments
{
    public const string Default = GroupName + ".Appointments";
    public const string Create = Default + ".Create";
    public const string Edit = Default + ".Edit";
    public const string Delete = Default + ".Delete";
}

// CaseEvaluationPermissionDefinitionProvider.cs
var perm = myGroup.AddPermission(CaseEvaluationPermissions.Appointments.Default, L("Permission:Appointments"));
perm.AddChild(CaseEvaluationPermissions.Appointments.Create, L("Permission:Create"));
perm.AddChild(CaseEvaluationPermissions.Appointments.Edit, L("Permission:Edit"));
perm.AddChild(CaseEvaluationPermissions.Appointments.Delete, L("Permission:Delete"));
```

## Conventions Observed

1. Entity extends `FullAuditedAggregateRoot<Guid>` (soft-delete + audit fields)
2. `IMultiTenant` for tenant-scoped data
3. Domain manager (`DomainService`) enforces business rules — AppService delegates to it
4. `[RemoteService(IsEnabled = false)]` on AppService prevents ABP duplicate route registration
5. Manual controller extends `AbpController` AND implements `IAppointmentsAppService`
6. Riok.Mapperly `[Mapper]` partial classes extend `MapperBase<TSource, TDest>`
7. EF Core repository uses LINQ joins (not navigation properties) for complex queries
8. Angular uses ABP Suite abstract/concrete component pattern for list pages
9. Permissions follow parent + 3 children pattern (Default, Create, Edit, Delete)
10. Consts class defines MaxLength values referenced by `[StringLength]` on DTOs
