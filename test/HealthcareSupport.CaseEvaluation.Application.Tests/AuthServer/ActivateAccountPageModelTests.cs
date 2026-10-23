using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ExternalAccount;
using HealthcareSupport.CaseEvaluation.Pages.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AuthServer.Tests;

/// <summary>
/// The activate-your-account page model over substituted collaborators. What it must hold: opening
/// the link (GET, and HEAD, which Razor Pages answers with the same handler) only ASKS whether the
/// link works and never activates; the POST is the one place that does. The activation logic itself
/// is covered with the real token provider in <c>AccountActivationFlowTests</c>.
/// </summary>
public class ActivateAccountPageModelTests
{
    private static readonly Guid UserId = Guid.Parse("7e570000-0000-4000-9000-00000000a001");
    private const string Token = "TEST-activation-token";
    private const string Password = "TEST-Passw0rd!";

    private readonly IAccountActivationLinkChecker _checker = Substitute.For<IAccountActivationLinkChecker>();
    private readonly IAccountActivationService _activation = Substitute.For<IAccountActivationService>();

    private ActivateAccountModel Model(bool linkUsable = true)
    {
        _checker.IsUsableAsync(UserId, Token).Returns(linkUsable);
        var httpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        var model = new ActivateAccountModel(_checker, _activation, NullLogger<ActivateAccountModel>.Instance)
        {
            UserId = UserId,
            Token = Token,
            Password = Password,
            ConfirmPassword = Password,
            PageContext = new PageContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Substitute.For<ITempDataProvider>()),
        };
        return model;
    }

    [Fact]
    public async Task Opening_a_usable_link_shows_the_form_and_activates_nothing()
    {
        var model = Model(linkUsable: true);

        (await model.OnGetAsync()).ShouldBeOfType<PageResult>();

        model.LinkIsDead.ShouldBeFalse();
        await _checker.Received(1).IsUsableAsync(UserId, Token);
        _activation.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Opening_a_dead_link_shows_the_dead_link_message_instead_of_the_form()
    {
        var model = Model(linkUsable: false);

        (await model.OnGetAsync()).ShouldBeOfType<PageResult>();

        model.LinkIsDead.ShouldBeTrue();
        _activation.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_scanner_opening_the_link_first_does_not_stop_the_person_activating_it()
    {
        var model = Model(linkUsable: true);

        // Safe Links-style: HEAD then GET, both answered by the GET handler, before the real click.
        await model.OnGetAsync();
        await model.OnGetAsync();
        _activation.ReceivedCalls().ShouldBeEmpty();

        var result = (await model.OnPostAsync()).ShouldBeOfType<LocalRedirectResult>();

        result.Url.ShouldBe("~/Account/Login?flash=password-updated");
        await _activation.Received(1).ActivateAsync(Arg.Is<ActivateAccountInput>(i =>
            i.UserId == UserId && i.Token == Token && i.Password == Password && i.ConfirmPassword == Password));
    }

    [Fact]
    public async Task A_dead_link_at_submit_shows_the_dead_link_message()
    {
        _activation.ActivateAsync(Arg.Any<ActivateAccountInput>())
            .ThrowsAsync(new BusinessException(CaseEvaluationDomainErrorCodes.ActivationLinkInvalid));
        var model = Model();

        (await model.OnPostAsync()).ShouldBeOfType<PageResult>();

        model.LinkIsDead.ShouldBeTrue();
        model.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task A_password_the_rules_refuse_keeps_the_form_and_says_why()
    {
        _activation.ActivateAsync(Arg.Any<ActivateAccountInput>())
            .ThrowsAsync(new UserFriendlyException("TEST-Passwords must have at least one digit."));
        var model = Model();

        (await model.OnPostAsync()).ShouldBeOfType<PageResult>();

        model.LinkIsDead.ShouldBeFalse();
        model.ErrorMessage.ShouldBe("TEST-Passwords must have at least one digit.");
    }

    [Fact]
    public async Task An_unexpected_failure_keeps_the_form_with_a_generic_message()
    {
        _activation.ActivateAsync(Arg.Any<ActivateAccountInput>())
            .ThrowsAsync(new InvalidOperationException("TEST-database-down"));
        var model = Model();

        (await model.OnPostAsync()).ShouldBeOfType<PageResult>();

        model.LinkIsDead.ShouldBeFalse();
        model.ErrorMessage.ShouldBe("We could not activate your account just now. Please try again.");
    }

    [Fact]
    public async Task An_invalid_form_is_shown_again_without_trying_to_activate()
    {
        var model = Model();
        model.ModelState.AddModelError(nameof(ActivateAccountModel.Password), "Choose a password.");

        (await model.OnPostAsync()).ShouldBeOfType<PageResult>();

        _activation.ReceivedCalls().ShouldBeEmpty();
    }
}
