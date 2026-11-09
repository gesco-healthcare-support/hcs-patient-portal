using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Branding;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using NSubstitute;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Issue #1014, unit level: <see cref="NotificationTemplateRenderer"/> fills <c>##ClinicName##</c> for
/// the current office itself, because ABP's <c>ICurrentTenant.Change(id)</c> leaves
/// <c>ICurrentTenant.Name</c> null and every handler that passed it sent a blank office name.
/// 2026-11-09: the name is the office's PRACTICE name from <see cref="IOfficeDisplayNameResolver"/>,
/// no longer the tenant store's name (the web-address slug).
///
/// <para>The ambient name here is a DECOY ("TEST-ambient-decoy"), load-bearing: production's is null,
/// so a renderer that read it would pass a test that did not set one and still blank the name live.
/// No rendered text may ever contain it. The real-rig companion is
/// <c>NotificationTemplateRendererOfficeNameTests</c> in the EF test project.</para>
/// </summary>
public class NotificationTemplateRendererClinicNameTests
{
    private const string OfficeName = "TEST Practice Name";
    private const string AmbientDecoy = "TEST-ambient-decoy";
    private const string TemplateCode = "TEST-CODE";

    private readonly Guid _officeId = Guid.NewGuid();
    private readonly INotificationTemplateRepository _templates = Substitute.For<INotificationTemplateRepository>();
    private readonly IOfficeDisplayNameResolver _displayNames = Substitute.For<IOfficeDisplayNameResolver>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();

    public NotificationTemplateRendererClinicNameTests()
    {
        _currentTenant.Id.Returns(_officeId);
        _currentTenant.Name.Returns(AmbientDecoy);
        _displayNames.ResolveCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(new OfficeDisplayNames(OfficeName, "TEST-Doctor (TEST Practice Name)"));
        UseTemplate(subject: "TEST subject for ##ClinicName##", bodyEmail: "<p>##ClinicName##|##ClinicName##</p>");
    }

    [Fact]
    public async Task NoClinicNameKey_FillsTheOfficePracticeName_Everywhere()
    {
        var variables = new Dictionary<string, object?>(StringComparer.Ordinal) { ["Other"] = "TEST-other" };

        var rendered = await Build().RenderAsync(TemplateCode, variables);

        rendered.Subject.ShouldBe($"TEST subject for {OfficeName}");
        rendered.BodyEmail.ShouldBe($"<p>{OfficeName}|{OfficeName}</p>");
        variables.ShouldNotContainKey("ClinicName", "the caller's dictionary must not be mutated");
        await _displayNames.Received(1).ResolveCurrentAsync(Arg.Any<CancellationToken>());
        rendered.BodyEmail.ShouldNotContain("TEST-Doctor", Case.Sensitive, "##ClinicName## is the practice, not the doctor");
    }

    /// <summary>
    /// Every document handler goes through <c>BuildVariables</c>, which always emits the key, with ""
    /// for a null name -- so an empty or null value must be treated as "not supplied".
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task BlankClinicNameValue_IsTreatedAsNotSupplied(string? passed)
    {
        var rendered = await Build().RenderAsync(TemplateCode, ClinicName(passed));

        rendered.BodyEmail.ShouldBe($"<p>{OfficeName}|{OfficeName}</p>");
        rendered.BodyEmail.ShouldNotContain(AmbientDecoy);
    }

    /// <summary>The accessor handlers resolve the name themselves; an explicit value wins untouched.</summary>
    [Fact]
    public async Task ExplicitClinicName_Wins_AndNoNameIsLookedUp()
    {
        var rendered = await Build().RenderAsync(TemplateCode, ClinicName("TEST-explicit-office"));

        rendered.BodyEmail.ShouldBe("<p>TEST-explicit-office|TEST-explicit-office</p>");
        _displayNames.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task TemplateWithoutThePlaceholder_LooksUpNoName()
    {
        UseTemplate(subject: "TEST subject", bodyEmail: "<p>TEST no office here</p>");

        var rendered = await Build().RenderAsync(TemplateCode, ClinicName(null));

        rendered.BodyEmail.ShouldBe("<p>TEST no office here</p>");
        _displayNames.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task OutsideAnOfficeScope_RendersBlank_WithoutLookingUpAName()
    {
        _currentTenant.Id.Returns((Guid?)null);

        var rendered = await Build().RenderAsync(TemplateCode, ClinicName(null));

        rendered.BodyEmail.ShouldBe("<p>|</p>");
        _displayNames.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task OfficeWithNoNameAtAll_RendersBlank_NotTheAmbientName()
    {
        _displayNames.ResolveCurrentAsync(Arg.Any<CancellationToken>()).Returns(OfficeDisplayNames.None);

        var rendered = await Build().RenderAsync(TemplateCode, ClinicName(null));

        rendered.BodyEmail.ShouldBe("<p>|</p>");
    }

    private NotificationTemplateRenderer Build() => new(_templates, _displayNames, _currentTenant);

    private void UseTemplate(string subject, string bodyEmail) =>
        _templates.FindByCodeAsync(TemplateCode, Arg.Any<CancellationToken>()).Returns(
            new NotificationTemplate(Guid.NewGuid(), _officeId, TemplateCode, Guid.NewGuid(), subject, bodyEmail, ""));

    private static Dictionary<string, object?> ClinicName(string? value) =>
        new(StringComparer.Ordinal) { ["ClinicName"] = value };
}
