using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.ClaimExaminers;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Domains;

/// <summary>
/// The plain (no navigation properties) <c>GetListAsync</c> filters of five repositories, which
/// the app services never call and so no other test reaches: claim examiners, defense attorneys,
/// appointment-defense-attorney links, injury details and doctor availabilities.
///
/// <para>Each fact inserts a target and a decoy that differ only in the filtered field, both
/// carrying a per-fact token, so the assertion is exact (<c>ShouldBe</c> the target alone) without
/// depending on what else the seed put in the table.</para>
/// </summary>
public class EfCoreRepositoryPlainListFilterTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly IClaimExaminerRepository _claimExaminers;
    private readonly IDefenseAttorneyRepository _defenseAttorneys;
    private readonly IAppointmentDefenseAttorneyRepository _defenseLinks;
    private readonly IAppointmentInjuryDetailRepository _injuries;
    private readonly IDoctorAvailabilityRepository _slots;
    private readonly ICurrentTenant _currentTenant;
    private readonly string _token = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    public EfCoreRepositoryPlainListFilterTests()
    {
        _claimExaminers = GetRequiredService<IClaimExaminerRepository>();
        _defenseAttorneys = GetRequiredService<IDefenseAttorneyRepository>();
        _defenseLinks = GetRequiredService<IAppointmentDefenseAttorneyRepository>();
        _injuries = GetRequiredService<IAppointmentInjuryDetailRepository>();
        _slots = GetRequiredService<IDoctorAvailabilityRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task ClaimExaminers_FilterByEmailPhoneCityAndFreeText()
    {
        var target = Guid.NewGuid();
        await InTenantAAsync(async () =>
        {
            await _claimExaminers.InsertAsync(
                new ClaimExaminer(target, null, null, $"555{_token[..4]}01", $"ce.{_token}.a@test.local")
                { LastName = $"TEST-CeTarget{_token}", City = $"TEST-CityA{_token}" }, autoSave: true);
            await _claimExaminers.InsertAsync(
                new ClaimExaminer(Guid.NewGuid(), null, null, $"555{_token[..4]}02", $"ce.{_token}.b@test.local")
                { LastName = $"TEST-CeDecoy{_token}", City = $"TEST-CityB{_token}" }, autoSave: true);
            return true;
        });

        await InTenantAAsync(async () =>
        {
            (await _claimExaminers.GetListAsync(email: $"ce.{_token}.a@")).Select(c => c.Id).ShouldBe(new[] { target });
            (await _claimExaminers.GetListAsync(phoneNumber: $"555{_token[..4]}01")).Select(c => c.Id).ShouldBe(new[] { target });
            (await _claimExaminers.GetListAsync(city: $"TEST-CityA{_token}")).Select(c => c.Id).ShouldBe(new[] { target });
            (await _claimExaminers.GetListAsync(filterText: $"TEST-CeTarget{_token}")).Select(c => c.Id).ShouldBe(new[] { target });
            return true;
        });
    }

    [Fact]
    public async Task DefenseAttorneys_FilterByFirmPhoneCityAndFreeText()
    {
        var target = Guid.NewGuid();
        await InTenantAAsync(async () =>
        {
            await _defenseAttorneys.InsertAsync(
                new DefenseAttorney(target, null, null, $"TEST-FirmA{_token}", null, $"555{_token[..4]}11", $"da.{_token}.a@test.local")
                { City = $"TEST-CityA{_token}" }, autoSave: true);
            await _defenseAttorneys.InsertAsync(
                new DefenseAttorney(Guid.NewGuid(), null, null, $"TEST-FirmB{_token}", null, $"555{_token[..4]}12", $"da.{_token}.b@test.local")
                { City = $"TEST-CityB{_token}" }, autoSave: true);
            return true;
        });

        await InTenantAAsync(async () =>
        {
            (await _defenseAttorneys.GetListAsync(firmName: $"TEST-FirmA{_token}")).Select(d => d.Id).ShouldBe(new[] { target });
            (await _defenseAttorneys.GetListAsync(phoneNumber: $"555{_token[..4]}11")).Select(d => d.Id).ShouldBe(new[] { target });
            (await _defenseAttorneys.GetListAsync(city: $"TEST-CityA{_token}")).Select(d => d.Id).ShouldBe(new[] { target });
            (await _defenseAttorneys.GetListAsync(filterText: $"TEST-FirmA{_token}")).Select(d => d.Id).ShouldBe(new[] { target });
            return true;
        });
    }

    [Fact]
    public async Task AppointmentDefenseAttorneyLinks_FreeTextDoesNotNarrowTheList()
    {
        // Characterizes current behaviour: the plain list's filter is `WhereIf(filterText, e => true)`,
        // so free text never narrows it. Pinned so a change to that is a visible decision (logged to
        // the backlog as a probable leftover of generated code).
        var attorneyId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        await InTenantAAsync(async () =>
        {
            await _defenseAttorneys.InsertAsync(new DefenseAttorney(attorneyId, null, null, $"TEST-Firm{_token}"), autoSave: true);
            await _defenseLinks.InsertAsync(
                new AppointmentDefenseAttorney(linkId, AppointmentsTestData.Appointment1Id, attorneyId, null), autoSave: true);
            return true;
        });

        await InTenantAAsync(async () =>
        {
            var all = await _defenseLinks.GetListAsync();
            var withText = await _defenseLinks.GetListAsync(filterText: "TEST-matches-nothing-" + _token);
            all.Select(l => l.Id).ShouldContain(linkId);
            withText.Select(l => l.Id).OrderBy(id => id).ShouldBe(all.Select(l => l.Id).OrderBy(id => id));
            return true;
        });
    }

    [Fact]
    public async Task InjuryDetails_FilterByAppointmentClaimNumberAndFreeText()
    {
        var target = Guid.NewGuid();
        await InTenantAAsync(async () =>
        {
            await _injuries.InsertAsync(new AppointmentInjuryDetail(
                target, AppointmentsTestData.Appointment1Id, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                $"TEST-CLA-{_token}", false, "TEST-knee", wcabAdj: $"TEST-ADJ-{_token}"), autoSave: true);
            await _injuries.InsertAsync(new AppointmentInjuryDetail(
                Guid.NewGuid(), AppointmentsTestData.Appointment1Id, new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                $"TEST-CLB-{_token}", false, "TEST-knee", wcabAdj: $"TEST-ADJ-{_token}"), autoSave: true);
            return true;
        });

        await InTenantAAsync(async () =>
        {
            (await _injuries.GetListAsync(appointmentId: AppointmentsTestData.Appointment1Id, claimNumber: $"TEST-CLA-{_token}"))
                .Select(i => i.Id).ShouldBe(new[] { target });
            (await _injuries.GetListAsync(filterText: $"TEST-CLA-{_token}")).Select(i => i.Id).ShouldBe(new[] { target });
            return true;
        });
    }

    [Fact]
    public async Task DoctorAvailabilities_FilterByDateTimeAndBookingStatus()
    {
        var day = new DateTime(2036, 3, 3, 0, 0, 0, DateTimeKind.Utc);
        var target = Guid.NewGuid();
        await InTenantAAsync(async () =>
        {
            await _slots.InsertAsync(new DoctorAvailability(target, LocationsTestData.Location1Id, day,
                new TimeOnly(9, 0), new TimeOnly(10, 0), BookingStatus.Available), autoSave: true);
            // Decoy: inside every date and time range asked for, but already booked. (Its hours differ
            // by 15 minutes only because a slot's identity is unique per tenant, location, day and hours.)
            await _slots.InsertAsync(new DoctorAvailability(Guid.NewGuid(), LocationsTestData.Location1Id, day,
                new TimeOnly(9, 15), new TimeOnly(10, 15), BookingStatus.Booked), autoSave: true);
            return true;
        });

        var found = await InTenantAAsync(() => _slots.GetListAsync(
            availableDateMin: day, availableDateMax: day.AddDays(1),
            fromTimeMin: new TimeOnly(8, 0), fromTimeMax: new TimeOnly(9, 30),
            toTimeMin: new TimeOnly(9, 30), toTimeMax: new TimeOnly(11, 0),
            bookingStatusId: BookingStatus.Available));

        found.Select(s => s.Id).ShouldBe(new[] { target });
    }

    private Task<T> InTenantAAsync<T>(Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await action();
            }
        });
}
