using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Unit coverage for <see cref="AppointmentReminderEmailHandler"/>, the single consolidated due-date
/// reminder: ONE email To the booker, the other parties CC'd, listing the documents still needed.
///
/// <para><b>WHY A UNIT FILE BESIDE THE INTEGRATION ONE.</b>
/// <c>EfCoreAppointmentReminderEmailHandlerTests</c> pins only the null-event and appointment-gone
/// guards, because the EF Core rig cannot complete a dispatch: the reminder's template is tenant-only
/// and not seeded there. Here <see cref="BookerCcDispatcher"/> is substituted, so the dispatch path and
/// every private branch behind it -- the outstanding-documents block, the Joint Declaration check, the
/// greeting fallback -- are reachable through <c>HandleEventAsync</c>.</para>
///
/// <para><b>WHY THE POSITIVE CONTROL IS NOT OPTIONAL.</b> An unconfigured substitute makes the
/// handler bail out early, which also dispatches nothing. The negative Fact means something only
/// because <see cref="HandleEventAsync_OneStakeholderNothingOutstanding_SendsAPlainReminder_PositiveControl"/>
/// shows this fixture reaches the dispatcher. Same rule as <c>PatientPacketEmailKindGateTests</c>.</para>
///
/// <para><b>NO MAIL CAN LEAVE.</b> <see cref="BookerCcDispatcher"/> is substituted, so no template
/// renders and nothing is enqueued. Both repositories are substitutes; no database is touched. The
/// handler is built with <c>new</c> (its <c>[UnitOfWork]</c> is inert).</para>
///
/// <para>Synthetic data only (HIPAA).</para>
/// </summary>
public class AppointmentReminderEmailHandlerTests
{
    private const string JointDeclarationItem =
        "Joint Declaration Form (required - the appointment is auto-cancelled if not uploaded by the due date)";

    private sealed class Rig
    {
        /// <summary>
        /// The ten nulls are never dereferenced: <c>ResolveAsync</c> is <c>virtual</c> and is
        /// configured below, so the real body never runs.
        /// </summary>
        public DocumentEmailContextResolver ContextResolver { get; } =
            Substitute.For<DocumentEmailContextResolver>(null, null, null, null, null, null, null, null, null, null);

        public IAppointmentRecipientResolver RecipientResolver { get; } = Substitute.For<IAppointmentRecipientResolver>();

        /// <summary>Five nulls, same reason: <c>ResolveAsync</c> is virtual and configured.</summary>
        public MissingRequiredDocumentsResolver MissingResolver { get; } =
            Substitute.For<MissingRequiredDocumentsResolver>(null, null, null, null, null);

        /// <summary>Three nulls, same reason: <c>DispatchToBookerWithCcAsync</c> is virtual.</summary>
        public BookerCcDispatcher BookerCc { get; } = Substitute.For<BookerCcDispatcher>(null, null, null);

        public IRepository<Appointment, Guid> Appointments { get; } = Substitute.For<IRepository<Appointment, Guid>>();
        public IRepository<AppointmentDocument, Guid> Documents { get; } = Substitute.For<IRepository<AppointmentDocument, Guid>>();
        public ICurrentTenant CurrentTenant { get; } = Substitute.For<ICurrentTenant>();

        public DocumentEmailContext Context { get; } = new()
        {
            AppointmentId = Guid.NewGuid(),
            RequestConfirmationNumber = "TEST-A0006",
            AppointmentDate = new DateTime(2026, 10, 9, 10, 0, 0),
            DueDate = new DateTime(2026, 10, 2),
            BookerEmail = "TEST-booker@test.local",
            BookerFullName = "TEST-Booker Name",
            PatientFirstName = "TEST-First",
            PatientLastName = "TEST-Last",
            PortalBaseUrl = "https://tenant.portal.test.local",
        };

        public List<SendAppointmentEmailArgs> Stakeholders { get; } = new()
        {
            Party("TEST-booker@test.local", RecipientRole.ApplicantAttorney),
            Party("TEST-patient@test.local", RecipientRole.Patient),
        };

        /// <summary>A non-AME appointment by default, so no Joint Declaration item appears.</summary>
        public Appointment? Appointment { get; set; } = NewAppointment(Guid.NewGuid());

        public List<AppointmentDocument> StoredDocuments { get; } = new();

