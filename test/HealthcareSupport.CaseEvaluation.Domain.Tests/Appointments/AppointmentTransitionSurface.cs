using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HealthcareSupport.CaseEvaluation.Enums;
using Stateless.Reflection;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Renders the appointment status transitions declared by
/// <see cref="AppointmentManager.BuildMachine"/> into a deterministic, sorted table.
///
/// WHY THIS EXISTS. The transitions used to be copied by hand into
/// docs/business-domain/APPOINTMENT-LIFECYCLE.md, and the copy drifted: before the 2026-09
/// documentation pass it taught eleven transitions the machine does not permit, missed three
/// it does, and presented the unreachable CheckedIn -> CheckedOut -> Billed chain as the happy
/// path. A hand-kept list cannot notice that the code moved. This type reads the machine's own
/// description of itself (Stateless <c>GetInfo()</c>), so the committed snapshot can only
/// differ from the code when someone has changed the code.
///
/// WHAT IT RECORDS: raw configuration, never conclusions. Every fixed transition (with any
/// guard description Stateless reports), every dynamic transition, ignored trigger and
/// superstate link, one line per <see cref="AppointmentStatusType"/> member with its outgoing
/// count, and one line per <see cref="AppointmentTransitionTrigger"/> member with its usage
/// count. It deliberately renders no "dead", "terminal" or "happy path" verdict: a derived
/// verdict that is wrong does not look wrong, it makes the detector blind (the lesson recorded
/// in AuthorizationSurface in Application.Tests).
///
/// WHAT IT DOES NOT PROVE:
/// - that a declared transition is reachable. CheckIn, CheckOut and Bill are declared and
///   appear here, but nothing in production fires them;
/// - the checks made outside the machine: the approval gates
///   (AppointmentManager.FindUnmetApprovalGateAsync), consent gating and permissions;
/// - status changes that bypass the machine. Cancellation approval writes the status directly
///   (AppointmentChangeRequestsAppService.Approval.cs), so this table does not cover that path;
/// - the status an appointment is created with (always Pending), or re-booking, which creates
///   a new appointment rather than transitioning an existing one.
/// </summary>
public static class AppointmentTransitionSurface
{
    /// <summary>
    /// The machine's description of its own configuration. The appointment passed in only
    /// supplies the state accessor Stateless requires; nothing is fired, so its values are
    /// irrelevant and synthetic.
    /// </summary>
    public static StateMachineInfo Info()
    {
        var appointment = new Appointment(
            id: new Guid("7a1c0000-0000-4000-8000-00000000a001"),
            patientId: new Guid("7a1c0000-0000-4000-8000-00000000a002"),
            identityUserId: null,
            appointmentTypeId: new Guid("7a1c0000-0000-4000-8000-00000000a003"),
            locationId: new Guid("7a1c0000-0000-4000-8000-00000000a004"),
            doctorAvailabilityId: new Guid("7a1c0000-0000-4000-8000-00000000a005"),
            appointmentDate: new DateTime(2030, 1, 7, 9, 0, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A00001",
            appointmentStatus: AppointmentStatusType.Pending);

        return AppointmentManager.BuildMachine(appointment).GetInfo();
    }

    /// <summary>The surface of the production machine, as committed in the approved file.</summary>
    public static string Render()
    {
        return Render(Info());
    }

    /// <summary>
    /// Renders <paramref name="info"/> as LF-terminated lines in a fixed section order
    /// (transition, dynamic, ignore, superstate, state, trigger), each section sorted by the
    /// numeric enum values. Numbers rather than culture-sensitive text keep the output
    /// byte-identical on every machine.
    /// </summary>
    public static string Render(StateMachineInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        var states = info.States.ToList();
        var builder = new StringBuilder();

        foreach (var (from, transition) in states
                     .SelectMany(s => s.FixedTransitions.Select(t => (From: State(s), Transition: t)))
                     .OrderBy(x => (int)x.From)
                     .ThenBy(x => (int)Trigger(x.Transition))
                     .ThenBy(x => (int)State(x.Transition.DestinationState)))
        {
            builder.Append("transition ")
                   .Append(Label(from))
                   .Append(" --")
                   .Append(Label(Trigger(transition)))
                   .Append("--> ")
                   .Append(Label(State(transition.DestinationState)))
                   .Append(Guards(transition))
                   .Append('\n');
        }

        foreach (var (from, transition) in states
                     .SelectMany(s => s.DynamicTransitions.Select(t => (From: State(s), Transition: t)))
                     .OrderBy(x => (int)x.From)
                     .ThenBy(x => (int)Trigger(x.Transition)))
        {
            builder.Append("dynamic ")
                   .Append(Label(from))
                   .Append(" --")
                   .Append(Label(Trigger(transition)))
                   .Append("--> ? selector=")
                   .Append(transition.DestinationStateSelectorDescription?.Description ?? "-")
                   .Append('\n');
        }

        foreach (var (state, ignored) in states
                     .SelectMany(s => s.IgnoredTriggers.Select(t => (State: State(s), Ignored: t)))
                     .OrderBy(x => (int)x.State)
                     .ThenBy(x => (int)Trigger(x.Ignored)))
        {
            builder.Append("ignore ")
                   .Append(Label(state))
                   .Append(' ')
                   .Append(Label(Trigger(ignored)))
                   .Append('\n');
        }

        foreach (var state in states.Where(s => s.Superstate != null).OrderBy(s => (int)State(s)))
        {
            builder.Append("superstate ")
                   .Append(Label(State(state)))
                   .Append(" of ")
                   .Append(Label(State(state.Superstate)))
                   .Append('\n');
        }

        foreach (var status in Enum.GetValues<AppointmentStatusType>().OrderBy(v => (int)v))
        {
            var outgoing = states
                .Where(s => State(s) == status)
                .Sum(s => s.FixedTransitions.Count() + s.DynamicTransitions.Count());
            builder.Append("state ")
                   .Append(Label(status))
                   .Append(": ")
                   .Append(outgoing.ToString(CultureInfo.InvariantCulture))
                   .Append(" outgoing\n");
        }

        foreach (var trigger in Enum.GetValues<AppointmentTransitionTrigger>().OrderBy(v => (int)v))
        {
            builder.Append("trigger ")
                   .Append(Label(trigger))
                   .Append(": ")
                   .Append(TransitionsUsing(info, trigger).ToString(CultureInfo.InvariantCulture))
                   .Append(" transition(s)\n");
        }

        return builder.ToString();
    }

    /// <summary>How many fixed or dynamic transitions in <paramref name="info"/> fire on <paramref name="trigger"/>.</summary>
    public static int TransitionsUsing(StateMachineInfo info, AppointmentTransitionTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(info);

        return info.States.Sum(s =>
            s.FixedTransitions.Count(t => Trigger(t) == trigger) +
            s.DynamicTransitions.Count(t => Trigger(t) == trigger));
    }

    private static AppointmentStatusType State(StateInfo state)
    {
        return (AppointmentStatusType)state.UnderlyingState;
    }

    private static AppointmentTransitionTrigger Trigger(TransitionInfo transition)
    {
        return (AppointmentTransitionTrigger)transition.Trigger.UnderlyingTrigger;
    }

    private static AppointmentTransitionTrigger Trigger(IgnoredTransitionInfo ignored)
    {
        return (AppointmentTransitionTrigger)ignored.Trigger.UnderlyingTrigger;
    }

    /// <summary>Guard descriptions exactly as Stateless reports them, or nothing when unguarded.</summary>
    private static string Guards(TransitionInfo transition)
    {
        var descriptions = transition.GuardConditionsMethodDescriptions
            .Select(g => g.Description)
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();

        return descriptions.Count == 0 ? string.Empty : " guard=" + string.Join(" + ", descriptions);
    }

    /// <summary><c>Name(value)</c>, so a renumbered member changes the snapshot as well as a renamed one.</summary>
    private static string Label<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return Enum.GetName(value) + "(" + Convert.ToInt32(value, CultureInfo.InvariantCulture)
            .ToString(CultureInfo.InvariantCulture) + ")";
    }
}
