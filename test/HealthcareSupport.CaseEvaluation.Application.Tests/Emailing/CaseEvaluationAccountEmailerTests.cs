using System;
using System.Threading.Tasks;
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
/// task_4c0f6fe9 (2026-07-21) -- unit tests for the host/tenant URL routing added to
/// <see cref="CaseEvaluationAccountEmailer"/>. Phase D made internal operators HOST
/// logins (null <c>TenantId</c>); the emailer must build a HOST reset/confirmation
/// URL for them (and must NOT throw, which the prior <c>EnsureUserHasTenant</c> guard
/// did) while keeping the tenant-scoped URL for external users.
///
/// <para>Only the routing is asserted. The template lookup is stubbed to miss, so
/// <c>DispatchAsync</c> logs + returns without enqueuing -- the URL builder is invoked
/// before that, so the branch (and the no-throw) is fully observed.</para>
/// </summary>
public class CaseEvaluationAccountEmailerTests
{
    [Fact]
    public async Task SendPasswordResetLinkAsync_HostUser_BuildsHostResetUrlAndDoesNotThrow()
    {
        var (sut, urls) = NewEmailer();
        var user = new IdentityUser(Guid.NewGuid(), "staff@gesco.com", "staff@gesco.com");

        await sut.SendPasswordResetLinkAsync(user, "reset-tok", "app");

        await urls.Received(1).BuildHostPasswordResetUrlAsync(user.Id, "reset-tok");
        await urls.DidNotReceive().BuildPasswordResetUrlAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SendPasswordResetLinkAsync_TenantUser_BuildsTenantResetUrl()
    {
        var (sut, urls) = NewEmailer();
        var tenantId = Guid.NewGuid();
        var user = new IdentityUser(Guid.NewGuid(), "ext@example.com", "ext@example.com", tenantId);

        await sut.SendPasswordResetLinkAsync(user, "reset-tok", "app");

        await urls.Received(1).BuildPasswordResetUrlAsync(tenantId, user.Id, "reset-tok");
        await urls.DidNotReceive().BuildHostPasswordResetUrlAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SendEmailConfirmationLinkAsync_HostUser_BuildsHostConfirmationUrlAndDoesNotThrow()
    {
        var (sut, urls) = NewEmailer();
        var user = new IdentityUser(Guid.NewGuid(), "staff@gesco.com", "staff@gesco.com");

        await sut.SendEmailConfirmationLinkAsync(user, "confirm-tok", "app");

        await urls.Received(1).BuildHostEmailConfirmationUrlAsync(user.Id, "confirm-tok");
        await urls.DidNotReceive().BuildEmailConfirmationUrlAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SendEmailConfirmationLinkAsync_TenantUser_BuildsTenantConfirmationUrl()
    {
        var (sut, urls) = NewEmailer();
        var tenantId = Guid.NewGuid();
        var user = new IdentityUser(Guid.NewGuid(), "ext@example.com", "ext@example.com", tenantId);

        await sut.SendEmailConfirmationLinkAsync(user, "confirm-tok", "app");

        await urls.Received(1).BuildEmailConfirmationUrlAsync(tenantId, user.Id, "confirm-tok");
        await urls.DidNotReceive().BuildHostEmailConfirmationUrlAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    private static (CaseEvaluationAccountEmailer Sut, IAccountUrlBuilder Urls) NewEmailer()
    {
        var templateRepository = Substitute.For<INotificationTemplateRepository>();
        var backgroundJobManager = Substitute.For<IBackgroundJobManager>();
        var currentTenant = Substitute.For<ICurrentTenant>();

        var urls = Substitute.For<IAccountUrlBuilder>();
        urls.BuildHostPasswordResetUrlAsync(Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(Task.FromResult("http://admin.host/Account/ResetPassword"));
        urls.BuildPasswordResetUrlAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(Task.FromResult("http://falkinstein.host/Account/ResetPassword"));
        urls.BuildHostEmailConfirmationUrlAsync(Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(Task.FromResult("http://admin.host/Account/EmailConfirmation"));
        urls.BuildEmailConfirmationUrlAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(Task.FromResult("http://falkinstein.host/Account/EmailConfirmation"));

        var sut = new CaseEvaluationAccountEmailer(
            templateRepository,
            backgroundJobManager,
            currentTenant,
            urls,
            NullLogger<CaseEvaluationAccountEmailer>.Instance);
        return (sut, urls);
    }
}
