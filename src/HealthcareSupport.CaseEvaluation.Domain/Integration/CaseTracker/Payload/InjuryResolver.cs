using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Builds the <c>injuries</c> array: the claim data the receiver's staff use to decide which of a
/// patient's records a case files under.
///
/// <para>Uses the repository's navigation-properties query so the injuries and their WCAB offices arrive
/// in ONE round trip rather than one lookup per row.</para>
/// </summary>
public class InjuryResolver : ITransientDependency
{
    private readonly IAppointmentInjuryDetailRepository _injuryRepository;

    public InjuryResolver(IAppointmentInjuryDetailRepository injuryRepository)
    {
        _injuryRepository = injuryRepository;
    }

    /// <summary>
    /// Every injury on the appointment, oldest injury first so the order is at least deterministic.
    ///
    /// <para>Returns an empty list rather than throwing when there are none. Booking blocks submit
    /// without at least one entry, but that guard lives in the Angular form and injury rows are written
    /// in a separate call, so zero rows is possible and must not break a push.</para>
    /// </summary>
    public virtual async Task<List<IntakeInjuryEntry>> ResolveAsync(
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _injuryRepository.GetListWithNavigationPropertiesAsync(
            appointmentId: appointmentId, cancellationToken: cancellationToken);

        return rows
            .Where(r => r.AppointmentInjuryDetail != null)
            .Select(Map)
            .OrderBy(e => e.DateOfInjury, StringComparer.Ordinal)
            .ToList();
    }

    private static IntakeInjuryEntry Map(AppointmentInjuryDetailWithNavigationProperties row)
    {
        var injury = row.AppointmentInjuryDetail;

        return new IntakeInjuryEntry
        {
            Id = injury.Id,
            DateOfInjury = IntegrationTimestamp.ToDateOnly(injury.DateOfInjury),
            ToDateOfInjury = injury.ToDateOfInjury.HasValue
                ? IntegrationTimestamp.ToDateOnly(injury.ToDateOfInjury.Value)
                : null,
            IsCumulativeInjury = injury.IsCumulativeInjury,
            ClaimNumber = injury.ClaimNumber,
            ClaimNumberNormalized = ClaimIdentifierNormalizer.Normalize(injury.ClaimNumber),
            WcabAdj = injury.WcabAdj,
            WcabAdjNormalized = ClaimIdentifierNormalizer.Normalize(injury.WcabAdj),
            BodyPartsSummary = injury.BodyPartsSummary,
            WcabOffice = row.WcabOffice == null
                ? null
                : new IntakeWcabOfficeSection
                {
                    Name = row.WcabOffice.Name,
                    Abbreviation = row.WcabOffice.Abbreviation,
                },
        };
    }
}
