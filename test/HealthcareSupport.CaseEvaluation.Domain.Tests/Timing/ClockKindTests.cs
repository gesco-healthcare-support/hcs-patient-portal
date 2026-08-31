using System;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Timing;

/// <summary>
/// Guards the clock-kind pin added to <c>CaseEvaluationDomainModule</c> on 2026-08-27.
///
/// <para>ABP's default <c>AbpClockOptions.Kind</c> is <see cref="DateTimeKind.Unspecified"/>, which
/// makes <c>IClock.Now</c> return <c>DateTime.Now</c> -- server LOCAL time, stamped with no Kind.
/// That was correct here only by accident of deployment: the API container's clock happens to be
/// UTC. Set a <c>TZ</c> on that container and every audit column, every consent timestamp and every
/// <c>submittedAtUtc</c> on the Case Tracker wire silently shifts, with nothing in the data to say
/// it had.</para>
///
/// <para>These are not tests of ABP; they are tests that OUR module still sets the option. The pin
/// is one line in one file and reads as boilerplate, so it is exactly the kind of line a future
/// module edit drops without noticing. The consequence would not surface as a failure -- it would
/// surface as timestamps that are quietly wrong on medical-legal records.</para>
///
/// <para>Storage stays UTC by decision (2026-08-27); rendering Pacific is the job of the display
/// formatters, not of the stored value. So the invariant asserted here is deliberately about the
/// KIND being explicit, not about the zone.</para>
///
/// <para>Abstract + concrete split per the house pattern: the domain test module cannot boot on its
/// own (AbpFeatureManagementDomainModule needs a store at initialization), so the concrete
/// EfCoreClockKindTests subclass under EntityFrameworkCore.Tests supplies the SQLite wiring.</para>
/// </summary>
public abstract class ClockKindTests<TStartupModule> : CaseEvaluationDomainTestBase<TStartupModule>
    where TStartupModule : Volo.Abp.Modularity.IAbpModule
{
    [Fact]
    public void ClockOptions_PinKindToUtc()
    {
        var options = GetRequiredService<IOptions<AbpClockOptions>>().Value;

        options.Kind.ShouldBe(
            DateTimeKind.Utc,
            "CaseEvaluationDomainModule must keep Configure<AbpClockOptions>(o => o.Kind = " +
            "DateTimeKind.Utc). Without it ABP falls back to Unspecified, IClock.Now returns " +
            "server-local time with no Kind, and consumers such as IntegrationTimestamp have to " +
            "ASSUME the value is already UTC -- an assumption that breaks the moment a TZ is set " +
            "on the API container.");
    }

    [Fact]
    public void Clock_StampsUtcKind()
    {
        var clock = GetRequiredService<IClock>();

        clock.Now.Kind.ShouldBe(
            DateTimeKind.Utc,
            "IClock.Now must carry DateTimeKind.Utc. Everything ABP stamps for us -- CreationTime, " +
            "LastModificationTime, DeletionTime -- comes from this clock, and domain guards such " +
            "as AppointmentChangeRequest.MarkDecided reject a non-UTC decision time outright.");
    }

    [Fact]
    public void Clock_SupportsMultipleTimezone()
    {
        var clock = GetRequiredService<IClock>();

        clock.SupportsMultipleTimezone.ShouldBeTrue(
            "ABP reports SupportsMultipleTimezone only when Kind is Utc. It is the flag ABP's own " +
            "normalization consults, so a false here means a stored instant can be reinterpreted " +
            "in the reader's zone rather than converted from a known one.");
    }
}
