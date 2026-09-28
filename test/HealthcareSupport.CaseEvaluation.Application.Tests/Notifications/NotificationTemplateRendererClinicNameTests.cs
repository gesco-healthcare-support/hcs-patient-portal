using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using NSubstitute;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Issue #1014, unit level: <see cref="NotificationTemplateRenderer"/> fills <c>##ClinicName##</c> from
/// the tenant STORE for the current office id, because ABP's <c>ICurrentTenant.Change(id)</c> leaves
/// <c>ICurrentTenant.Name</c> null and every handler that passed it sent a blank office name.
///
/// <para>The ambient name here is a DECOY ("TEST-ambient-decoy"), load-bearing: production's is null,
/// so a renderer that read it would pass a test that did not set one and still blank the name live.
/// No rendered text may ever contain it. The real-rig companion is
/// <c>NotificationTemplateRendererOfficeNameTests</c> in the EF test project.</para>
/// </summary>
public class NotificationTemplateRendererClinicNameTests
{
    private const string OfficeName = "TEST-office-from-store";
    private const string AmbientDecoy = "TEST-ambient-decoy";
    private const string TemplateCode = "TEST-CODE";

    private readonly Guid _officeId = Guid.NewGuid();
    private readonly INotificationTemplateRepository _templates = Substitute.For<INotificationTemplateRepository>();
    private readonly ITenantStore _tenantStore = Substitute.For<ITenantStore>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();

    public NotificationTemplateRendererClinicNameTests()
    {
        _currentTenant.Id.Returns(_officeId);
        _currentTenant.Name.Returns(AmbientDecoy);
        _tenantStore.FindAsync(_officeId).Returns(new TenantConfiguration(_officeId, OfficeName));
        UseTemplate(subject: "TEST subject for ##ClinicName##", bodyEmail: "<p>##ClinicName##|##ClinicName##</p>");
    }

    [Fact]
    public async Task NoClinicNameKey_FillsTheOfficeNameFromTheStore_Everywhere()
    {
        var variables = new Dictionary<string, object?>(StringComparer.Ordinal) { ["Other"] = "TEST-other" };

        var rendered = await Build().RenderAsync(TemplateCode, variables);

        rendered.Subject.ShouldBe($"TEST subject for {OfficeName}");
        rendered.BodyEmail.ShouldBe($"<p>{OfficeName}|{OfficeName}</p>");
        variables.ShouldNotContainKey("ClinicName", "the caller's dictionary must not be mutated");
        await _tenantStore.Received(1).FindAsync(_officeId);
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
    public async Task ExplicitClinicName_Wins_AndTheStoreIsNotAsked()
    {
        var rendered = await Build().RenderAsync(TemplateCode, ClinicName("TEST-explicit-office"));

        rendered.BodyEmail.ShouldBe("<p>TEST-explicit-office|TEST-explicit-office</p>");
        _tenantStore.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task TemplateWithoutThePlaceholder_DoesNotAskTheStore()
    {
        UseTemplate(subject: "TEST subject", bodyEmail: "<p>TEST no office here</p>");

        var rendered = await Build().RenderAsync(TemplateCode, ClinicName(null));

        rendered.BodyEmail.ShouldBe("<p>TEST no office here</p>");
        _tenantStore.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task OutsideAnOfficeScope_RendersBlank_WithoutAskingTheStore()
    {
        _currentTenant.Id.Returns((Guid?)null);

        var rendered = await Build().RenderAsync(TemplateCode, ClinicName(null));

        rendered.BodyEmail.ShouldBe("<p>|</p>");
        _tenantStore.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task OfficeUnknownToTheStore_RendersBlank_NotTheAmbientName()
    {
        var unknownOffice = Guid.NewGuid();
        _currentTenant.Id.Returns(unknownOffice);
        _tenantStore.FindAsync(unknownOffice).Returns((TenantConfiguration?)null);

        var rendered = await Build().RenderAsync(TemplateCode, ClinicName(null));

        rendered.BodyEmail.ShouldBe("<p>|</p>");
    }

    private NotificationTemplateRenderer Build() => new(_templates, _tenantStore, _currentTenant);

    private void UseTemplate(string subject, string bodyEmail) =>
        _templates.FindByCodeAsync(TemplateCode, Arg.Any<CancellationToken>()).Returns(
            new NotificationTemplate(Guid.NewGuid(), _officeId, TemplateCode, Guid.NewGuid(), subject, bodyEmail, ""));

    private static Dictionary<string, object?> ClinicName(string? value) =>
        new(StringComparer.Ordinal) { ["ClinicName"] = value };
}
