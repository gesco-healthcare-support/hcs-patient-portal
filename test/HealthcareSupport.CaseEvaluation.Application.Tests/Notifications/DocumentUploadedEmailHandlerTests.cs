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
/// Unit coverage for <see cref="DocumentUploadedEmailHandler"/>: the single To+CC email sent when a
/// document is uploaded to an appointment.
///
/// <para><b>WHAT IS PINNED, and where it differs from the accept/reject emails.</b> To = the
/// uploader; CC = every other party WITH the office mailbox kept (staff want a copy of each new
/// upload -- the accept and reject emails drop it). The uploader's identity comes from the EVENT
/// first and the stored document second, and "registered" follows the EVENT alone. Template
/// routing is the same three-way (package / ad-hoc / Joint Declaration) split.</para>
///
/// <para><b>WHY THE POSITIVE CONTROL IS NOT OPTIONAL.</b> An unconfigured substitute makes the
/// handler bail out early, which also dispatches nothing. Every "dispatches nothing" Fact means
/// something only because
/// <see cref="HandleEventAsync_PackageDocument_EmailsTheUploader_PositiveControl"/> shows this same
/// fixture reaches the dispatcher. Same rule as <c>PatientPacketEmailKindGateTests</c>.</para>
///
/// <para><b>NO MAIL CAN LEAVE.</b> <see cref="INotificationDispatcher"/> is fully substituted and the
/// handler is built with <c>new</c> (its <c>[UnitOfWork]</c> is inert). No database is touched.</para>
///
/// <para>Synthetic data only (HIPAA).</para>
/// </summary>
public class DocumentUploadedEmailHandlerTests
{
    private const string UploaderEmail = "TEST-uploader@test.local";
    private const string AuthRoot = "https://auth.test.local";
    private const string ClinicName = "TEST-clinic";

    /// <summary>
    /// Every collaborator, configured for the HAPPY path -- a package document, an uploader email,
    /// two other parties -- so each Fact changes only the input it is about.
    /// </summary>
    private sealed class Rig
    {
        public INotificationDispatcher Dispatcher { get; } = Substitute.For<INotificationDispatcher>();

        /// <summary>
        /// The ten nulls are never dereferenced: both resolver methods the handler calls are
        /// <c>virtual</c> and are configured below, so the real bodies never run.
        /// </summary>
        public DocumentEmailContextResolver ContextResolver { get; } =
            Substitute.For<DocumentEmailContextResolver>(null, null, null, null, null, null, null, null, null, null);

        public IAppointmentRecipientResolver RecipientResolver { get; } = Substitute.For<IAppointmentRecipientResolver>();
        public ICurrentTenant CurrentTenant { get; } = Substitute.For<ICurrentTenant>();
        public IAccountUrlBuilder UrlBuilder { get; } = Substitute.For<IAccountUrlBuilder>();
        public DocumentEmailContext Context { get; } = SampleContext();

        public List<SendAppointmentEmailArgs> Parties { get; } = new()
        {
            Party("TEST-applicant-attorney@test.local", RecipientRole.ApplicantAttorney, "TEST-tenant-from-party"),
            Party("TEST-defense-attorney@test.local", RecipientRole.DefenseAttorney, "TEST-tenant-from-party"),
        };

        public Rig()
        {
            ContextResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns(_ => Context);
            ContextResolver.ResolveUploaderEmailAsync(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(UploaderEmail);
            RecipientResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>()).Returns(_ => Parties);
            CurrentTenant.Name.Returns(ClinicName);
            UrlBuilder.BuildAuthServerRootUrlAsync(Arg.Any<Guid?>()).Returns(AuthRoot);
        }

        public DocumentUploadedEmailHandler Build() => new(
            Dispatcher,
            ContextResolver,
            RecipientResolver,
            CurrentTenant,
            NullLogger<DocumentUploadedEmailHandler>.Instance,
            UrlBuilder);
    }

    /// <summary>The arguments of one <c>DispatchToWithCcAsync</c> call, read back from the substitute.</summary>
    private sealed record SentEmail(
        string TemplateCode,
        NotificationRecipient To,
        IReadOnlyCollection<NotificationRecipient> Cc,
        IReadOnlyDictionary<string, object?> Variables,
        string ContextTag);

