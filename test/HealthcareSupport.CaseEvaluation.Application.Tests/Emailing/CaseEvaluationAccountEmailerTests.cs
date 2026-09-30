using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Emailing;

/// <summary>
/// task_4c0f6fe9 (2026-07-21) -- host/tenant URL routing for
/// <see cref="CaseEvaluationAccountEmailer"/>. Phase D made internal operators HOST logins (null
/// <c>TenantId</c>); the emailer must produce a HOST reset/confirmation URL for them (and must NOT
/// throw, which the prior <c>EnsureUserHasTenant</c> guard did) while keeping the tenant-scoped URL
/// for external users.
///
/// <para><b>Item C (2026-08-22) moved the branch.</b> The host-vs-tenant ternary now lives inside
/// <see cref="IAccountUrlBuilder"/>, because duplicating it per call site is exactly how
/// <c>ExternalAccountAppService</c> was missed when Phase D landed. So these tests now assert what
/// this class is actually responsible for: passing the user's nullable tenant id through, unchanged
/// and un-second-guessed. The BRANCH itself -- null tenant to the admin subdomain, set tenant to the
/// office one -- is asserted in <c>AccountUrlBuilderTests</c>.</para>
///
/// <para>The template lookup is stubbed to miss, so <c>DispatchAsync</c> logs and returns without
/// enqueuing. The URL builder is invoked before that, so the call (and the no-throw) is still fully
/// observed.</para>
/// </summary>
public class CaseEvaluationAccountEmailerTests
{
    [Fact]
    public async Task SendPasswordResetLinkAsync_HostUser_PassesANullTenantAndDoesNotThrow()
    {
        var (sut, urls) = NewEmailer();
        var user = new IdentityUser(Guid.NewGuid(), "staff@example.test", "staff@example.test");

        await sut.SendPasswordResetLinkAsync(user, "reset-tok", "app");

        await urls.Received(1).BuildPasswordResetUrlForUserAsync((Guid?)null, user.Id, "reset-tok");
    }

    [Fact]
    public async Task SendPasswordResetLinkAsync_TenantUser_PassesThatTenant()
    {
        var (sut, urls) = NewEmailer();
        var tenantId = Guid.NewGuid();
        var user = new IdentityUser(Guid.NewGuid(), "ext@example.test", "ext@example.test", tenantId);

        await sut.SendPasswordResetLinkAsync(user, "reset-tok", "app");

        await urls.Received(1).BuildPasswordResetUrlForUserAsync(tenantId, user.Id, "reset-tok");
    }

    [Fact]
    public async Task SendEmailConfirmationLinkAsync_HostUser_PassesANullTenantAndDoesNotThrow()
    {
        var (sut, urls) = NewEmailer();
        var user = new IdentityUser(Guid.NewGuid(), "staff@example.test", "staff@example.test");

        await sut.SendEmailConfirmationLinkAsync(user, "confirm-tok", "app");

        await urls.Received(1)
            .BuildEmailConfirmationUrlForUserAsync((Guid?)null, user.Id, "confirm-tok");
    }

    [Fact]
    public async Task SendEmailConfirmationLinkAsync_TenantUser_PassesThatTenant()
    {
        var (sut, urls) = NewEmailer();
        var tenantId = Guid.NewGuid();
        var user = new IdentityUser(Guid.NewGuid(), "ext@example.test", "ext@example.test", tenantId);

        await sut.SendEmailConfirmationLinkAsync(user, "confirm-tok", "app");

        await urls.Received(1)
            .BuildEmailConfirmationUrlForUserAsync(tenantId, user.Id, "confirm-tok");
    }

