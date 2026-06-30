using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Pins the fix-it server-side lock: a correction may only touch fields the open
/// request flagged. With the generic key-&gt;value corrections map (QA item L), the rule
/// is "every provided key must be flagged". Security-path unit (pure, no DB), reachable
/// via the Application InternalsVisibleTo wiring.
/// </summary>
public class InfoRequestCorrectionLockTests
{
    [Fact]
    public void Allows_changes_to_flagged_fields()
    {
        var provided = new[] { "cellPhoneNumber", "street" };
        var flagged = new HashSet<string> { "cellPhoneNumber", "street", "city" };

        InfoRequestCorrectionLock.FindUnflaggedChanges(provided, flagged).ShouldBeEmpty();
    }

    [Fact]
    public void Rejects_a_change_to_an_unflagged_field()
    {
        var provided = new[] { "cellPhoneNumber" };
        var flagged = new HashSet<string> { "dateOfBirth" };

        InfoRequestCorrectionLock.FindUnflaggedChanges(provided, flagged).ShouldContain("cellPhoneNumber");
    }

    [Fact]
    public void Reports_every_unflagged_change_once()
    {
        var provided = new[] { "dateOfBirth", "applicantAttorneyEmail", "dateOfBirth" };

        var violations = InfoRequestCorrectionLock.FindUnflaggedChanges(provided, new HashSet<string>());

        violations.ShouldContain("dateOfBirth");
        violations.ShouldContain("applicantAttorneyEmail");
        violations.Count.ShouldBe(2); // de-duped
    }

    [Fact]
    public void No_provided_keys_has_no_violations()
    {
        InfoRequestCorrectionLock
            .FindUnflaggedChanges(System.Array.Empty<string>(), new HashSet<string>())
            .ShouldBeEmpty();
    }
}
