using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

/// <summary>
/// <see cref="AppointmentInjuryDetailManager"/>'s refusals, against the real repositories: a
/// duplicate claim on create and on update, an injury dated before the patient was born, and
/// the three cumulative-injury date-range rules.
///
/// <para>The duplicate cases use the seeded injury on Appointment1 (claim
/// <c>TEST-CLAIM-0001</c>, 2026-03-14) as the decoy that makes a duplicate possible; every
/// refusal also asserts that nothing new was written for the claim number it used.</para>
/// </summary>
public class EfCoreAppointmentInjuryDetailManagerRulesTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string DuplicateMessage = "A claim with the same Claim Number and Date Of Injury already exists for this appointment.";

    private readonly AppointmentInjuryDetailManager _manager;
    private readonly IRepository<AppointmentInjuryDetail, Guid> _injuries;
    private readonly ICurrentTenant _currentTenant;

    public EfCoreAppointmentInjuryDetailManagerRulesTests()
    {
        _manager = GetRequiredService<AppointmentInjuryDetailManager>();
        _injuries = GetRequiredService<IRepository<AppointmentInjuryDetail, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Create_SameClaimAndDateOnTheSameAppointment_IsRefused_AndAddsNoRow()
    {
        var ex = await Should.ThrowAsync<UserFriendlyException>(() => InTenantAAsync(() => _manager.CreateAsync(
            AppointmentsTestData.Appointment1Id, AppointmentInjuryDetailsTestData.Detail1DateOfInjury,
            AppointmentInjuryDetailsTestData.Detail1ClaimNumber, isCumulativeInjury: false, bodyPartsSummary: "TEST-dup", wcabAdj: "TEST-ADJ-DUP")));

        ex.Message.ShouldBe(DuplicateMessage);
        (await CountClaimAsync(AppointmentInjuryDetailsTestData.Detail1ClaimNumber)).ShouldBe(1);
    }

    [Fact]
    public async Task Update_OntoAnotherInjurysClaimAndDate_IsRefused_AndLeavesTheRowUnchanged()
    {
        var ownClaim = "TEST-CLM-" + NewToken();
        var own = await InTenantAAsync(() => _manager.CreateAsync(
            AppointmentsTestData.Appointment1Id, new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            ownClaim, isCumulativeInjury: false, bodyPartsSummary: "TEST-wrist", wcabAdj: "TEST-ADJ-OWN"));

        var ex = await Should.ThrowAsync<UserFriendlyException>(() => InTenantAAsync(() => _manager.UpdateAsync(
            own.Id, AppointmentsTestData.Appointment1Id, AppointmentInjuryDetailsTestData.Detail1DateOfInjury,
            AppointmentInjuryDetailsTestData.Detail1ClaimNumber, isCumulativeInjury: false, bodyPartsSummary: "TEST-wrist", wcabAdj: "TEST-ADJ-OWN")));

        ex.Message.ShouldBe(DuplicateMessage);
        (await InTenantAAsync(() => _injuries.GetAsync(own.Id))).ClaimNumber.ShouldBe(ownClaim);
    }

    [Theory]
    [InlineData("1980-06-01", false, null, "Injury date cannot be earlier than the patient's date of birth.")]
    [InlineData("2025-05-01", true, "2025-04-01", "Injury 'From' date must be earlier than the 'To' date.")]
    [InlineData("2025-05-01", true, "2099-01-01", "Injury 'To' date cannot be in the future.")]
    [InlineData("2025-05-01", true, "2025-05-01", "Injury 'From' and 'To' dates must be different.")]
    public async Task Create_WithAnInvalidDateRange_IsRefused_AndAddsNoRow(
        string from, bool cumulative, string? to, string expectedMessage)
    {
        var claim = "TEST-CLM-" + NewToken();

        var ex = await Should.ThrowAsync<UserFriendlyException>(() => InTenantAAsync(() => _manager.CreateAsync(
            AppointmentsTestData.Appointment1Id, ParseUtc(from), claim, cumulative, "TEST-shoulder",
            toDateOfInjury: to == null ? null : ParseUtc(to), wcabAdj: "TEST-ADJ-RANGE")));

        ex.Message.ShouldBe(expectedMessage);
        (await CountClaimAsync(claim)).ShouldBe(0);
    }

    [Fact]
    public async Task Create_CumulativeWithAValidRange_IsSaved()
    {
        // Positive control for the theory above: the same shape with a valid range passes, so the
        // refusals are about the dates and not about cumulative injuries as such.
        var claim = "TEST-CLM-" + NewToken();

        var created = await InTenantAAsync(() => _manager.CreateAsync(
            AppointmentsTestData.Appointment1Id, ParseUtc("2025-02-01"), claim, true, "TEST-shoulder",
            toDateOfInjury: ParseUtc("2025-03-01"), wcabAdj: "TEST-ADJ-OK"));

        created.ClaimNumber.ShouldBe(claim);
        (await CountClaimAsync(claim)).ShouldBe(1);
    }

    private Task<int> CountClaimAsync(string claim) =>
        InTenantAAsync(() => _injuries.CountAsync(i => i.AppointmentId == AppointmentsTestData.Appointment1Id && i.ClaimNumber == claim));

    private Task<T> InTenantAAsync<T>(Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await action();
            }
        });

    private static DateTime ParseUtc(string date) =>
        DateTime.SpecifyKind(DateTime.Parse(date, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);

    private static string NewToken() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