    private static DocumentEmailContext SampleContext() => new()
    {
        AppointmentId = Guid.NewGuid(),
        RequestConfirmationNumber = "TEST-A0002",
        AppointmentDate = new DateTime(2026, 10, 6, 14, 0, 0),
        PatientFirstName = "TEST-First",
        PatientLastName = "TEST-Last",
        PatientEmail = "TEST-patient@test.local",
        BookerEmail = "TEST-booker@test.local",
        DocumentName = "TEST-document.pdf",
        DocumentLabel = "TEST-label",
        UploaderFullName = "TEST-Uploader Name",
        DocumentUploadedByUserId = Guid.NewGuid(),
        PortalBaseUrl = "https://tenant.portal.test.local",
        IsAdHoc = false,
        IsJointDeclaration = false,
    };

    private static SendAppointmentEmailArgs Party(string email, RecipientRole? role, string? tenantName = null) => new()
    {
        To = email,
        Role = role,
        IsRegistered = true,
        TenantName = tenantName,
    };

    private static AppointmentDocumentUploadedEto UploadedEvent(Guid? uploadedByUserId = null) => new()
    {
        AppointmentId = Guid.NewGuid(),
        AppointmentDocumentId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        UploadedByUserId = uploadedByUserId,
        OccurredAt = new DateTime(2026, 9, 23, 17, 0, 0, DateTimeKind.Utc),
    };