        public Rig()
        {
            ContextResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns(_ => Context);
            // List<T> and the sealed result record are not auto-substituted: unconfigured, both
            // would come back null and throw.
            RecipientResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>()).Returns(_ => Stakeholders);
            MissingResolver.ResolveAsync(Arg.Any<Guid>()).Returns(MissingRequiredDocumentsResult.Empty);
            Appointments.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(_ => Appointment);
            // The handler runs a synchronous .Any() over this queryable, so an in-memory list stands in.
            Documents.GetQueryableAsync().Returns(_ => StoredDocuments.AsQueryable());
            CurrentTenant.Name.Returns("TEST-clinic");
        }

        public AppointmentReminderEmailHandler Build() => new(
            ContextResolver,
            RecipientResolver,
            MissingResolver,
            BookerCc,
            Appointments,
            Documents,
            CurrentTenant,
            NullLogger<AppointmentReminderEmailHandler>.Instance);
    }

    /// <summary>The arguments of one <c>DispatchToBookerWithCcAsync</c> call, read back from the substitute.</summary>
    private sealed record SentReminder(
        string TemplateCode,
        string? BookerEmail,
        IReadOnlyCollection<NotificationRecipient> Stakeholders,
        IReadOnlyDictionary<string, object?> Variables,
        string ContextTag);

    private static SendAppointmentEmailArgs Party(string email, RecipientRole? role) => new()
    {
        To = email,
        Role = role,
        IsRegistered = true,
    };

    private static Appointment NewAppointment(Guid appointmentTypeId) => new(
        id: Guid.NewGuid(),
        patientId: Guid.NewGuid(),
        identityUserId: null,
        appointmentTypeId: appointmentTypeId,
        locationId: Guid.NewGuid(),
        doctorAvailabilityId: Guid.NewGuid(),
        appointmentDate: new DateTime(2026, 10, 9, 10, 0, 0),
        requestConfirmationNumber: "TEST-A0006",
        appointmentStatus: AppointmentStatusType.Approved);

    private static AppointmentDocument JointDeclaration(Guid appointmentId, DocumentStatus status) =>
        new(
            id: Guid.NewGuid(),
            tenantId: null,
            appointmentId: appointmentId,
            documentName: "TEST-joint-declaration.pdf",
            fileName: "TEST-joint-declaration.pdf",
            blobName: "TEST-blob",
            contentType: "application/pdf",
            fileSize: 1,
            uploadedByUserId: Guid.NewGuid())
        {
            IsJointDeclaration = true,
            Status = status,
        };

    private static MissingRequiredDocumentsResult Missing(params (string Name, RequiredDocumentState State)[] docs) =>
        new(docs.Length, docs.Select(d => new MissingRequiredDocument(Guid.NewGuid(), d.Name, d.State)).ToArray());

    private static AppointmentReminderEto ReminderEvent() => new()
    {
        AppointmentId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        DaysUntilDue = 7,
        OccurredAt = new DateTime(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc),
    };

    private static List<SentReminder> Sends(BookerCcDispatcher bookerCc) =>
        bookerCc.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(BookerCcDispatcher.DispatchToBookerWithCcAsync))
            .Select(c => c.GetArguments())
            .Select(a => new SentReminder(
                (string)a[0]!,
                (string?)a[1],
                (IReadOnlyCollection<NotificationRecipient>)a[2]!,
                (IReadOnlyDictionary<string, object?>)a[3]!,
                (string)a[4]!))
            .ToList();

    /// <summary>Reads the ONE reminder, asserting the count first for a readable failure.</summary>
    private static SentReminder SingleSend(BookerCcDispatcher bookerCc)
    {
        var sends = Sends(bookerCc);
        sends.Count.ShouldBe(1, "one reminder event sends exactly one consolidated email");
        return sends[0];
    }

    private static string OutstandingBlock(SentReminder sent) => (string)sent.Variables["OutstandingDocuments"]!;

    /// <summary>
    /// <b>POSITIVE CONTROL -- load-bearing.</b> Proves this fixture reaches the dispatcher at all. With
    /// nothing outstanding the documents block is EMPTY, so the email reads as a plain due-date nudge.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_OneStakeholderNothingOutstanding_SendsAPlainReminder_PositiveControl()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(ReminderEvent());

        var sent = SingleSend(rig.BookerCc);
        sent.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentDueDateReminder);
        OutstandingBlock(sent).ShouldBe(string.Empty);
    }

    [Fact]
    public async Task HandleEventAsync_EveryStakeholderAddressBlank_SendsNothing()
    {
        var rig = new Rig();
        rig.Stakeholders.Clear();
        rig.Stakeholders.Add(Party("", RecipientRole.ApplicantAttorney));
        rig.Stakeholders.Add(Party("   ", RecipientRole.Patient));

        await rig.Build().HandleEventAsync(ReminderEvent());

        Sends(rig.BookerCc).ShouldBeEmpty(
            "blank addresses are dropped, and with none left there is nobody to remind; the positive control "
            + "proves this fixture otherwise dispatches");
    }

    /// <summary>The To address is the primary recipient when one is set, else the booker.</summary>
    [Theory]
    [InlineData("TEST-primary@test.local", "TEST-primary@test.local")]
    [InlineData(null, "TEST-booker@test.local")]
    public async Task HandleEventAsync_AddressesThePrimaryRecipientElseTheBooker(string? primaryEmail, string expectedTo)
    {
        var rig = new Rig();
        rig.Context.PrimaryRecipientEmail = primaryEmail;

        await rig.Build().HandleEventAsync(ReminderEvent());

        SingleSend(rig.BookerCc).BookerEmail.ShouldBe(expectedTo);
    }

    /// <summary>
    /// When the booker is a promoted attorney-creator, the creator (often a firm or paralegal address
    /// that is not otherwise a party) is added to the CC list. Only then: not for other bookers, and not
    /// when the creator has no address.
    /// </summary>
    [Theory]
    [InlineData(true, "TEST-creator@test.local", true)]
    [InlineData(false, "TEST-creator@test.local", false)]
    [InlineData(true, "   ", false)]
    public async Task HandleEventAsync_AddsThePromotedCreatorToTheCcOnlyWhenPromotedWithAnAddress(
        bool isPromoted, string creatorEmail, bool expectCreatorAdded)
    {
        var rig = new Rig();
        rig.Context.IsPromoted = isPromoted;
        rig.Context.CreatorEmail = creatorEmail;

        await rig.Build().HandleEventAsync(ReminderEvent());

        var stakeholders = SingleSend(rig.BookerCc).Stakeholders;
        stakeholders.Count.ShouldBe(expectCreatorAdded ? 3 : 2);
        var creator = stakeholders.SingleOrDefault(r => r.Email == "TEST-creator@test.local");
        if (expectCreatorAdded)
        {
            creator.ShouldNotBeNull();
            creator.Role.ShouldBe(RecipientRole.OfficeAdmin);
            creator.IsRegistered.ShouldBeTrue();
        }
        else
        {
            creator.ShouldBeNull();
        }
    }

    [Theory]
    [InlineData(true, "10/02/2026")]
    [InlineData(false, "")]
    public async Task HandleEventAsync_FormatsTheDueDateAsMonthDayYearOrLeavesItEmpty(bool hasDueDate, string expected)
    {
        var rig = new Rig();
        rig.Context.DueDate = hasDueDate ? new DateTime(2026, 10, 2) : null;

        await rig.Build().HandleEventAsync(ReminderEvent());

        SingleSend(rig.BookerCc).Variables["DueDate"].ShouldBe(expected);
    }

    /// <summary>
    /// Only documents that need the BOOKER to act are listed: not-yet-uploaded ones by name, rejected
    /// ones with a re-upload note. Documents awaiting staff review are left out -- there is nothing for
    /// the booker to do.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_ListsNotUploadedAndRejectedDocumentsButNotThoseAwaitingReview()
    {
        var rig = new Rig();
        rig.MissingResolver.ResolveAsync(Arg.Any<Guid>()).Returns(Missing(
            ("TEST-Doc-Needed", RequiredDocumentState.NotUploaded),
            ("TEST-Doc-Bounced", RequiredDocumentState.Rejected),
            ("TEST-Doc-InReview", RequiredDocumentState.AwaitingReview)));

        await rig.Build().HandleEventAsync(ReminderEvent());

        var block = OutstandingBlock(SingleSend(rig.BookerCc));
        block.ShouldStartWith("<p><strong>Documents still needed:</strong></p>");
        block.ShouldContain("<li>TEST-Doc-Needed</li>");
        block.ShouldContain("<li>TEST-Doc-Bounced (rejected - please re-upload)</li>");
        block.ShouldNotContain("TEST-Doc-InReview");
    }

    [Fact]
    public async Task HandleEventAsync_OutstandingDocumentNamesAreHtmlEncoded()
    {
        var rig = new Rig();
        rig.MissingResolver.ResolveAsync(Arg.Any<Guid>()).Returns(Missing(
            ("TEST<Doc>&", RequiredDocumentState.NotUploaded),
            ("<b>TEST-Bounced</b>", RequiredDocumentState.Rejected)));

        await rig.Build().HandleEventAsync(ReminderEvent());

        var block = OutstandingBlock(SingleSend(rig.BookerCc));
        block.ShouldContain("<li>TEST&lt;Doc&gt;&amp;</li>");
        block.ShouldContain("<li>&lt;b&gt;TEST-Bounced&lt;/b&gt; (rejected - please re-upload)</li>");
        block.ShouldNotContain("<b>TEST-Bounced</b>");
    }

    /// <summary>
    /// The Joint Declaration Form is tracked outside the package model, so it is checked separately:
    /// it is listed only for an AME appointment with no non-rejected Joint Declaration on file.
    /// </summary>
    [Theory]
    [InlineData("ame-with-no-joint-declaration", true)]
    [InlineData("ame-with-a-live-joint-declaration", false)]
    [InlineData("ame-with-only-a-rejected-joint-declaration", true)]
    [InlineData("not-ame", false)]
    [InlineData("appointment-not-found", false)]
    public async Task HandleEventAsync_ListsTheJointDeclarationOnlyForAnAmeAppointmentWithoutOne(
        string scenario, bool expectListed)
    {
        var rig = new Rig();
        var evt = ReminderEvent();
        var ame = CaseEvaluationSeedIds.AppointmentTypes.Ame;
        switch (scenario)
        {
            case "ame-with-no-joint-declaration":
                rig.Appointment = NewAppointment(ame);
                break;
            case "ame-with-a-live-joint-declaration":
                rig.Appointment = NewAppointment(ame);
                rig.StoredDocuments.Add(JointDeclaration(evt.AppointmentId, DocumentStatus.Uploaded));
                break;
            case "ame-with-only-a-rejected-joint-declaration":
                rig.Appointment = NewAppointment(ame);
                rig.StoredDocuments.Add(JointDeclaration(evt.AppointmentId, DocumentStatus.Rejected));
                break;
            case "not-ame":
                rig.Appointment = NewAppointment(Guid.NewGuid());
                break;
            case "appointment-not-found":
                rig.Appointment = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "unknown scenario");
        }

        await rig.Build().HandleEventAsync(evt);

        var block = OutstandingBlock(SingleSend(rig.BookerCc));
        if (expectListed)
        {
            block.ShouldContain($"<li>{JointDeclarationItem}</li>");
        }
        else
        {
            block.ShouldBe(string.Empty, "with nothing else outstanding, no Joint Declaration item means no block at all");
        }
    }

    /// <summary>
    /// The greeting falls back from the promoted attorney's name, to the booker's, to the patient's, to
    /// a neutral "there" -- so the email never renders "Hello ,".
    /// </summary>
    [Theory]
    [InlineData("TEST-Greeting", "TEST-Booker Name", "TEST-First", "TEST-Last", "TEST-Greeting")]
    [InlineData(null, "TEST-Booker Name", "TEST-First", "TEST-Last", "TEST-Booker Name")]
    [InlineData(null, "", "TEST-First", "TEST-Last", "TEST-First TEST-Last")]
    [InlineData(null, "", null, null, "there")]
    public async Task HandleEventAsync_GreetingFallsBackFromAttorneyToBookerToPatientToThere(
        string? greetingName, string bookerFullName, string? patientFirst, string? patientLast, string expected)
    {
        var rig = new Rig();
        rig.Context.GreetingName = greetingName;
        rig.Context.BookerFullName = bookerFullName;
        rig.Context.PatientFirstName = patientFirst;
        rig.Context.PatientLastName = patientLast;

        await rig.Build().HandleEventAsync(ReminderEvent());

        SingleSend(rig.BookerCc).Variables["BookerFullName"].ShouldBe(expected);
    }

    [Fact]
    public async Task HandleEventAsync_TagsTheSendWithDaysUntilDueAndBlanksMissingUrlAndClinic()
    {
        var rig = new Rig();
        rig.Context.PortalBaseUrl = null;
        rig.CurrentTenant.Name.Returns((string?)null);
        var evt = ReminderEvent();

        await rig.Build().HandleEventAsync(evt);

        var sent = SingleSend(rig.BookerCc);
        sent.ContextTag.ShouldBe($"AppointmentReminder/T-7/{evt.AppointmentId}");
        sent.Variables["PortalUrl"].ShouldBe(string.Empty);
        sent.Variables["ClinicName"].ShouldBe(string.Empty);
        sent.Variables["AppointmentRequestConfirmationNumber"].ShouldBe("TEST-A0006");
    }
}
