using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
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
/// Unit coverage for <see cref="DocumentRejectedEmailHandler"/>: the single To+CC email sent when
/// office staff reject an uploaded document.
///
/// <para><b>WHAT IS PINNED.</b> Who the email goes to (To = the uploader, CC = every other party
/// EXCEPT the office mailbox, because the office performed the reject), which of the three
/// OLD-parity templates it uses, and the package-document branch that swaps in the
/// "remaining documents" template while required documents are still outstanding.</para>
///
/// <para><b>WHY THE POSITIVE CONTROL IS NOT OPTIONAL.</b> Every collaborator is a substitute, and an
/// unconfigured one makes the handler bail out early -- which ALSO dispatches nothing. So a
/// "dispatches nothing" Fact proves its guard only because
/// <see cref="HandleEventAsync_PackageDocumentWithNothingMissing_EmailsTheUploader_PositiveControl"/>
/// shows this same fixture DOES reach the dispatcher. If the control fails, treat every negative
/// Fact in this file as vacuous. Same rule as <c>PatientPacketEmailKindGateTests</c>.</para>
///
/// <para><b>NO MAIL CAN LEAVE.</b> <see cref="INotificationDispatcher"/> is fully substituted, so no
/// template renders and nothing is enqueued. <c>[UnitOfWork]</c> on the handler is inert here: it is
/// built with <c>new</c>, so no interceptor wraps it. No database is touched.</para>
///
/// <para><b>NOT PINNED, deliberately.</b> Whether document names in the remaining-documents list are
/// HTML-encoded (issue #1010): the Facts use plain names, which render identically either way.</para>
///
/// <para>Synthetic data only (HIPAA).</para>
/// </summary>
public class DocumentRejectedEmailHandlerTests
{
    private const string UploaderEmail = "TEST-uploader@test.local";
    private const string AuthRoot = "https://auth.test.local";
    private const string PortalRoot = "https://portal.test.local/";
    private const string ClinicName = "TEST-clinic";

    /// <summary>
    /// One configured instance of every collaborator. The defaults describe the HAPPY path -- a
    /// package document, an uploader email, two other parties, nothing missing -- so each Fact
    /// changes only the one input it is about.
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

