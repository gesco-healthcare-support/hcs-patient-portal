using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.Enums;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// Dev seeder: gives each freshly provisioned office a window of bookable, future-dated
/// availability slots so the booking flow has something to book -- a fresh office otherwise
/// has zero slots and an empty calendar. Slots start beyond the 3-day booking lead time, at
/// the office's seeded clinic location, and accept the office's seeded appointment types.
///
/// Tenant-scoped (no slots in the host DB) + idempotent (skips when the office already has
/// slots). autoSave:false so the slot + M2M-type rows flush with the rest of the office seed
/// in one FK-ordered SaveChanges. Development-gated.
/// </summary>
public class OfficeAvailabilitySeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<DoctorAvailability, Guid> _slotRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<OfficeAvailabilitySeedContributor> _logger;

    public OfficeAvailabilitySeedContributor(
        IRepository<DoctorAvailability, Guid> slotRepository,
        IGuidGenerator guidGenerator,
        ILogger<OfficeAvailabilitySeedContributor> logger)
    {
        _slotRepository = slotRepository;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (!IsDevelopment())
        {
            return;
        }

        // Slots are office data; the host DB has none.
        if (context?.TenantId == null)
        {
            return;
        }

        // Idempotent: leave an office that already has slots alone.
        if (await _slotRepository.GetCountAsync() > 0)
        {
            return;
        }

        var officeId = context.TenantId.Value;
        var appointmentTypeIds = new[]
        {
            CaseEvaluationSeedIds.AppointmentTypes.Ame,
            CaseEvaluationSeedIds.AppointmentTypes.Ime,
            CaseEvaluationSeedIds.AppointmentTypes.PanelQme,
        };

        // ~10 bookable days starting day+5 (past the 3-day lead time), one morning slot
        // each (09:00-12:00, capacity 3) at the seeded north clinic, accepting all types.
        var seeded = 0;
        for (var dayOffset = 5; dayOffset <= 14; dayOffset++)
        {
            var slot = new DoctorAvailability(
                id: _guidGenerator.Create(),
                locationId: CaseEvaluationSeedIds.Locations.DemoClinicNorth,
                availableDate: DateTime.Today.AddDays(dayOffset),
                fromTime: new TimeOnly(9, 0),
                toTime: new TimeOnly(12, 0),
                bookingStatusId: BookingStatus.Available,
                capacity: 3);
            slot.TenantId = officeId;
            foreach (var appointmentTypeId in appointmentTypeIds)
            {
                slot.AddAppointmentType(appointmentTypeId);
            }
            await _slotRepository.InsertAsync(slot, autoSave: false);
            seeded++;
        }

        _logger.LogInformation(
            "OfficeAvailabilitySeedContributor: seeded {Count} bookable slots for office {OfficeId}.",
            seeded, officeId);
    }

    private static bool IsDevelopment()
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }
}
