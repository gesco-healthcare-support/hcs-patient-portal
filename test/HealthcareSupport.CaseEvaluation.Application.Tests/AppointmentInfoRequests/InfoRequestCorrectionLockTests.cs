using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Pins the fix-it server-side lock: a correction may only touch fields the open
/// request flagged. Security-path unit (pure, no DB), reachable via the
/// Application InternalsVisibleTo wiring.
/// </summary>
public class InfoRequestCorrectionLockTests
{
    [Fact]
    public void Allows_a_change_to_a_flagged_field()
    {
        var input = new SaveInfoRequestCorrectionsInput { CellPhoneNumber = "(213) 555-0148" };
        var flagged = new HashSet<string> { "cellPhoneNumber" };

        InfoRequestCorrectionLock.FindUnflaggedChanges(input, flagged).ShouldBeEmpty();
    }

    [Fact]
    public void Rejects_a_change_to_an_unflagged_field()
    {
        var input = new SaveInfoRequestCorrectionsInput { CellPhoneNumber = "(213) 555-0148" };
        var flagged = new HashSet<string> { "dateOfBirth" };

        InfoRequestCorrectionLock.FindUnflaggedChanges(input, flagged).ShouldContain("cellPhoneNumber");
    }

    [Fact]
    public void Insurance_name_and_phone_share_one_flag()
    {
        var input = new SaveInfoRequestCorrectionsInput
        {
            InsuranceName = "Acme Mutual",
            InsurancePhoneNumber = "(800) 555-0100",
        };
        var flagged = new HashSet<string> { "appointmentInsuranceName" };

        InfoRequestCorrectionLock.FindUnflaggedChanges(input, flagged).ShouldBeEmpty();
    }

    [Fact]
    public void Allows_address_sub_field_changes_when_each_is_flagged()
    {
        var input = new SaveInfoRequestCorrectionsInput
        {
            Street = "128 W 4th St",
            City = "Los Angeles",
            StateId = Guid.NewGuid(),
            ZipCode = "90013",
        };
        var flagged = new HashSet<string> { "street", "city", "stateId", "zipCode" };

        InfoRequestCorrectionLock.FindUnflaggedChanges(input, flagged).ShouldBeEmpty();
    }

    [Fact]
    public void Rejects_an_unflagged_address_sub_field()
    {
        // Only street was flagged; correcting the zip is an unflagged change.
        var input = new SaveInfoRequestCorrectionsInput { ZipCode = "90013" };
        var flagged = new HashSet<string> { "street" };

        InfoRequestCorrectionLock.FindUnflaggedChanges(input, flagged).ShouldContain("zipCode");
    }

    [Fact]
    public void Empty_input_has_no_violations()
    {
        InfoRequestCorrectionLock
            .FindUnflaggedChanges(new SaveInfoRequestCorrectionsInput(), new HashSet<string>())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Reports_every_unflagged_change()
    {
        var input = new SaveInfoRequestCorrectionsInput
        {
            DateOfBirth = new DateTime(1985, 3, 22),
            ApplicantAttorneyEmail = "applicant@example.test",
        };

        var violations = InfoRequestCorrectionLock.FindUnflaggedChanges(input, new HashSet<string>());

        violations.ShouldContain("dateOfBirth");
        violations.ShouldContain("applicantAttorneyEmail");
    }
}
