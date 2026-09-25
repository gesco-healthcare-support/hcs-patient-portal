using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Saas;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Seeding;

/// <summary>
/// <see cref="DoctorProfileDataSeedContributor"/>: a new practice gets exactly one doctor, named
/// from the New Practice form when given, else from the known-office table, else from the practice
/// name, and linked to the three seeded appointment types and two demo clinics. With no admin
/// email the practice gets no doctor. A second run adds nothing.
///
/// <para>The "does this practice already have a doctor?" check is per office, so every test carries
/// an OFFICE decoy for free: office A's seeded doctor. A count that lost its office filter would
/// see that doctor and seed nothing for the new practice.</para>
/// </summary>
public class DoctorProfileSeedTests : SeedContributorTestBase
{
    private const string AdminEmail = "test.admin@test.local";

    private readonly DoctorProfileDataSeedContributor _seeder;
    private readonly IRepository<Doctor, Guid> _doctors;
    private readonly IRepository<AppointmentType, Guid> _appointmentTypes;
    private readonly IRepository<Location, Guid> _locations;

    public DoctorProfileSeedTests()
    {
        _seeder = GetRequiredService<DoctorProfileDataSeedContributor>();
        _doctors = GetRequiredService<IRepository<Doctor, Guid>>();
        _appointmentTypes = GetRequiredService<IRepository<AppointmentType, Guid>>();
        _locations = GetRequiredService<IRepository<Location, Guid>>();
    }

    [Fact]
    public async Task NamesFromTheForm_AreUsedTrimmed_LinkedToTheSeededCatalog_AndASecondRunAddsNoDoctor()
    {
        var practiceId = await CreatePracticeWithSeedCatalogAsync();
        var context = new DataSeedContext(practiceId)
            .WithProperty(IdentityDataSeedContributor.AdminEmailPropertyName, AdminEmail)
            .WithProperty(OfficeSeedProperties.DoctorFirstName, "  TEST-Ada ")
            .WithProperty(OfficeSeedProperties.DoctorLastName, " TEST-Lovelace  ")
            .WithProperty(OfficeSeedProperties.DoctorEmail, " test.doctor@test.local ");

        await SeedAsync(_seeder, context);
        await SeedAsync(_seeder, context);

        var doctor = (await DoctorsOf(practiceId)).ShouldHaveSingleItem();
        doctor.FirstName.ShouldBe("TEST-Ada");
        doctor.LastName.ShouldBe("TEST-Lovelace");
        doctor.Email.ShouldBe("test.doctor@test.local");
        doctor.AppointmentTypes.Select(t => t.AppointmentTypeId).OrderBy(g => g).ShouldBe(new[]
        {
            CaseEvaluationSeedIds.AppointmentTypes.Ame, CaseEvaluationSeedIds.AppointmentTypes.Ime, CaseEvaluationSeedIds.AppointmentTypes.PanelQme,
        }.OrderBy(g => g));
        doctor.Locations.Select(l => l.LocationId).OrderBy(g => g).ShouldBe(new[]
        {
            CaseEvaluationSeedIds.Locations.DemoClinicNorth, CaseEvaluationSeedIds.Locations.DemoClinicSouth,
        }.OrderBy(g => g));
    }

    [Fact]
    public async Task AFormEmailLeftBlank_FallsBackToTheAdminEmail()
    {
        var practiceId = await CreatePracticeWithSeedCatalogAsync();

        await SeedAsync(_seeder, new DataSeedContext(practiceId)
            .WithProperty(IdentityDataSeedContributor.AdminEmailPropertyName, AdminEmail)
            .WithProperty(OfficeSeedProperties.DoctorLastName, "TEST-Hopper"));

        var doctor = (await DoctorsOf(practiceId)).ShouldHaveSingleItem();
        doctor.FirstName.ShouldBe(string.Empty);
        doctor.LastName.ShouldBe("TEST-Hopper");
        doctor.Email.ShouldBe(AdminEmail);
    }

