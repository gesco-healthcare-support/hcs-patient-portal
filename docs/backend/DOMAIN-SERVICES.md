# Domain Services (Managers)

[Home](../INDEX.md) > [Backend](./) > Domain Services

---

Domain managers enforce business rules during entity creation and updates. Not every entity has a manager -- simple lookup entities use the AppService directly. For the full business rules of each feature, see its CLAUDE.md file linked below.

## Manager Index

| Manager | Entity | Key Responsibilities | CLAUDE.md |
|---------|--------|---------------------|-----------|
| `AppointmentManager` | Appointment | Create with slot validation, update with frozen field enforcement | [Appointments](../../src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md) |
| `DoctorManager` | Doctor | Create/update with M2M collection sync (AppointmentTypes, Locations) | [Doctors](../../src/HealthcareSupport.CaseEvaluation.Domain/Doctors/CLAUDE.md) |
| `DoctorAvailabilityManager` | DoctorAvailability | Create/update slots, bulk generation | [DoctorAvailabilities](../../src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/CLAUDE.md) |
| `PatientManager` | Patient | Create with get-or-create pattern, IdentityUser auto-creation | [Patients](../../src/HealthcareSupport.CaseEvaluation.Domain/Patients/CLAUDE.md) |
| `LocationManager` | Location | Create/update with length validation | [Locations](../../src/HealthcareSupport.CaseEvaluation.Domain/Locations/CLAUDE.md) |
| `ApplicantAttorneyManager` | ApplicantAttorney | Create/update with length validation | [ApplicantAttorneys](../../src/HealthcareSupport.CaseEvaluation.Domain/ApplicantAttorneys/CLAUDE.md) |
| `WcabOfficeManager` | WcabOffice | Create/update with length validation | [WcabOffices](../../src/HealthcareSupport.CaseEvaluation.Domain/WcabOffices/CLAUDE.md) |
| `StateManager` | State | Create/update (name only) | [States](../../src/HealthcareSupport.CaseEvaluation.Domain/States/CLAUDE.md) |
| `AppointmentTypeManager` | AppointmentType | Create/update with length validation | [AppointmentTypes](../../src/HealthcareSupport.CaseEvaluation.Domain/AppointmentTypes/CLAUDE.md) |
| `AppointmentLanguageManager` | AppointmentLanguage | Create/update with length validation | [AppointmentLanguages](../../src/HealthcareSupport.CaseEvaluation.Domain/AppointmentLanguages/CLAUDE.md) |
| `AppointmentStatusManager` | AppointmentStatus | Create/update with length validation | [AppointmentStatuses](../../src/HealthcareSupport.CaseEvaluation.Domain/AppointmentStatuses/CLAUDE.md) |

## Pattern

All managers follow the same structure:

```csharp
// Real example from src/.../Domain/States/StateManager.cs
public class StateManager : DomainService
{
    public virtual async Task<State> CreateAsync(string name)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        var state = new State(GuidGenerator.Create(), name);
        return await _stateRepository.InsertAsync(state);
    }

    public virtual async Task<State> UpdateAsync(Guid id, string name, string? concurrencyStamp = null)
    {
        var state = await _stateRepository.GetAsync(id);
        state.Name = name;
        state.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _stateRepository.UpdateAsync(state);
    }
}
```

The AppService always delegates to the manager for create/update -- never calls `_repository.InsertAsync()` directly. This ensures business rules are enforced regardless of the caller.

## Entities Without Managers

These entities have no DomainService and are managed directly by their parent feature's AppService (typically as part of the Appointment booking flow):
- AppointmentAccessor
- AppointmentApplicantAttorney
- AppointmentEmployerDetail

---

**Related:**
- [Application Services](APPLICATION-SERVICES.md) -- how AppServices delegate to managers
- [DDD Layers](../architecture/DDD-LAYERS.md) -- where domain services fit in the architecture
