using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Unit coverage for <see cref="IntakeChangedEmailHandler"/>: the per-field "what changed" table
/// emailed to the office inbox when staff edit an intake (2026-11-03: inbox only, and no second
/// reschedule email when the date or time moved).
///
/// <para><b>THE TWO GUARANTEES THAT MATTER MOST.</b> A field marked <c>ValueRedacted</c> renders as
/// "updated (value hidden)" and NEITHER its old nor its new value appears anywhere in what is
/// dispatched -- the handler must not undo redaction even if a value reaches it. And every value
/// and label that IS shown is HTML-encoded, so text typed into an intake cannot inject markup into
/// an email that attorneys and staff open.</para>
///
/// <para><b>WHY THE POSITIVE CONTROL IS NOT OPTIONAL.</b> An unconfigured substitute makes the
/// handler bail out early, which also dispatches nothing. The negative Facts mean something only
/// because <see cref="HandleEventAsync_OneChangedField_EmailsTheChangeTable_PositiveControl"/> shows
/// this fixture reaches the dispatcher. Same rule as <c>PatientPacketEmailKindGateTests</c>.</para>
///
/// <para><b>NO MAIL CAN LEAVE.</b> <see cref="INotificationDispatcher"/> is fully substituted and the
/// handler is built with <c>new</c> (its <c>[UnitOfWork]</c> is inert). No database is touched.</para>
///
/// <para>Synthetic data only (HIPAA).</para>
/// </summary>
public class IntakeChangedEmailHandlerTests
{
    private sealed class Rig
    {
        public INotificationDispatcher Dispatcher { get; } = Substitute.For<INotificationDispatcher>();

        /// <summary>
        /// The ten nulls are never dereferenced: <c>ResolveAsync</c> is <c>virtual</c> and is
        /// configured below, so the real body never runs.
        /// </summary>
        public DocumentEmailContextResolver ContextResolver { get; } =
            Substitute.For<DocumentEmailContextResolver>(null, null, null, null, null, null, null, null, null, null);

        /// <summary>The office inbox is the only recipient (2026-11-03); Intake Staff are listed to prove they are not.</summary>
        public FakeOfficeRecipients Office { get; } = new() { Inbox = "TEST-inbox@test.local" };

        public ICurrentTenant CurrentTenant { get; } = Substitute.For<ICurrentTenant>();

        public DocumentEmailContext Context { get; } = new()
        {
            AppointmentId = Guid.NewGuid(),
            RequestConfirmationNumber = "TEST-A0003",
            AppointmentDate = new DateTime(2026, 10, 5, 9, 30, 0),
            PatientFirstName = "TEST-First",
            PatientLastName = "TEST-Last",
            PortalBaseUrl = "https://tenant.portal.test.local",
        };

        public Rig()
        {
            ContextResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns(_ => Context);
            // DECOY, load-bearing (#1014): production's ambient name is null, so ClinicName
            // must never come from it.
            CurrentTenant.Name.Returns("TEST-clinic");
        }

        public IntakeChangedEmailHandler Build() => new(
            Dispatcher,
            ContextResolver,
            Office,
            CurrentTenant,
            NullLogger<IntakeChangedEmailHandler>.Instance);
    }

    /// <summary>The arguments of one <c>DispatchAsync</c> call, read back from the substitute.</summary>
    private sealed record SentEmail(
        string TemplateCode,
        IReadOnlyCollection<NotificationRecipient> Recipients,
        IReadOnlyDictionary<string, object?> Variables,
        string ContextTag);

    private static IntakeChangedField Field(string name, string? oldValue, string? newValue, bool redacted = false) => new()
    {
        Section = "TEST-section",
        FieldName = name,
        OldValue = oldValue,
        NewValue = newValue,
        ValueRedacted = redacted,
    };

    private static AppointmentIntakeChangedEto ChangedEvent(bool dateOrTimeChanged = false, params IntakeChangedField[] fields)
    {
        var evt = new AppointmentIntakeChangedEto
        {
            AppointmentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            DateOrTimeChanged = dateOrTimeChanged,
            OccurredAt = new DateTime(2026, 10, 23, 18, 30, 0, DateTimeKind.Utc),
        };
        evt.ChangedFields.AddRange(fields);
        return evt;
    }

