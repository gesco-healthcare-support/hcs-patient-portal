using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.WcabOffices;
using NSubstitute;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for the injuries array. All fixture data is synthetic; claim numbers deliberately avoid
/// long digit runs so the PHI scanner does not read them as record numbers.
///
/// <para>The cases that matter: a two-injury appointment must publish BOTH claims (the receiver's staff
/// choose between them, so dropping one recreates the misfiling this change exists to fix), and an
/// appointment with no injury rows must not throw.</para>
/// </summary>
public class InjuryResolverTests
{
    private static readonly Guid AppointmentId = new("ada5e3c5-0034-ebde-253c-3a2293631dee");

    private static AppointmentInjuryDetailWithNavigationProperties Row(
        string claimNumber,
        string wcabAdj,
        DateTime dateOfInjury,
        DateTime? toDateOfInjury = null,
        bool cumulative = false,
        WcabOffice? office = null) =>
        new()
        {
            AppointmentInjuryDetail = new AppointmentInjuryDetail(
                Guid.NewGuid(),
                AppointmentId,
                dateOfInjury,
                claimNumber,
                cumulative,
                bodyPartsSummary: "Lower back",
                toDateOfInjury: toDateOfInjury,
                wcabAdj: wcabAdj),
            WcabOffice = office,
        };

    private static InjuryResolver Build(params AppointmentInjuryDetailWithNavigationProperties[] rows)
    {
        var repo = Substitute.For<IAppointmentInjuryDetailRepository>();
        repo.GetListWithNavigationPropertiesAsync(
                Arg.Any<string?>(), AppointmentId, Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(rows.ToList()));

        return new InjuryResolver(repo);
    }

    [Fact]
    public async Task BothInjuriesOnATwoInjuryAppointment_ArePublished()
    {
        var resolver = Build(
            Row("WC-SAMPLE-A", "ADJ-SAMPLE-A", new DateTime(2025, 11, 14)),
            Row("WC-SAMPLE-B", "ADJ-SAMPLE-B", new DateTime(2024, 3, 1)));

        var result = await resolver.ResolveAsync(AppointmentId);

        result.Count.ShouldBe(2);
        result.Select(e => e.ClaimNumber).ShouldBe(new[] { "WC-SAMPLE-B", "WC-SAMPLE-A" });
    }

    [Fact]
    public async Task WithNoInjuryRows_AnEmptyListIsReturned()
    {
        // The booking guard against this is client-side only, so zero rows must not break a push.
        var resolver = Build();

        var result = await resolver.ResolveAsync(AppointmentId);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task IdentifiersArePublishedRawAndNormalized()
    {
        var resolver = Build(Row("wc-sample-a", "adj sample a", new DateTime(2025, 11, 14)));

        var entry = (await resolver.ResolveAsync(AppointmentId)).Single();

        entry.ClaimNumber.ShouldBe("wc-sample-a");            // exactly as typed, for display
        entry.ClaimNumberNormalized.ShouldBe("WCSAMPLEA");    // comparable, for grouping
        entry.WcabAdj.ShouldBe("adj sample a");
        entry.WcabAdjNormalized.ShouldBe("ADJSAMPLEA");
    }

    [Fact]
    public async Task ASpecificInjuryHasNoEndDate_AndACumulativeOneDoes()
    {
        var resolver = Build(
            Row("WC-SAMPLE-A", "ADJ-SAMPLE-A", new DateTime(2025, 11, 14)),
            Row("WC-SAMPLE-B", "ADJ-SAMPLE-B", new DateTime(2024, 3, 1),
                toDateOfInjury: new DateTime(2025, 10, 31), cumulative: true));

        var result = await resolver.ResolveAsync(AppointmentId);

        var specific = result.Single(e => e.ClaimNumber == "WC-SAMPLE-A");
        specific.IsCumulativeInjury.ShouldBeFalse();
        specific.ToDateOfInjury.ShouldBeNull();

        var cumulative = result.Single(e => e.ClaimNumber == "WC-SAMPLE-B");
        cumulative.IsCumulativeInjury.ShouldBeTrue();
        cumulative.ToDateOfInjury.ShouldBe("2025-10-31");
        cumulative.DateOfInjury.ShouldBe("2024-03-01");
    }

    [Fact]
    public async Task WithNoWcabOffice_TheOfficeIsNullRatherThanThrowing()
    {
        var resolver = Build(Row("WC-SAMPLE-A", "ADJ-SAMPLE-A", new DateTime(2025, 11, 14)));

        (await resolver.ResolveAsync(AppointmentId)).Single().WcabOffice.ShouldBeNull();
    }

    [Fact]
    public async Task TheWcabOfficeIsSentAsNameAndAbbreviation_NotItsId()
    {
        var office = new WcabOffice(
            Guid.NewGuid(), stateId: null, name: "Van Nuys", abbreviation: "VNO", isActive: true);
        var resolver = Build(Row("WC-SAMPLE-A", "ADJ-SAMPLE-A", new DateTime(2025, 11, 14), office: office));

        var resolved = (await resolver.ResolveAsync(AppointmentId)).Single().WcabOffice;

        resolved.ShouldNotBeNull();
        resolved!.Name.ShouldBe("Van Nuys");
        resolved.Abbreviation.ShouldBe("VNO");
    }
}
