using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Integration coverage for <see cref="AppointmentReminderEmailHandler"/>, the Group F consolidated
/// due-date reminder (phase 8 tranche 1). Nothing drove it before: a word-boundary search for the
/// type name across <c>test/</c> returns nothing.
///
/// <para>WHAT THESE FACTS PIN, AND IT IS NARROW BY NECESSITY. This handler is invoked by
/// <c>AppointmentReminderJob</c> for every appointment whose due date hits a reminder anchor. It
/// therefore runs in a LOOP over appointments, and an unhandled exception on one row stops reminders
/// for every appointment behind it. The two guards below are exactly the cases that would otherwise
/// throw -- a null event, and an appointment that no longer exists -- so they are worth more than
/// their line count suggests.</para>
///
/// <para>WHAT IS NOT COVERED, AND WHY. The dispatch path cannot complete in this rig.
/// <c>BookerCcDispatcher.DispatchToBookerWithCcAsync</c> renders
/// <c>NotificationTemplateConsts.Codes.AppointmentDueDateReminder</c>, which is tenant-only, and
/// <c>NotificationTemplateDataSeedContributor</c> seeds only the four host-scoped codes here. The
/// dispatcher has NO catch, so a real appointment throws
/// <c>BusinessException("CaseEvaluation:NotificationTemplate.NotFound")</c> out of the handler. An
/// assertion on that would pin the rig's gap rather than the product, and seeding a template to force
/// it green would be asserting against invented scaffolding -- an attempt to add per-tenant template
/// seeding broke the entire rig at module initialisation earlier today.</para>
///
/// <para>Also unreachable: <c>BuildOutstandingDocsBlockAsync</c>, <c>IsJointDeclarationOutstandingAsync</c>
/// and <c>ResolveGreetingName</c> are all private, so InternalsVisibleTo does not reach them. Note
/// additionally that the JDF branch could not fire here even if it were reachable -- it compares
/// against <c>CaseEvaluationSeedIds.AppointmentTypes.Ame</c>, and no <c>CaseEvaluationSeedIds</c> GUID
/// is a live row in this rig, because every production catalog seeder early-returns on host scope.</para>
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreAppointmentReminderEmailHandlerTests
    : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private readonly AppointmentReminderEmailHandler _handler;

    public EfCoreAppointmentReminderEmailHandlerTests()
    {
        _handler = GetRequiredService<AppointmentReminderEmailHandler>();
    }

    [Fact]
    public async Task HandleEventAsync_WithANullEvent_ReturnsWithoutThrowing()
    {
        // The handler is reached through the local event bus, which does not guarantee a non-null
        // payload to a handler written defensively. This is the first line of the method and it is
        // the cheapest possible protection for a job that processes appointments in a loop.
        await Should.NotThrowAsync(
            async () => await _handler.HandleEventAsync(null!),
            "A null event must be ignored, not thrown on. This handler runs inside a batch job; an "
            + "exception here stops the reminder run for every appointment behind it.");
    }

    [Fact]
    public async Task HandleEventAsync_WhenTheAppointmentNoLongerExists_SkipsInsteadOfThrowing()
    {
        // THE REAL GUARANTEE. A reminder event is raised by a scheduled job against a snapshot of
        // appointments; by the time the handler runs the row may have been deleted. The context
        // resolver returns null and the handler logs and returns.
        //
        // This id resolves to nothing, so the method returns BEFORE the recipient resolver and before
        // the dispatcher -- which is what keeps this Fact clear of the notification-template wall
        // described on the class. Delete the null check and this throws a NullReferenceException.
        var reminder = new AppointmentReminderEto
        {
            AppointmentId = Guid.NewGuid(),
            TenantId = TenantsTestData.TenantARef,
            DaysUntilDue = 7,
            OccurredAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        await Should.NotThrowAsync(
            async () => await _handler.HandleEventAsync(reminder),
            "A reminder for an appointment that has since been deleted must be skipped quietly. "
            + "Throwing here would abort the whole reminder run.");
    }

    [Fact]
    public async Task HandleEventAsync_WithNoTenantOnTheEvent_StillSkipsCleanlyForAnUnknownAppointment()
    {
        // TenantId is nullable on the ETO and the handler opens `_currentTenant.Change(eventData.TenantId)`
        // with whatever it receives. A null tenant is host scope, which is a legitimate state rather
        // than an error, and the unknown-appointment path must behave identically there.
        var reminder = new AppointmentReminderEto
        {
            AppointmentId = Guid.NewGuid(),
            TenantId = null,
            DaysUntilDue = 3,
            OccurredAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        await Should.NotThrowAsync(
            async () => await _handler.HandleEventAsync(reminder),
            "A host-scoped reminder event must not throw. CurrentTenant.Change(null) is valid.");
    }
}
