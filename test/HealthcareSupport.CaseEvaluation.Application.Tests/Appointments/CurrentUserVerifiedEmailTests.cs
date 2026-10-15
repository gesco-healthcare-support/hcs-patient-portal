using NSubstitute;
using Shouldly;
using Volo.Abp.Users;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

public class CurrentUserVerifiedEmailTests
{
    [Theory]
    [InlineData(true, "party@example.test", "party@example.test")]
    [InlineData(false, "party@example.test", null)]
    public void GetVerifiedEmail_returns_the_address_only_when_confirmed(
        bool verified, string email, string? expected)
    {
        var user = Substitute.For<ICurrentUser>();
        user.Email.Returns(email);
        user.EmailVerified.Returns(verified);

        user.GetVerifiedEmail().ShouldBe(expected);
    }
}
