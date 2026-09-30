using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Account.Emailing;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Emailing;

/// <summary>
/// The code-carrying sends of <see cref="CaseEvaluationAccountEmailer"/>: the security code and the
/// email-confirmation code, and the skip when there is nobody to send to.
/// </summary>
/// <remarks>
/// The background job manager is replaced for THIS class only, in <c>AfterAddApplication</c>, so
/// the queued email is asserted and never sent. Host context, where the host-scoped
/// <c>UserRegistered</c> template is seeded. Addresses are synthetic.
/// </remarks>
public abstract class AccountEmailerQueueTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private IBackgroundJobManager _jobs = null!;

    private readonly IAccountEmailer _emailer;

    protected AccountEmailerQueueTests()
    {
        _emailer = GetRequiredService<IAccountEmailer>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        _jobs = Substitute.For<IBackgroundJobManager>();
        services.Replace(ServiceDescriptor.Singleton(typeof(IBackgroundJobManager), _jobs));
    }

    private SendAppointmentEmailArgs[] Queued() =>
        _jobs.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IBackgroundJobManager.EnqueueAsync))
            .Select(c => c.GetArguments()[0])
            .OfType<SendAppointmentEmailArgs>()
            .ToArray();

    [Fact]
    public async Task A_confirmation_code_is_queued_to_the_address_and_the_context_masks_it()
    {
        await WithUnitOfWorkAsync(() => _emailer.SendEmailConfirmationCodeAsync("code.recipient@example.test", "SYN123"));

        var queued = Queued().ShouldHaveSingleItem();
        queued.To.ShouldBe("code.recipient@example.test");
        queued.Context.ShouldStartWith("AccountEmailer/ConfirmationCode/");
        queued.Context.ShouldNotContain("code.recipient@example.test");
        queued.IsRegistered.ShouldBeTrue();
    }

    [Fact]
    public async Task A_security_code_is_queued_to_the_users_address()
    {
        var user = new IdentityUser(Guid.NewGuid(), "sec-code-user", "sec.code@example.test") { Name = "Synthetic" };

        await WithUnitOfWorkAsync(() => _emailer.SendEmailSecurityCodeAsync(user, "SYN456"));

        var queued = Queued().ShouldHaveSingleItem();
        queued.To.ShouldBe("sec.code@example.test");
        queued.Context.ShouldBe($"AccountEmailer/SecurityCode/{user.Id}");
    }

    [Fact]
    public async Task With_no_address_nothing_is_queued()
    {
        await WithUnitOfWorkAsync(() => _emailer.SendEmailConfirmationCodeAsync("   ", "SYN789"));

        Queued().ShouldBeEmpty();
    }
}
