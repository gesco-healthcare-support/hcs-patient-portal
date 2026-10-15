using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.PhiAccess;

/// <summary>
/// Pins the integer of every <see cref="PhiAccessAction"/> member. The access log stores the action
/// as an integer and is kept for years, so renumbering a member would relabel every row already
/// written: a download would read back as a view. The map is written as literals, not derived from
/// the enum. Do not edit a literal to make this pass; add a new member with a new integer instead.
/// </summary>
public class PhiAccessActionContractTests
{
    private static readonly IReadOnlyDictionary<string, int> Contract = new Dictionary<string, int>
    {
        ["PatientViewed"] = 1,
        ["PatientSsnRevealed"] = 2,
        ["AppointmentViewed"] = 3,
        ["DocumentDownloaded"] = 4,
        ["PacketDownloaded"] = 5,
        ["ReportPdfExported"] = 6,
        ["ReportCsvExported"] = 7,
        ["DemographicsExported"] = 8,
        ["AppointmentListCsvExported"] = 9,
    };

    [Fact]
    public void Every_member_keeps_its_exact_integer_and_none_is_added_unpinned()
    {
        foreach (var (name, value) in Contract)
        {
            ((int)Enum.Parse<PhiAccessAction>(name)).ShouldBe(value,
                $"PhiAccessAction.{name} must stay = {value}; changing it relabels stored access-log rows.");
        }

        Enum.GetNames<PhiAccessAction>().OrderBy(n => n, StringComparer.Ordinal)
            .ShouldBe(Contract.Keys.OrderBy(n => n, StringComparer.Ordinal),
                "PhiAccessAction changed. Add a member with a NEW integer and pin it here; never remove one.");
    }
}
