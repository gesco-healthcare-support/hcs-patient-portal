using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Item 3 (2026-08-17) -- the three branches of the attorney upsert.
///
/// <para>An attorney is a SHARED master record that many appointments point at. The upsert picks
/// one of three behaviours, and which one it picks is now load-bearing for the legal-trail
/// guarantee, so it is pinned here rather than exercised incidentally:</para>
///
/// <list type="number">
///   <item>id supplied -> writes the posted values UNCONDITIONALLY, so a blank field blanks it on
///   the shared record.</item>
///   <item>no id, email matches -> MERGES (<c>input.X ?? existing.X</c>), preserving anything the
///   booker left empty.</item>
///   <item>neither -> creates a new attorney.</item>
/// </list>
///
/// <para>Item 3 stopped the prefill carrying the id precisely so a copied booking takes branch 2
/// instead of branch 1. If branch 2 ever stopped merging, that change would silently become
/// destructive again -- which is what these tests exist to catch.</para>
///
/// <para>Covers BOTH applicant and defense: the defense upsert is a separate copy of the same
/// logic rather than a shared helper, so one could be fixed and the other missed.</para>
///
/// <para>GOTCHA: <c>FirmName</c> is mandatory (<c>EnsureAttorneyFirmNamePresent</c>), so merge
/// semantics are asserted with the genuinely optional fields instead. All fixture data is
/// synthetic.</para>
/// </summary>
public abstract class AttorneyUpsertBranchTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAppointmentsAppService _appointmentsAppService;
    private readonly IApplicantAttorneyRepository _applicantAttorneyRepository;
    private readonly IDefenseAttorneyRepository _defenseAttorneyRepository;
    private readonly ICurrentTenant _currentTenant;

    protected AttorneyUpsertBranchTests()
    {
        _appointmentsAppService = GetRequiredService<IAppointmentsAppService>();
        _applicantAttorneyRepository = GetRequiredService<IApplicantAttorneyRepository>();
        _defenseAttorneyRepository = GetRequiredService<IDefenseAttorneyRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    // ---------------------------------------------------------------- applicant

    [Fact]
    public async Task ApplicantUpsert_WithNoIdAndAnUnmatchedEmail_CreatesTheAttorney()
    {
        const string email = "TEST-aa-create@test.local";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await _appointmentsAppService.UpsertApplicantAttorneyForAppointmentAsync(
                AppointmentsTestData.Appointment1Id,
                new ApplicantAttorneyDetailsDto
                {
                    Email = email,
                    FirstName = "TESTAA",
                    LastName = "TESTCREATED",
                    FirmName = "TEST Firm Alpha",
                    PhoneNumber = "555-0300",
                    FaxNumber = "555-0301",
                    City = "Alphaville",
                });

            var created = await _applicantAttorneyRepository.FindByNormalizedEmailAsync(email);

            created.ShouldNotBeNull();
            created!.FirmName.ShouldBe("TEST Firm Alpha");
            created.PhoneNumber.ShouldBe("555-0300");
            created.City.ShouldBe("Alphaville");
        }
    }

    [Fact]
    public async Task ApplicantUpsert_WithNoIdAndAMatchingEmail_PreservesFieldsLeftBlank()
    {
        // THE assertion item 3 depends on. The second call leaves fax and city null; both must
        // survive, because this is the branch a prefilled booking now takes.
        const string email = "TEST-aa-merge@test.local";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await _appointmentsAppService.UpsertApplicantAttorneyForAppointmentAsync(
                AppointmentsTestData.Appointment1Id,
                new ApplicantAttorneyDetailsDto
                {
                    Email = email,
                    FirstName = "TESTAA",
                    LastName = "TESTMERGE",
                    FirmName = "TEST Firm Beta",
                    PhoneNumber = "555-0400",
                    FaxNumber = "555-0401",
                    City = "Betaville",
                });

            await _appointmentsAppService.UpsertApplicantAttorneyForAppointmentAsync(
                AppointmentsTestData.Appointment1Id,
                new ApplicantAttorneyDetailsDto
                {
                    Email = email,
                    FirstName = "TESTAA",
                    LastName = "TESTMERGE",
                    FirmName = "TEST Firm Beta Renamed",
                    PhoneNumber = "555-0499",
                    FaxNumber = null,
                    City = null,
                });

            var merged = await _applicantAttorneyRepository.FindByNormalizedEmailAsync(email);

            merged.ShouldNotBeNull();
            // Supplied values win.
            merged!.FirmName.ShouldBe("TEST Firm Beta Renamed");
            merged.PhoneNumber.ShouldBe("555-0499");
            // Omitted values are PRESERVED, not blanked.
            merged.FaxNumber.ShouldBe("555-0401");
            merged.City.ShouldBe("Betaville");
        }
    }

    [Fact]
    public async Task ApplicantUpsert_WithAnId_WritesTheSuppliedValuesAsGiven()
    {
        // The branch the prefill no longer takes. Documented here so its destructive
        // characteristic is explicit rather than folklore: a blank overwrites.
        const string email = "TEST-aa-byid@test.local";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await _appointmentsAppService.UpsertApplicantAttorneyForAppointmentAsync(
                AppointmentsTestData.Appointment1Id,
                new ApplicantAttorneyDetailsDto
                {
                    Email = email,
                    FirstName = "TESTAA",
                    LastName = "TESTBYID",
                    FirmName = "TEST Firm Gamma",
                    FaxNumber = "555-0501",
                    City = "Gammaville",
                });

            var seeded = await _applicantAttorneyRepository.FindByNormalizedEmailAsync(email);
            seeded.ShouldNotBeNull();

            await _appointmentsAppService.UpsertApplicantAttorneyForAppointmentAsync(
                AppointmentsTestData.Appointment1Id,
                new ApplicantAttorneyDetailsDto
                {
                    ApplicantAttorneyId = seeded!.Id,
                    ConcurrencyStamp = seeded.ConcurrencyStamp,
                    Email = email,
                    FirstName = "TESTAA",
                    LastName = "TESTBYID",
                    FirmName = "TEST Firm Gamma",
                    FaxNumber = null,
                    City = null,
                });

            var afterById = await _applicantAttorneyRepository.FindByNormalizedEmailAsync(email);

            afterById.ShouldNotBeNull();
            afterById!.Id.ShouldBe(seeded.Id, "the same master must be reused, not duplicated");
            afterById.FaxNumber.ShouldBeNull();
            afterById.City.ShouldBeNull();
        }
    }

    // ------------------------------------------------------------------ defense

    [Fact]
    public async Task DefenseUpsert_WithNoIdAndAnUnmatchedEmail_CreatesTheAttorney()
    {
        const string email = "TEST-da-create@test.local";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await _appointmentsAppService.UpsertDefenseAttorneyForAppointmentAsync(
                AppointmentsTestData.Appointment1Id,
                new DefenseAttorneyDetailsDto
                {
                    Email = email,
                    FirstName = "TESTDA",
                    LastName = "TESTCREATED",
                    FirmName = "TEST Defense Alpha",
                    PhoneNumber = "555-0600",
                    FaxNumber = "555-0601",
                    City = "Deltaville",
                });

            var created = await _defenseAttorneyRepository.FindByNormalizedEmailAsync(email);

            created.ShouldNotBeNull();
            created!.FirmName.ShouldBe("TEST Defense Alpha");
            created.PhoneNumber.ShouldBe("555-0600");
            created.City.ShouldBe("Deltaville");
        }
    }

    [Fact]
    public async Task DefenseUpsert_WithNoIdAndAMatchingEmail_PreservesFieldsLeftBlank()
    {
        const string email = "TEST-da-merge@test.local";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await _appointmentsAppService.UpsertDefenseAttorneyForAppointmentAsync(
                AppointmentsTestData.Appointment1Id,
                new DefenseAttorneyDetailsDto
                {
                    Email = email,
                    FirstName = "TESTDA",
                    LastName = "TESTMERGE",
                    FirmName = "TEST Defense Beta",
                    PhoneNumber = "555-0700",
                    FaxNumber = "555-0701",
                    City = "Epsilonville",
                });

            await _appointmentsAppService.UpsertDefenseAttorneyForAppointmentAsync(
                AppointmentsTestData.Appointment1Id,
                new DefenseAttorneyDetailsDto
                {
                    Email = email,
                    FirstName = "TESTDA",
                    LastName = "TESTMERGE",
                    FirmName = "TEST Defense Beta Renamed",
                    PhoneNumber = "555-0799",
                    FaxNumber = null,
                    City = null,
                });

            var merged = await _defenseAttorneyRepository.FindByNormalizedEmailAsync(email);

            merged.ShouldNotBeNull();
            merged!.FirmName.ShouldBe("TEST Defense Beta Renamed");
            merged.PhoneNumber.ShouldBe("555-0799");
            merged.FaxNumber.ShouldBe("555-0701");
            merged.City.ShouldBe("Epsilonville");
        }
    }

    [Fact]
    public async Task DefenseUpsert_WithAnId_WritesTheSuppliedValuesAsGiven()
    {
        const string email = "TEST-da-byid@test.local";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await _appointmentsAppService.UpsertDefenseAttorneyForAppointmentAsync(
                AppointmentsTestData.Appointment1Id,
                new DefenseAttorneyDetailsDto
                {
                    Email = email,
                    FirstName = "TESTDA",
                    LastName = "TESTBYID",
                    FirmName = "TEST Defense Gamma",
                    FaxNumber = "555-0801",
                    City = "Zetaville",
                });

            var seeded = await _defenseAttorneyRepository.FindByNormalizedEmailAsync(email);
            seeded.ShouldNotBeNull();

            await _appointmentsAppService.UpsertDefenseAttorneyForAppointmentAsync(
                AppointmentsTestData.Appointment1Id,
                new DefenseAttorneyDetailsDto
                {
                    DefenseAttorneyId = seeded!.Id,
                    ConcurrencyStamp = seeded.ConcurrencyStamp,
                    Email = email,
                    FirstName = "TESTDA",
                    LastName = "TESTBYID",
                    FirmName = "TEST Defense Gamma",
                    FaxNumber = null,
                    City = null,
                });

            var afterById = await _defenseAttorneyRepository.FindByNormalizedEmailAsync(email);

            afterById.ShouldNotBeNull();
            afterById!.Id.ShouldBe(seeded.Id, "the same master must be reused, not duplicated");
            afterById.FaxNumber.ShouldBeNull();
            afterById.City.ShouldBeNull();
        }
    }
}
