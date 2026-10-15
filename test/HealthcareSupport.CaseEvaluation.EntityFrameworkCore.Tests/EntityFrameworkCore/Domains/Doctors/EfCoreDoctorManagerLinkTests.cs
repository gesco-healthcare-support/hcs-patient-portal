using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Saas.Tenants;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Doctors;

/// <summary>
/// <see cref="DoctorManager"/>'s appointment-type and location linking, against the real
/// repositories: ids that exist are linked, and a list of ids none of which exist is ignored
/// rather than clearing what the doctor already has. That last case is the one that could
/// silently strip a doctor's links, so it is checked with the existing links present.
///
/// <para>A tenant (practice) has at most one doctor (unique index on <c>AppDoctors.TenantId</c>,
/// which is also a foreign key to the tenants table), and the seeded TenantA already has one, so
/// each fact creates a practice of its own through <c>ITenantManager</c>. Appointment types and
/// locations are tenant-scoped too (the seeded ones belong to the host and are invisible inside
/// the practice), so the practice gets one of each.</para>
/// </summary>
public class EfCoreDoctorManagerLinkTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly DoctorManager _manager;
    private readonly IDoctorRepository _doctors;
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantManager _tenantManager;
    private readonly IRepository<Tenant, Guid> _tenants;
    private readonly IRepository<AppointmentType, Guid> _appointmentTypes;
    private readonly IRepository<Location, Guid> _locations;
    private readonly Guid _typeId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();
    private Guid? _practiceId;

    public EfCoreDoctorManagerLinkTests()
    {
        _manager = GetRequiredService<DoctorManager>();
        _doctors = GetRequiredService<IDoctorRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _tenantManager = GetRequiredService<ITenantManager>();
        _tenants = GetRequiredService<IRepository<Tenant, Guid>>();
        _appointmentTypes = GetRequiredService<IRepository<AppointmentType, Guid>>();
        _locations = GetRequiredService<IRepository<Location, Guid>>();
    }

    [Fact]
    public async Task Create_WithExistingTypeAndLocation_LinksBoth()
    {
        var doctor = await CreateLinkedDoctorAsync();

        var stored = await LoadAsync(doctor.Id);
        stored.AppointmentTypes.Select(t => t.AppointmentTypeId).ShouldBe(new[] { _typeId });
        stored.Locations.Select(l => l.LocationId).ShouldBe(new[] { _locationId });
    }

    [Fact]
    public async Task Create_WithOnlyUnknownIds_SavesTheDoctorWithNoLinks()
    {
        var created = await InOwnPracticeAsync(() => _manager.CreateAsync(
            new List<Guid> { Guid.NewGuid() }, new List<Guid> { Guid.NewGuid() },
            "TEST-First", "TEST-Unlinked", $"test.{Guid.NewGuid():N}@test.local", Gender.Male));

        var stored = await LoadAsync(created.Id);
        stored.LastName.ShouldBe("TEST-Unlinked");
        stored.AppointmentTypes.ShouldBeEmpty();
        stored.Locations.ShouldBeEmpty();
    }

    [Fact]
    public async Task Update_WithOnlyUnknownIds_KeepsTheExistingLinks()
    {
        // Decoy: the doctor already has one type and one location. An update naming only ids that
        // do not exist must leave them alone, not treat "nothing matched" as "remove everything".
        var doctor = await CreateLinkedDoctorAsync();

        await InOwnPracticeAsync(() => _manager.UpdateAsync(
            doctor.Id, new List<Guid> { Guid.NewGuid() }, new List<Guid> { Guid.NewGuid() },
            "TEST-First", "TEST-Renamed", doctor.Email, Gender.Female));

        var stored = await LoadAsync(doctor.Id);
        stored.LastName.ShouldBe("TEST-Renamed");
        stored.AppointmentTypes.Select(t => t.AppointmentTypeId).ShouldBe(new[] { _typeId });
        stored.Locations.Select(l => l.LocationId).ShouldBe(new[] { _locationId });
    }

    [Fact]
    public async Task Update_WithEmptyLists_RemovesEveryLink()
    {
        var doctor = await CreateLinkedDoctorAsync();

        await InOwnPracticeAsync(() => _manager.UpdateAsync(
            doctor.Id, new List<Guid>(), new List<Guid>(), "TEST-First", "TEST-Cleared", doctor.Email, Gender.Other));

        var stored = await LoadAsync(doctor.Id);
        stored.AppointmentTypes.ShouldBeEmpty();
        stored.Locations.ShouldBeEmpty();
    }

    private Task<Doctor> CreateLinkedDoctorAsync() =>
        InOwnPracticeAsync(() => _manager.CreateAsync(
            new List<Guid> { _typeId },
            new List<Guid> { _locationId },
            "TEST-First", "TEST-Linked", $"test.{Guid.NewGuid():N}@test.local", Gender.Female));

    private Task<Doctor> LoadAsync(Guid id) =>
        InOwnPracticeAsync(async () =>
        {
            var query = await _doctors.WithDetailsAsync(d => d.AppointmentTypes, d => d.Locations);
            return query.Single(d => d.Id == id);
        });

    private async Task<T> InOwnPracticeAsync<T>(Func<Task<T>> action)
    {
        var practiceId = _practiceId ??= await CreatePracticeAsync();
        return await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(practiceId))
            {
                return await action();
            }
        });
    }

    private async Task<Guid> CreatePracticeAsync()
    {
        var practiceId = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var practice = await _tenantManager.CreateAsync("TEST-practice-" + Guid.NewGuid().ToString("N")[..10]);
                await _tenants.InsertAsync(practice, autoSave: true);
                return practice.Id;
            }
        });
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(practiceId))
            {
                await _appointmentTypes.InsertAsync(new AppointmentType(_typeId, "TEST-Type " + _typeId.ToString("N")[..6]), autoSave: true);
                await _locations.InsertAsync(new Location(_locationId, null, "TEST-Location " + _locationId.ToString("N")[..6], 0m, true), autoSave: true);
                return true;
            }
        });
        return practiceId;
    }
}
