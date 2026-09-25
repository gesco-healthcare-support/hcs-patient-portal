using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Unit coverage for <see cref="CcRecipientAppender"/>, which adds the office's configured CC list
/// (<c>SystemParameter.CcEmailIds</c>) to an outgoing email's recipients.
///
/// <para><b>WHAT IS PINNED.</b> Addresses split on semicolons AND commas, are trimmed, blanks are
/// dropped; an address already on the email (compared case-insensitively) is not added twice; every
/// added address is an unregistered OfficeAdmin CC; and a missing row or empty column changes nothing.</para>
///
/// <para><b>The result asserted is the recipient list itself</b>, which the appender mutates in place.
/// The repository is a substitute; no database is touched. Synthetic data only (HIPAA).</para>
/// </summary>
public class CcRecipientAppenderTests
{
    private static CcRecipientAppender Appender(string? ccEmailIds, bool rowExists = true)
    {
        var repository = Substitute.For<ISystemParameterRepository>();
        SystemParameter? row = rowExists
            ? new SystemParameter(Guid.NewGuid(), null, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, false, ccEmailIds)
            : null;
        repository.GetCurrentTenantAsync(Arg.Any<CancellationToken>()).Returns(row);
        return new CcRecipientAppender(repository, NullLogger<CcRecipientAppender>.Instance);
    }

    private static List<NotificationRecipient> Existing() => new()
    {
        new NotificationRecipient(email: "TEST-patient@test.local", role: RecipientRole.Patient, isRegistered: true),
    };

    [Fact]
    public async Task AppendAsync_SplitsOnSemicolonsAndCommas_TrimsAndDropsBlanks()
    {
        var recipients = Existing();

        await Appender(" TEST-cc-one@test.local ; TEST-cc-two@test.local,,  TEST-cc-three@test.local ; ").AppendAsync(recipients, "TEST-context");

        recipients.Select(r => r.Email).ShouldBe(new[]
        {
            "TEST-patient@test.local",
            "TEST-cc-one@test.local",
            "TEST-cc-two@test.local",
            "TEST-cc-three@test.local",
        });
    }

    [Fact]
    public async Task AppendAsync_AddsEachCcAsAnUnregisteredOfficeAdmin()
    {
        var recipients = Existing();

        await Appender("TEST-cc-one@test.local").AppendAsync(recipients);

        var added = recipients.Single(r => r.Email == "TEST-cc-one@test.local");
        added.Role.ShouldBe(RecipientRole.OfficeAdmin);
        added.IsRegistered.ShouldBeFalse();
    }

    /// <summary>
    /// An address already on the email -- in any letter case -- is not added again, and a CC listed
    /// twice in the setting is added once.
    /// </summary>
    [Fact]
    public async Task AppendAsync_NeverAddsAnAddressAlreadyPresent_IgnoringCase()
    {
        var recipients = Existing();

        await Appender("test-PATIENT@test.local;TEST-cc-one@test.local;TEST-CC-ONE@test.local").AppendAsync(recipients);

        recipients.Select(r => r.Email).ShouldBe(new[] { "TEST-patient@test.local", "TEST-cc-one@test.local" });
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("TEST-cc-one@test.local", false)]
    public async Task AppendAsync_NoRowOrAnEmptyColumn_LeavesTheListUnchanged(string? ccEmailIds, bool rowExists)
    {
        var recipients = Existing();

        await Appender(ccEmailIds, rowExists).AppendAsync(recipients);

        recipients.Select(r => r.Email).ShouldBe(new[] { "TEST-patient@test.local" });
    }
}
