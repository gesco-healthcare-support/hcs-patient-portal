using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// The structural invariant behind the transition snapshot.
///
/// A snapshot can always be regenerated carelessly to make a build pass. This test refuses one
/// SHAPE whatever the snapshot says: a member of <see cref="AppointmentTransitionTrigger"/> that
/// no transition uses. Such a trigger is half-added -- declared, callable by name, and certain
/// to throw an invalid-transition error from every state -- and a regenerated snapshot would
/// record it without complaint.
/// </summary>
public sealed class AppointmentTransitionSurfaceInvariantTests
{
    [Fact]
    public void Every_trigger_is_used_by_at_least_one_transition()
    {
        var info = AppointmentTransitionSurface.Info();

        var unused = Enum.GetValues<AppointmentTransitionTrigger>()
            .Where(t => AppointmentTransitionSurface.TransitionsUsing(info, t) == 0)
            .Select(t => t.ToString())
            .ToList();

        unused.ShouldBeEmpty(
            "these triggers are declared on AppointmentTransitionTrigger but no transition in " +
            "AppointmentManager.BuildMachine uses them: " + string.Join(", ", unused) +
            ". Configure a transition for each, or remove the member.");
    }
}
