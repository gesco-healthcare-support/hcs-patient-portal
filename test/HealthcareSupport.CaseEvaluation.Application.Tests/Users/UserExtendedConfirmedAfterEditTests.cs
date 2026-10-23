using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Users;

/// <summary>
/// The rule an edit on the identity screen follows for the email-confirmed flag: it can only keep
/// confirmation, and only when the address is unchanged. It can never turn it on.
/// </summary>
public class UserExtendedConfirmedAfterEditTests
{
    [Theory]
    [InlineData(true, "a@example.test", "a@example.test", true)]
    [InlineData(true, "a@example.test", " A@Example.TEST ", true)]
    [InlineData(true, "a@example.test", "b@example.test", false)]
    [InlineData(false, "a@example.test", "a@example.test", false)]
    [InlineData(false, "a@example.test", "b@example.test", false)]
    [InlineData(true, null, "a@example.test", false)]
    public void An_edit_keeps_confirmation_only_for_the_same_address_and_never_grants_it(
        bool storedConfirmed, string? storedEmail, string? requestedEmail, bool expected)
    {
        UserExtendedAppService.ConfirmedAfterEdit(storedConfirmed, storedEmail, requestedEmail).ShouldBe(expected);
    }
}