    private static AppointmentIntakeChangedEto OneFieldChanged() =>
        ChangedEvent(false, Field("PanelNumber", "TEST-P1", "TEST-P2"));

    private static List<SentEmail> Sends(INotificationDispatcher dispatcher) =>
        dispatcher.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(INotificationDispatcher.DispatchAsync))
            .Select(c => c.GetArguments())
            .Select(a => new SentEmail(
                (string)a[0]!,
                (IReadOnlyCollection<NotificationRecipient>)a[1]!,
                (IReadOnlyDictionary<string, object?>)a[2]!,
                (string)a[3]!))
            .ToList();

    /// <summary>Reads the ONE dispatch, asserting the count first for a readable failure.</summary>
    private static SentEmail SingleSend(INotificationDispatcher dispatcher)
    {
        var sends = Sends(dispatcher);
        sends.Count.ShouldBe(1, "a change that does not move the date or time sends exactly one email");
        return sends[0];
    }

    private static string ChangeTable(SentEmail sent) => (string)sent.Variables["AppointmentChangeLogs"]!;

    /// <summary>
    /// <b>POSITIVE CONTROL -- load-bearing.</b> Proves this fixture reaches the dispatcher at all.
    /// Every "dispatches nothing" Fact below depends on it.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_OneChangedField_EmailsTheChangeTable_PositiveControl()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(OneFieldChanged());

        var sent = SingleSend(rig.Dispatcher);
        sent.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentChangeLogs);
        sent.Recipients.Select(r => r.Email).ShouldBe(new[] { "TEST-inbox@test.local" });
    }

    /// <summary>
    /// Issue #1014: the handler leaves <c>ClinicName</c> blank for the renderer to fill from the tenant
    /// store. The rig's ambient office name is a DECOY -- production's is null inside
    /// <c>Change(TenantId)</c> -- so a handler that passed it through would pass here and blank it live.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_LeavesClinicNameForTheRenderer_NotTheAmbientName()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(OneFieldChanged());

        SingleSend(rig.Dispatcher).Variables["ClinicName"].ShouldBe(
            string.Empty, "the renderer resolves the office name from the tenant store (#1014)");
    }

    [Fact]
    public async Task HandleEventAsync_NullEvent_DispatchesNothing()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(null!);

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty(
            "a null event must be ignored; the positive control proves this fixture otherwise dispatches");
    }

    /// <summary>
    /// An edit that changed nothing sends nothing -- and stops before looking anything up, which is
    /// what distinguishes this guard from the later "no recipients" one.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_NoChangedFields_DispatchesNothingAndLooksNothingUp()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(ChangedEvent());

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty("an empty diff must not produce an empty change table email");
        await rig.ContextResolver.DidNotReceive().ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>());
    }

    [Fact]
    public async Task HandleEventAsync_AppointmentNotFound_DispatchesNothing()
    {
        var rig = new Rig();
        rig.ContextResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns((DocumentEmailContext?)null);

        await rig.Build().HandleEventAsync(OneFieldChanged());

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty(
            "with no appointment context the handler must skip, not throw");
    }

    /// <summary>
    /// 2026-11-03: the change log is the office's own housekeeping, so it goes to the office inbox ONLY:
    /// neither the parties nor the Intake Staff are mailed.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_GoesToTheOfficeInboxOnly_NotToPartiesOrIntakeStaff()
    {
        var rig = new Rig();
        rig.Office.Intake.Add("TEST-intake@test.local");
        var evt = OneFieldChanged();

        await rig.Build().HandleEventAsync(evt);

        var recipients = SingleSend(rig.Dispatcher).Recipients;
        recipients.Select(r => r.Email).ShouldBe(new[] { "TEST-inbox@test.local" });
    }

    [Fact]
    public async Task HandleEventAsync_NoOfficeInbox_DispatchesNothing()
    {
        var rig = new Rig();
        rig.Office.Inbox = null;
        rig.Office.Intake.Add("TEST-intake@test.local");

        await rig.Build().HandleEventAsync(OneFieldChanged());

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty(
            "with no office inbox there is nobody to send the change log to; Intake Staff do not receive it");
    }

    /// <summary>
    /// <b>PHI.</b> A redacted field shows only that it changed. Its old and new values must not
    /// appear in the table -- nor in ANY dispatched variable -- even though the event carries them.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_RedactedField_ShowsItChangedButNeverItsValues()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(ChangedEvent(false,
            Field("TEST-SensitiveField", "TEST-secret-old-value", "TEST-secret-new-value", redacted: true)));

        var sent = SingleSend(rig.Dispatcher);
        ChangeTable(sent).ShouldContain("<td>TEST-SensitiveField</td><td colspan=\"2\"><em>updated (value hidden)</em></td>");
        foreach (var value in sent.Variables.Values.OfType<string>())
        {
            value.ShouldNotContain("TEST-secret-old-value", Case.Sensitive, "a redacted OLD value leaked into the email");
            value.ShouldNotContain("TEST-secret-new-value", Case.Sensitive, "a redacted NEW value leaked into the email");
        }
    }

    /// <summary>
    /// <b>Injection.</b> Values that are shown are HTML-encoded, so text typed into an intake renders
    /// as text rather than markup in the email.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_ShownValues_AreHtmlEncoded()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(ChangedEvent(false,
            Field("PanelNumber", "<b>TEST-old</b>", "<script>TEST-new</script>")));

        var table = ChangeTable(SingleSend(rig.Dispatcher));
        table.ShouldContain("<td>&lt;b&gt;TEST-old&lt;/b&gt;</td><td>&lt;script&gt;TEST-new&lt;/script&gt;</td>");
        table.ShouldNotContain("<script>");
        table.ShouldNotContain("<b>TEST-old</b>");
    }

    [Fact]
    public async Task HandleEventAsync_AMissingOldOrNewValue_RendersAsADash()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(ChangedEvent(false,
            Field("TEST-FieldAdded", null, "TEST-new"),
            Field("TEST-FieldCleared", "TEST-old", null)));

        var table = ChangeTable(SingleSend(rig.Dispatcher));
        table.ShouldContain("<td>TEST-FieldAdded</td><td>-</td><td>TEST-new</td>");
        table.ShouldContain("<td>TEST-FieldCleared</td><td>TEST-old</td><td>-</td>");
    }

    [Theory]
    [InlineData("AppointmentDate", "Appointment Date")]
    [InlineData("PanelNumber", "Panel Number")]
    [InlineData("DueDate", "Due Date")]
    [InlineData("TEST-UnmappedField", "TEST-UnmappedField")]
    public async Task HandleEventAsync_ShowsAFriendlyLabelForKnownFieldsAndTheRawNameOtherwise(
        string fieldName, string expectedLabel)
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(ChangedEvent(false, Field(fieldName, "TEST-a", "TEST-b")));

        ChangeTable(SingleSend(rig.Dispatcher)).ShouldContain($"<tr><td>{expectedLabel}</td><td>TEST-a</td>");
    }

    [Fact]
    public async Task HandleEventAsync_TheFieldLabelIsHtmlEncodedToo()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(ChangedEvent(false, Field("TEST<Field>&", "TEST-a", "TEST-b")));

        var table = ChangeTable(SingleSend(rig.Dispatcher));
        table.ShouldContain("<tr><td>TEST&lt;Field&gt;&amp;</td>");
        table.ShouldNotContain("TEST<Field>");
    }

    /// <summary>
    /// 2026-11-03: a date or time change no longer sends the second, reschedule-by-admin notice -- the
    /// change log to the office inbox is the only email.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_DateOrTimeChanged_StillSendsOnlyTheChangeTable()
    {
        var rig = new Rig();
        var evt = ChangedEvent(true, Field("AppointmentDate", "TEST-a", "TEST-b"));

        await rig.Build().HandleEventAsync(evt);

        var sent = SingleSend(rig.Dispatcher);
        sent.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentChangeLogs);
        Sends(rig.Dispatcher).ShouldNotContain(
            e => e.TemplateCode == NotificationTemplateConsts.Codes.AppointmentRescheduleRequestByAdmin);
    }

    [Fact]
    public async Task HandleEventAsync_DateAndTimeUnchanged_SendsOnlyTheChangeTable()
    {
        var rig = new Rig();
        var evt = OneFieldChanged();

        await rig.Build().HandleEventAsync(evt);

        var sent = SingleSend(rig.Dispatcher);
        sent.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentChangeLogs);
        // Each edit of the same appointment is its own email (NotificationOccurrence).
        sent.ContextTag.ShouldBe($"IntakeChanged/{evt.AppointmentId}/t{evt.OccurredAt.Ticks}");
    }
}