    [Fact]
    public async Task AKnownOffice_WithNoFormNames_GetsThatOfficesDoctor()
    {
        // The known-office table is production source data; the test reads it rather than restating it.
        var office = OfficeSeedData.Offices[0];
        var practiceId = await CreatePracticeWithSeedCatalogAsync(office.TenantName);

        await SeedAsync(_seeder, new DataSeedContext(practiceId).WithProperty(IdentityDataSeedContributor.AdminEmailPropertyName, AdminEmail));

        var doctor = (await DoctorsOf(practiceId)).ShouldHaveSingleItem();
        doctor.FirstName.ShouldBe(office.DoctorFirstName);
        doctor.LastName.ShouldBe(office.DoctorLastName);
        doctor.Email.ShouldBe(office.DoctorEmail);
    }

    [Fact]
    public async Task AnUnknownOffice_WithNoFormNames_IsNamedAfterThePractice_AtTheAdminEmail()
    {
        var name = "TEST-practice-" + Guid.NewGuid().ToString("N")[..10];
        var practiceId = await CreatePracticeWithSeedCatalogAsync(name);

        await SeedAsync(_seeder, new DataSeedContext(practiceId).WithProperty(IdentityDataSeedContributor.AdminEmailPropertyName, AdminEmail));

        var doctor = (await DoctorsOf(practiceId)).ShouldHaveSingleItem();
        doctor.FirstName.ShouldBe(string.Empty);
        doctor.LastName.ShouldBe(name);
        doctor.Email.ShouldBe(AdminEmail);
    }

    [Fact]
    public async Task WithNoAdminEmail_NoDoctorIsSeeded()
    {
        var practiceId = await CreatePracticeWithSeedCatalogAsync();

        await SeedAsync(_seeder, new DataSeedContext(practiceId).WithProperty(OfficeSeedProperties.DoctorLastName, "TEST-Ignored"));

        (await DoctorsOf(practiceId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task HostPass_WritesNothing()
    {
        var before = await InScopeAsync<long>(null, () => _doctors.GetCountAsync());

        await SeedAsync(_seeder, new DataSeedContext(null).WithProperty(IdentityDataSeedContributor.AdminEmailPropertyName, AdminEmail));

        (await InScopeAsync<long>(null, () => _doctors.GetCountAsync())).ShouldBe(before);
    }

    /// <summary>
    /// A practice holding the three appointment types and two clinics the seeded doctor links to.
    /// The links are foreign keys, and in production those rows are seeded into the office first.
    /// </summary>
    private async Task<Guid> CreatePracticeWithSeedCatalogAsync(string? name = null)
    {
        var practiceId = await CreatePracticeAsync(name);
        await InScopeAsync(practiceId, async () =>
        {
            await _appointmentTypes.InsertAsync(new AppointmentType(CaseEvaluationSeedIds.AppointmentTypes.Ame, "TEST-AME"), autoSave: true);
            await _appointmentTypes.InsertAsync(new AppointmentType(CaseEvaluationSeedIds.AppointmentTypes.Ime, "TEST-IME"), autoSave: true);
            await _appointmentTypes.InsertAsync(new AppointmentType(CaseEvaluationSeedIds.AppointmentTypes.PanelQme, "TEST-PQME"), autoSave: true);
            await _locations.InsertAsync(new Location(CaseEvaluationSeedIds.Locations.DemoClinicNorth, null, "TEST-Clinic North", 0m, true), autoSave: true);
            await _locations.InsertAsync(new Location(CaseEvaluationSeedIds.Locations.DemoClinicSouth, null, "TEST-Clinic South", 0m, true), autoSave: true);
            return true;
        });
        return practiceId;
    }

    private Task<System.Collections.Generic.List<Doctor>> DoctorsOf(Guid practiceId) =>
        InScopeAsync(practiceId, async () =>
        {
            var query = await _doctors.WithDetailsAsync(d => d.AppointmentTypes, d => d.Locations);
            return await GetRequiredService<Volo.Abp.Linq.IAsyncQueryableExecuter>().ToListAsync(query);
        });
}
