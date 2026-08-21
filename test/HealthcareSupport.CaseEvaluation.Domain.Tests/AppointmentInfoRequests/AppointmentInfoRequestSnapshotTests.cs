using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Pins the Branch 2 value-snapshot columns on AppointmentInfoRequest: the "before"
/// is captured at send-back, the "after" at resubmit, and each round-trips the
/// stored JSON independently. Values are synthetic non-PHI sentinels.
/// </summary>
public class AppointmentInfoRequestSnapshotTests
{
    private static AppointmentInfoRequest NewRequest() =>
        new(Guid.NewGuid(), null, Guid.NewGuid(), "Please fix.", "[]", Guid.NewGuid());

    [Fact]
    public void New_request_has_no_snapshots()
    {
        var entity = NewRequest();

        entity.BeforeValues.ShouldBeNull();
        entity.AfterValues.ShouldBeNull();
    }

    [Fact]
    public void Captures_before_and_after_independently()
    {
        var entity = NewRequest();

        entity.CaptureBeforeValues("{\"cellPhoneNumber\":\"old-value\"}");
        entity.CaptureAfterValues("{\"cellPhoneNumber\":\"new-value\"}");

        entity.BeforeValues!.ShouldContain("old-value");
        entity.AfterValues!.ShouldContain("new-value");
    }
}