        /// <summary>Five nulls, same reason: <c>ResolveAsync</c> is virtual and configured.</summary>
        public MissingRequiredDocumentsResolver MissingResolver { get; } =
            Substitute.For<MissingRequiredDocumentsResolver>(null, null, null, null, null);

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
            // Lambdas, not values, so a Fact can edit Context or Parties after construction.
            ContextResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns(_ => Context);
            ContextResolver.ResolveUploaderEmailAsync(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(UploaderEmail);
            RecipientResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>()).Returns(_ => Parties);
            // A sealed record is not auto-substituted: unconfigured, this would return null and throw.
            MissingResolver.ResolveAsync(Arg.Any<Guid>()).Returns(MissingRequiredDocumentsResult.Empty);
            CurrentTenant.Name.Returns(ClinicName);
            UrlBuilder.BuildAuthServerRootUrlAsync(Arg.Any<Guid?>()).Returns(AuthRoot);
            UrlBuilder.BuildPortalRootUrlAsync(Arg.Any<Guid?>()).Returns(PortalRoot);
        }

        public DocumentRejectedEmailHandler Build() => new(
            Dispatcher,
            ContextResolver,
            RecipientResolver,
            MissingResolver,
            CurrentTenant,
            NullLogger<DocumentRejectedEmailHandler>.Instance,
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
        RequestConfirmationNumber = "TEST-A0001",
        AppointmentDate = new DateTime(2026, 10, 5, 9, 30, 0),
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

    private static AppointmentDocumentRejectedEto RejectedEvent() => new()
    {
        AppointmentId = Guid.NewGuid(),
        AppointmentDocumentId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        RejectedByUserId = Guid.NewGuid(),
        RejectionNotes = "TEST-notes: the scan is unreadable",
        OccurredAt = new DateTime(2026, 9, 23, 17, 0, 0, DateTimeKind.Utc),
    };

    /// <summary>
    /// Reads the ONE <c>DispatchToWithCcAsync</c> call. Asserting the count first gives a readable
    /// failure ("expected 1 but was 0") instead of a bare "sequence contains no elements".
    /// </summary>
    private static SentEmail SingleSend(INotificationDispatcher dispatcher)
    {
        var calls = dispatcher.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(INotificationDispatcher.DispatchToWithCcAsync))
            .ToList();
        calls.Count.ShouldBe(1, "exactly one To+CC email is expected for one rejected document");
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
    public async Task HandleEventAsync_PackageDocumentWithNothingMissing_EmailsTheUploader_PositiveControl()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(RejectedEvent());

        var sent = SingleSend(rig.Dispatcher);
        sent.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.PatientDocumentRejected);
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

        await rig.Build().HandleEventAsync(RejectedEvent());

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

        await rig.Build().HandleEventAsync(RejectedEvent());

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty(
            "the uploader is the To address; with none resolved the email must not be sent to the CC list alone");
    }

    /// <summary>
    /// The fallback handed to the uploader lookup is the patient's email, else the booker's -- the
    /// address used when the upload was anonymous (a verification-code upload has no user id).
    /// </summary>
    [Theory]
    [InlineData("TEST-patient@test.local", "TEST-patient@test.local")]
    [InlineData(null, "TEST-booker@test.local")]
    public async Task HandleEventAsync_UploaderLookupFallsBackToPatientThenBookerEmail(
        string? patientEmail, string expectedFallback)
    {
        var rig = new Rig();
        rig.Context.PatientEmail = patientEmail;

        await rig.Build().HandleEventAsync(RejectedEvent());

        await rig.ContextResolver.Received(1).ResolveUploaderEmailAsync(
            rig.Context.DocumentUploadedByUserId, expectedFallback);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleEventAsync_ToIsRegisteredOnlyWhenTheDocumentHasAnUploaderUserId(bool hasUploaderUserId)
    {
        var rig = new Rig();
        rig.Context.DocumentUploadedByUserId = hasUploaderUserId ? Guid.NewGuid() : null;

        await rig.Build().HandleEventAsync(RejectedEvent());

        SingleSend(rig.Dispatcher).To.IsRegistered.ShouldBe(hasUploaderUserId);
    }

    /// <summary>
    /// The office performed the reject, so it is left OFF the CC (contrast the upload email, which
    /// keeps it). A party with no role is still copied, as a Patient-role recipient.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_CcDropsTheOfficeMailboxAndKeepsEveryOtherParty()
    {
        var rig = new Rig();
        rig.Parties.Clear();
        rig.Parties.Add(Party("TEST-office@test.local", RecipientRole.OfficeAdmin));
        rig.Parties.Add(Party("TEST-applicant-attorney@test.local", RecipientRole.ApplicantAttorney));
        rig.Parties.Add(Party("TEST-no-role@test.local", role: null));
        var evt = RejectedEvent();

        await rig.Build().HandleEventAsync(evt);

        var cc = SingleSend(rig.Dispatcher).Cc;
        cc.Select(r => r.Email).ShouldBe(
            new[] { "TEST-applicant-attorney@test.local", "TEST-no-role@test.local" },
            ignoreOrder: true);
        cc.Single(r => r.Email == "TEST-no-role@test.local").Role.ShouldBe(RecipientRole.Patient);
        await rig.RecipientResolver.Received(1).ResolveAsync(evt.AppointmentId, NotificationKind.DocumentRejected);
    }

    [Theory]
    [InlineData(false, false, NotificationTemplateConsts.Codes.PatientDocumentRejected)]
    [InlineData(true, false, NotificationTemplateConsts.Codes.PatientNewDocumentRejected)]
    [InlineData(false, true, NotificationTemplateConsts.Codes.JointAgreementLetterRejected)]
    public async Task HandleEventAsync_PicksTheTemplateByAdHocAndJointDeclaration(
        bool isAdHoc, bool isJointDeclaration, string expectedTemplate)
    {
        var rig = new Rig();
        rig.Context.IsAdHoc = isAdHoc;
        rig.Context.IsJointDeclaration = isJointDeclaration;

        await rig.Build().HandleEventAsync(RejectedEvent());

        SingleSend(rig.Dispatcher).TemplateCode.ShouldBe(expectedTemplate);
    }

    /// <summary>
    /// A package document rejected while required documents are still outstanding switches to the
    /// RemainingDocs template and carries the count, the list and a link to the appointment. The
    /// portal root's trailing slash is trimmed so the link has no double slash.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_PackageDocumentWithDocumentsStillMissing_UsesTheRemainingDocsTemplate()
    {
        var rig = new Rig();
        rig.MissingResolver.ResolveAsync(Arg.Any<Guid>()).Returns(new MissingRequiredDocumentsResult(
            RequiredCount: 3,
            Missing: new[]
            {
                new MissingRequiredDocument(Guid.NewGuid(), "TEST-Doc-One", RequiredDocumentState.NotUploaded),
                new MissingRequiredDocument(Guid.NewGuid(), "TEST-Doc-Two", RequiredDocumentState.Rejected),
            }));
        var evt = RejectedEvent();

        await rig.Build().HandleEventAsync(evt);

        var sent = SingleSend(rig.Dispatcher);
        sent.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.PatientDocumentRejectedRemainingDocs);
        sent.Variables["RemainingDocumentCount"].ShouldBe(2);
        sent.Variables["RemainingDocumentList"].ShouldBe("<li>TEST-Doc-One</li><li>TEST-Doc-Two</li>");
        sent.Variables["URL"].ShouldBe($"https://portal.test.local/appointments/view/{evt.AppointmentId:N}");
    }

    [Fact]
    public async Task HandleEventAsync_PackageDocumentWithNothingMissing_KeepsTheBaseTemplateAndAddsNoRemainingVariables()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(RejectedEvent());

        await rig.MissingResolver.Received(1).ResolveAsync(Arg.Any<Guid>());
        var sent = SingleSend(rig.Dispatcher);
        sent.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.PatientDocumentRejected);
        sent.Variables.ContainsKey("RemainingDocumentCount").ShouldBeFalse();
    }

    /// <summary>
    /// Ad-hoc and Joint Declaration documents have no package queue, so the missing-documents
    /// lookup must not run for them. The send is asserted too, so the Fact cannot pass by the
    /// handler bailing out before the branch.
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task HandleEventAsync_AdHocOrJointDeclaration_NeverConsultsTheMissingDocumentsResolver(
        bool isAdHoc, bool isJointDeclaration)
    {
        var rig = new Rig();
        rig.Context.IsAdHoc = isAdHoc;
        rig.Context.IsJointDeclaration = isJointDeclaration;

        await rig.Build().HandleEventAsync(RejectedEvent());

        await rig.MissingResolver.DidNotReceive().ResolveAsync(Arg.Any<Guid>());
        SingleSend(rig.Dispatcher);
    }

    /// <summary>
    /// The login link's tenant hint comes from the first party; with no other party at all it falls
    /// back to the current tenant's name -- and the uploader is STILL emailed, because there is no
    /// party-count guard on this handler.
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

        await rig.Build().HandleEventAsync(RejectedEvent());

        var sent = SingleSend(rig.Dispatcher);
        sent.Variables["LoginUrl"].ShouldBe(expectedLoginUrl);
        sent.To.Email.ShouldBe(UploaderEmail);
        sent.Cc.Count.ShouldBe(hasOtherParties ? 2 : 0);
    }

    [Fact]
    public async Task HandleEventAsync_CarriesTheUploaderAndDocumentLabelAndTagsTheSend()
    {
        var rig = new Rig();
        var evt = RejectedEvent();

        await rig.Build().HandleEventAsync(evt);

        var sent = SingleSend(rig.Dispatcher);
        sent.Variables["UploaderFullName"].ShouldBe("TEST-Uploader Name");
        sent.Variables["DocumentLabel"].ShouldBe("TEST-label");
        sent.ContextTag.ShouldBe(
            $"DocumentRejected/{NotificationTemplateConsts.Codes.PatientDocumentRejected}/{evt.AppointmentDocumentId}");
    }

    /// <summary>
    /// The staff member's rejection notes reach the email verbatim -- they are the only place the
    /// uploader learns WHY the document was rejected.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_PassesTheStaffRejectionNotesThroughToTheEmail()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(RejectedEvent());

        SingleSend(rig.Dispatcher).Variables["RejectionNotes"].ShouldBe("TEST-notes: the scan is unreadable");
    }
}
