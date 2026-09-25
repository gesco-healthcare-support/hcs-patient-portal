using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using Shouldly;
using Volo.Abp.Identity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.HealthChecks;

/// <summary>
/// <see cref="CaseEvaluationDatabaseCheck"/> reads one role to prove the database answers. A read
/// that succeeds is Healthy; a read that throws is Unhealthy and carries the exception, so the status
/// page shows why rather than a bare "down". The two Facts share the same substitute, differing only
/// in whether the read throws.
/// </summary>
public class CaseEvaluationDatabaseCheckTests
{
    private static IIdentityRoleRepository Roles(Exception? readFails = null)
    {
        var roles = Substitute.For<IIdentityRoleRepository>();
        roles.GetListAsync(
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(readFails == null
                ? Task.FromResult(new List<IdentityRole>())
                : Task.FromException<List<IdentityRole>>(readFails));
        return roles;
    }

    [Fact]
    public async Task ADatabaseThatAnswers_IsHealthy_AfterReadingAtMostOneRole()
    {
        var roles = Roles();

        var result = await new CaseEvaluationDatabaseCheck(roles).CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Exception.ShouldBeNull();
        await roles.Received(1).GetListAsync(
            nameof(IdentityRole.Id), 1, Arg.Any<int>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ADatabaseThatFails_IsUnhealthy_AndCarriesTheException()
    {
        var failure = new InvalidOperationException("TEST-database-unreachable");

        var result = await new CaseEvaluationDatabaseCheck(Roles(failure)).CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Exception.ShouldBeSameAs(failure);
    }
}