    [Fact]
    public async Task SendPasswordResetLinkAsync_DoesNotBranchOnHostVersusTenantItself()
    {
        // Guards the item C refactor from being quietly undone. If someone reinstates a local ternary
        // here, one of the explicit overloads gets called again and this fails -- which is the whole
        // point of moving the decision into the builder.
        var (sut, urls) = NewEmailer();
        var user = new IdentityUser(Guid.NewGuid(), "staff@example.test", "staff@example.test");

        await sut.SendPasswordResetLinkAsync(user, "reset-tok", "app");
        await sut.SendEmailConfirmationLinkAsync(user, "confirm-tok", "app");

        await urls.DidNotReceive().BuildHostPasswordResetUrlAsync(Arg.Any<Guid>(), Arg.Any<string>());
        await urls.DidNotReceive()
            .BuildPasswordResetUrlAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>());
        await urls.DidNotReceive()
            .BuildHostEmailConfirmationUrlAsync(Arg.Any<Guid>(), Arg.Any<string>());
        await urls.DidNotReceive()
            .BuildEmailConfirmationUrlAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    /// <summary>
    /// Phase 4.1 (2026-09-09) -- CodeQL alert 211. The context tag must not carry a full email
    /// address, because it reaches a <c>LogWarning</c> on the template-missing branch, past the
    /// empty-recipient guard.
    ///
    /// <para><b>This asserts the ENQUEUED JOB, not the log, and that is a deliberate proxy.</b>
    /// The existing fixture builds the emailer with <c>NullLogger.Instance</c>, <c>LogWarning</c>
    /// is an extension method that NSubstitute cannot intercept, and no test in this repository
    /// asserts on logger output -- there is no pattern to mirror and inventing one for a single
    /// assertion is not worth it.</para>
    ///
    /// <para><b>Why the proxy is sound rather than convenient:</b> both sinks read the SAME local,
    /// built once at the call site. If the value reaching <c>Context</c> is masked, the value
    /// reaching the log is the same masked string. The proxy would only mislead if the tag were
    /// transformed between the two sinks, and it is not -- it is passed through
    /// <c>DispatchAsync</c> untouched.</para>
    ///
    /// <para>Note this test needs an ACTIVE template, unlike its neighbours: the others stub the
    /// lookup to miss so <c>DispatchAsync</c> returns early, which is exactly the path that never
    /// reaches the enqueue.</para>
    /// </summary>
    [Fact]
    public async Task SendEmailConfirmationCodeAsync_MasksTheAddressInTheContextTag()
    {
        var (sut, jobs) = NewEmailerWithActiveTemplate();

        await sut.SendEmailConfirmationCodeAsync("adrian@example.test", "123456");

        await jobs.Received(1).EnqueueAsync(
            Arg.Is<SendAppointmentEmailArgs>(a =>
                a.Context == "AccountEmailer/ConfirmationCode/a***@example.test"),
            Arg.Any<BackgroundJobPriority>(),
            Arg.Any<TimeSpan?>());
    }

    /// <summary>
    /// The property that matters, asserted independently of the exact masked shape so a future
    /// formatting change cannot quietly reintroduce the identifier.
    /// </summary>
    [Fact]
    public async Task SendEmailConfirmationCodeAsync_NeverPutsTheLocalPartInTheContextTag()
    {
        var (sut, jobs) = NewEmailerWithActiveTemplate();

        await sut.SendEmailConfirmationCodeAsync("averyspecificlocalpart@example.test", "123456");

        await jobs.Received(1).EnqueueAsync(
            Arg.Is<SendAppointmentEmailArgs>(a =>
                a.Context != null && !a.Context.Contains("averyspecificlocalpart")),
            Arg.Any<BackgroundJobPriority>(),
            Arg.Any<TimeSpan?>());
    }

    private static (CaseEvaluationAccountEmailer Sut, IBackgroundJobManager Jobs)
        NewEmailerWithActiveTemplate()
    {
        var templateRepository = Substitute.For<INotificationTemplateRepository>();
        templateRepository
            .FindByCodeAsync(Arg.Any<string>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(new NotificationTemplate(
                Guid.NewGuid(),
                null,
                NotificationTemplateConsts.Codes.UserRegistered,
                Guid.NewGuid(),
                "Subject",
                "Body",
                "Sms"));

        var backgroundJobManager = Substitute.For<IBackgroundJobManager>();
        var currentTenant = Substitute.For<ICurrentTenant>();
        var urls = Substitute.For<IAccountUrlBuilder>();

        var sut = new CaseEvaluationAccountEmailer(
            templateRepository,
            backgroundJobManager,
            currentTenant,
            urls,
            NullLogger<CaseEvaluationAccountEmailer>.Instance);
        return (sut, backgroundJobManager);
    }

    private static (CaseEvaluationAccountEmailer Sut, IAccountUrlBuilder Urls) NewEmailer()
    {
        var templateRepository = Substitute.For<INotificationTemplateRepository>();
        var backgroundJobManager = Substitute.For<IBackgroundJobManager>();
        var currentTenant = Substitute.For<ICurrentTenant>();

        var urls = Substitute.For<IAccountUrlBuilder>();
        urls.BuildPasswordResetUrlForUserAsync(Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(Task.FromResult("http://admin.host/Account/ResetPassword"));
        urls.BuildEmailConfirmationUrlForUserAsync(Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(Task.FromResult("http://admin.host/Account/EmailConfirmation"));

        var sut = new CaseEvaluationAccountEmailer(
            templateRepository,
            backgroundJobManager,
            currentTenant,
            urls,
            NullLogger<CaseEvaluationAccountEmailer>.Instance);
        return (sut, urls);
    }
}
