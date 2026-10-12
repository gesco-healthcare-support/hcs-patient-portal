using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Enums;

/// <summary>
/// Pins the integer value of every <see cref="AppointmentStatusType"/> member.
///
/// <para>WHY THIS EXISTS. The status is persisted as an INTEGER (the
/// <c>AppointmentStatus</c> column on an appointment), the member NAME is sent to the
/// Case Tracker on the intake wire, and the name is a key in <c>en.json</c>. Renumbering
/// a member, or removing one from the middle, silently RELABELS every stored row whose
/// value is equal or higher -- there is no migration unless someone writes one.</para>
///
/// <para>In particular <see cref="AppointmentStatusType.CheckedIn"/> = 9,
/// <see cref="AppointmentStatusType.CheckedOut"/> = 10 and
/// <see cref="AppointmentStatusType.Billed"/> = 11 are DEAD (nothing transitions into
/// them) but RESERVED FOREVER: removing them would shift
/// <see cref="AppointmentStatusType.RescheduleRequested"/> = 12,
/// <see cref="AppointmentStatusType.CancellationRequested"/> = 13,
/// <see cref="AppointmentStatusType.InfoRequested"/> = 14 and
/// <see cref="AppointmentStatusType.NotSeen"/> = 15, which are LIVE.</para>
///
/// <para>The map below is the CONTRACT, written as literals on purpose -- it is not
/// derived from the enum, so a change to the enum is measured against a fixed expectation
/// rather than against itself. If a test here fails you are about to corrupt stored data:
/// do NOT make it pass by editing these literals to match the enum. Add a new member with
/// a new, never-reused integer; never renumber or remove one.</para>
/// </summary>
public class AppointmentStatusTypeContractTests
{
    private static readonly IReadOnlyDictionary<string, int> Contract = new Dictionary<string, int>
    {
        ["Pending"] = 1,
        ["Approved"] = 2,
        ["Rejected"] = 3,
        ["NoShow"] = 4,
        ["CancelledNoBill"] = 5,
        ["CancelledLate"] = 6,
        ["RescheduledNoBill"] = 7,
        ["RescheduledLate"] = 8,
        ["CheckedIn"] = 9,
        ["CheckedOut"] = 10,
        ["Billed"] = 11,
        ["RescheduleRequested"] = 12,
        ["CancellationRequested"] = 13,
        ["InfoRequested"] = 14,
        ["NotSeen"] = 15,
    };

    [Fact]
    public void Every_pinned_member_keeps_its_exact_integer()
    {
        foreach (var (name, value) in Contract)
        {
            Enum.IsDefined(typeof(AppointmentStatusType), name).ShouldBeTrue(
                $"AppointmentStatusType.{name} was removed, but integer {value} is reserved -- " +
                "removing a member relabels stored rows. Keep it declared.");

            ((int)Enum.Parse<AppointmentStatusType>(name)).ShouldBe(value,
                $"AppointmentStatusType.{name} must stay = {value}. Changing it relabels every " +
                "persisted row and every Case Tracker payload that carries this value.");
        }
    }

    [Fact]
    public void No_member_is_added_or_removed_beyond_the_contract()
    {
        var actual = Enum.GetNames<AppointmentStatusType>().OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var expected = Contract.Keys.OrderBy(n => n, StringComparer.Ordinal).ToArray();

        actual.ShouldBe(expected,
            "AppointmentStatusType's member set changed. To ADD a status: give it a new integer " +
            "(never reuse an old one) and add it here. To REMOVE one: STOP -- the enum persists as " +
            "integers, so removal relabels stored rows and is a data migration, not a code change. " +
            "See the comment at the top of this file.");
    }
}