    /// <summary>
    /// Reads the ONE <c>DispatchToWithCcAsync</c> call, asserting the count first for a readable
    /// failure message.
    /// </summary>
    private static SentEmail SingleSend(INotificationDispatcher dispatcher)
    {
        var calls = dispatcher.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(INotificationDispatcher.DispatchToWithCcAsync))
            .ToList();
        calls.Count.ShouldBe(1, "exactly one To+CC email is expected for one uploaded document");
        var args = calls[0].GetArguments();
        return new SentEmail(
            (string)args[0]!,
            (NotificationRecipient)args[1]!,
            (IReadOnlyCollection<NotificationRecipient>)args[2]!,
            (IReadOnlyDictionary<string, object?>)args[3]!,
            (string)args[4]!);
    }

    /// <summary>
    /// <b>POSITIVE CONTROL -- load-bearing.</b> Proves this fixture reaches the dispatcher at all.
    /// Every "dispatches nothing" Fact below depends on it.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_PackageDocument_EmailsTheUploader_PositiveControl()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(UploadedEvent(Guid.NewGuid()));

        var sent = SingleSend(rig.Dispatcher);
        sent.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.PatientDocumentUploaded);
        sent.To.Email.ShouldBe(UploaderEmail);
    }

    [Fact]
    public async Task HandleEventAsync_NullEvent_DispatchesNothing()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(null!);

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty(
            "a null event must be ignored; the positive control proves this fixture otherwise dispatches");
    }

    [Fact]
    public async Task HandleEventAsync_AppointmentNotFound_DispatchesNothing()
    {
        var rig = new Rig();
        rig.ContextResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns((DocumentEmailContext?)null);

        await rig.Build().HandleEventAsync(UploadedEvent(Guid.NewGuid()));

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty(
            "with no appointment context there is nobody to address; the handler must skip, not throw");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleEventAsync_NoUploaderEmailResolved_DispatchesNothing(string? uploaderEmail)
    {
        var rig = new Rig();
        rig.ContextResolver.ResolveUploaderEmailAsync(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(uploaderEmail);

        await rig.Build().HandleEventAsync(UploadedEvent(Guid.NewGuid()));

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty(
            "the uploader is the To address; with none resolved the email must not be sent to the CC list alone");
    }

    /// <summary>
    /// The uploader looked up is the EVENT's user when it carries one, else the stored document's.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleEventAsync_UploaderLookupPrefersTheEventsUserOverTheDocuments(bool eventCarriesUploader)
    {
        var rig = new Rig();
        var eventUserId = Guid.NewGuid();
        var documentUserId = rig.Context.DocumentUploadedByUserId;

        await rig.Build().HandleEventAsync(UploadedEvent(eventCarriesUploader ? eventUserId : null));

        await rig.ContextResolver.Received(1).ResolveUploaderEmailAsync(
            eventCarriesUploader ? eventUserId : documentUserId,
            "TEST-patient@test.local");
    }

    /// <summary>
    /// "Registered" follows the EVENT alone. The stored document has an uploader in both rows, so the
    /// second row -- an anonymous upload event -- is what separates this handler from accept/reject,
    /// which read the stored document instead.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleEventAsync_ToIsRegisteredOnlyWhenTheEventCarriesAnUploader(bool eventCarriesUploader)
    {
        var rig = new Rig();
        rig.Context.DocumentUploadedByUserId = Guid.NewGuid();

        await rig.Build().HandleEventAsync(UploadedEvent(eventCarriesUploader ? Guid.NewGuid() : null));

        SingleSend(rig.Dispatcher).To.IsRegistered.ShouldBe(eventCarriesUploader);
    }

    /// <summary>
    /// Staff want a copy of every new upload, so the office mailbox stays ON the CC here -- the
    /// opposite of the accept and reject emails. A party with no role is copied as Patient.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_CcKeepsTheOfficeMailboxAndEveryOtherParty()
    {
        var rig = new Rig();
        rig.Parties.Clear();
        rig.Parties.Add(Party("TEST-office@test.local", RecipientRole.OfficeAdmin));
        rig.Parties.Add(Party("TEST-applicant-attorney@test.local", RecipientRole.ApplicantAttorney));
        rig.Parties.Add(Party("TEST-no-role@test.local", role: null));
        var evt = UploadedEvent(Guid.NewGuid());

        await rig.Build().HandleEventAsync(evt);

        var cc = SingleSend(rig.Dispatcher).Cc;
        cc.Select(r => r.Email).ShouldBe(
            new[] { "TEST-office@test.local", "TEST-applicant-attorney@test.local", "TEST-no-role@test.local" },
            ignoreOrder: true);
        cc.Single(r => r.Email == "TEST-office@test.local").Role.ShouldBe(RecipientRole.OfficeAdmin);
        cc.Single(r => r.Email == "TEST-no-role@test.local").Role.ShouldBe(RecipientRole.Patient);
        await rig.RecipientResolver.Received(1).ResolveAsync(evt.AppointmentId, NotificationKind.DocumentUploaded);
    }

    [Theory]
    [InlineData(false, false, NotificationTemplateConsts.Codes.PatientDocumentUploaded)]
    [InlineData(true, false, NotificationTemplateConsts.Codes.PatientNewDocumentUploaded)]
    [InlineData(false, true, NotificationTemplateConsts.Codes.JointAgreementLetterUploaded)]
    public async Task HandleEventAsync_PicksTheTemplateByAdHocAndJointDeclaration(
        bool isAdHoc, bool isJointDeclaration, string expectedTemplate)
    {
        var rig = new Rig();
        rig.Context.IsAdHoc = isAdHoc;
        rig.Context.IsJointDeclaration = isJointDeclaration;

        await rig.Build().HandleEventAsync(UploadedEvent(Guid.NewGuid()));

        SingleSend(rig.Dispatcher).TemplateCode.ShouldBe(expectedTemplate);
    }

    /// <summary>
    /// The login link's tenant hint comes from the first party, else the current tenant's name; with
    /// no other party the uploader is still emailed (there is no party-count guard).
    /// </summary>
    [Theory]
    [InlineData(true, "https://auth.test.local/Account/Login?__tenant=TEST-tenant-from-party")]
    [InlineData(false, "https://auth.test.local/Account/Login?__tenant=TEST-clinic")]
    public async Task HandleEventAsync_LoginUrlTenantHintComesFromTheFirstPartyElseTheCurrentTenant(
        bool hasOtherParties, string expectedLoginUrl)
    {
        var rig = new Rig();
        if (!hasOtherParties)
        {
            rig.Parties.Clear();
        }

        await rig.Build().HandleEventAsync(UploadedEvent(Guid.NewGuid()));

        var sent = SingleSend(rig.Dispatcher);
        sent.Variables["LoginUrl"].ShouldBe(expectedLoginUrl);
        sent.To.Email.ShouldBe(UploaderEmail);
        sent.Cc.Count.ShouldBe(hasOtherParties ? 2 : 0);
    }

    [Fact]
    public async Task HandleEventAsync_CarriesTheUploaderAndDocumentLabelAndTagsTheSend()
    {
        var rig = new Rig();
        var evt = UploadedEvent(Guid.NewGuid());

        await rig.Build().HandleEventAsync(evt);

        var sent = SingleSend(rig.Dispatcher);
        sent.Variables["UploaderFullName"].ShouldBe("TEST-Uploader Name");
        sent.Variables["DocumentLabel"].ShouldBe("TEST-label");
        sent.ContextTag.ShouldBe(
            $"DocumentUploaded/{NotificationTemplateConsts.Codes.PatientDocumentUploaded}/{evt.AppointmentDocumentId}");
    }
}
