using System.Collections.Concurrent;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Captures the local events <see cref="AppointmentsAppService"/> publishes, so integration tests
/// can assert on them.
///
/// <para>WHY THIS EXISTS RATHER THAN A SUBSTITUTED BUS. Six test files in this repository reference
/// <c>ILocalEventBus</c>, and every one of them does <c>Substitute.For&lt;ILocalEventBus&gt;()</c> --
/// they construct a manager by hand with a fake bus and assert on the substitute. That works for a
/// unit test of a manager. It does not work here: <c>UpdateAsync</c> runs through the real DI graph
/// in the EF Core rig, where the bus is the genuine one, and substituting it would mean changing the
/// rig for every other test in the collection.</para>
///
/// <para>So there was no pattern to copy and this is built rather than borrowed. ABP's conventional
/// registration scans the modules in the dependency graph, and
/// <c>CaseEvaluationEntityFrameworkCoreTestModule</c> depends on
/// <c>CaseEvaluationApplicationTestModule</c>, which lives in this assembly -- so a handler declared
/// here is discovered and wired automatically, with no module edit.</para>
///
/// <para>THE COLLECTION IS SHARED ACROSS THE WHOLE TEST COLLECTION, because this is a singleton and
/// the rig is shared. Never assert on a COUNT; always filter by an appointment id the calling test
/// created itself. A count assertion here would pass or fail depending on what else ran first.</para>
/// </summary>
public class AppointmentEventCapture : ISingletonDependency
{
    public ConcurrentBag<AppointmentIntakeChangedEto> IntakeChanged { get; } = new();

    public ConcurrentBag<AppointmentStatusChangedEto> StatusChanged { get; } = new();
}

public class CapturingIntakeChangedHandler
    : ILocalEventHandler<AppointmentIntakeChangedEto>, ITransientDependency
{
    private readonly AppointmentEventCapture _capture;

    public CapturingIntakeChangedHandler(AppointmentEventCapture capture)
    {
        _capture = capture;
    }

    public Task HandleEventAsync(AppointmentIntakeChangedEto eventData)
    {
        _capture.IntakeChanged.Add(eventData);
        return Task.CompletedTask;
    }
}

public class CapturingStatusChangedHandler
    : ILocalEventHandler<AppointmentStatusChangedEto>, ITransientDependency
{
    private readonly AppointmentEventCapture _capture;

    public CapturingStatusChangedHandler(AppointmentEventCapture capture)
    {
        _capture = capture;
    }

    public Task HandleEventAsync(AppointmentStatusChangedEto eventData)
    {
        _capture.StatusChanged.Add(eventData);
        return Task.CompletedTask;
    }
}
