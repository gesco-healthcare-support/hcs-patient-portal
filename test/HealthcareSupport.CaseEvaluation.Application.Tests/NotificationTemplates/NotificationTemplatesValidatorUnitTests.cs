using System;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Phase 4 (2026-05-03) -- pure unit tests for the
/// <see cref="NotificationTemplatesAppService"/> validator + field-copy
/// helpers. Bypasses the ABP integration test harness (currently blocked
/// by the ABP Pro license validation issue documented in
/// <c>docs/handoffs/2026-05-03-test-host-license-blocker.md</c>) by
/// invoking the static helpers via the <c>InternalsVisibleTo</c> hook.
///
/// Coverage:
///   1. <c>ValidateBodies</c> rejects null Email or SMS body.
///   2. <c>ValidateSubjectLength</c> enforces the 200-char cap and
///      accepts null (Subject is optional in OLD).
///   3. <c>ApplyUpdate</c> copies the four editable fields and preserves
///      <c>TemplateCode</c>, <c>TemplateTypeId</c>, <c>Description</c>,
///      <c>Id</c>, <c>TenantId</c>.
///
/// Integration-tier coverage (paged list, GetByCode, permission gates,
/// concurrency-stamp 409, multi-tenant isolation) lives in
/// <see cref="NotificationTemplatesAppServiceTests{TStartupModule}"/>
/// and runs once the ABP Pro license is populated.
/// </summary>
public class NotificationTemplatesValidatorUnitTests
{
    // ------------------------------------------------------------------
    // Body validation
    // ------------------------------------------------------------------

    [Fact]
    public void ValidateBodies_NullBodyEmail_Throws()
    {
        var dto = ValidDto();
        dto.BodyEmail = null!;

        Should.Throw<ArgumentException>(
            () => NotificationTemplatesAppService.ValidateBodies(dto));
    }

    [Fact]
    public void ValidateBodies_NullBodySms_Throws()
    {
        var dto = ValidDto();
        dto.BodySms = null!;

        Should.Throw<ArgumentException>(
            () => NotificationTemplatesAppService.ValidateBodies(dto));
    }

    [Fact]
    public void ValidateBodies_EmptyStringsAccepted()
    {
        // OLD allowed empty bodies (nvarchar(MAX) with no NOT EMPTY check).
        // NEW preserves that semantics -- only NULL is rejected.
        var dto = ValidDto();
        dto.BodyEmail = string.Empty;
        dto.BodySms = string.Empty;

        NotificationTemplatesAppService.ValidateBodies(dto);
    }

    [Fact]
    public void ValidateBodies_HtmlContent_Accepted()
    {
        var dto = ValidDto();
        dto.BodyEmail = "<p>Welcome ##UserName##</p>";
        dto.BodySms = "Welcome ##UserName##";

        NotificationTemplatesAppService.ValidateBodies(dto);
    }

    // ------------------------------------------------------------------
    // Subject validation
    // ------------------------------------------------------------------

    [Fact]
    public void ValidateSubjectLength_Null_DoesNotThrow()
    {
        var dto = ValidDto();
        dto.Subject = null;

        NotificationTemplatesAppService.ValidateSubjectLength(dto);
    }

    [Fact]
    public void ValidateSubjectLength_AtMax_DoesNotThrow()
    {
        var dto = ValidDto();
        dto.Subject = new string('a', NotificationTemplateConsts.SubjectMaxLength);

        NotificationTemplatesAppService.ValidateSubjectLength(dto);
    }

    [Fact]
    public void ValidateSubjectLength_OverMax_Throws()
    {
        var dto = ValidDto();
        dto.Subject = new string('a', NotificationTemplateConsts.SubjectMaxLength + 1);

        Should.Throw<ArgumentException>(
            () => NotificationTemplatesAppService.ValidateSubjectLength(dto));
    }

    // ------------------------------------------------------------------
    // ApplyUpdate field copy
    // ------------------------------------------------------------------

    [Fact]
    public void ApplyUpdate_CopiesEditableFields()
    {
        var entity = NewEntity(out var originalId, out var originalTenantId, out var originalTypeId);
        entity.Subject = "old subject";
        entity.BodyEmail = "<p>old body</p>";
        entity.BodySms = "old sms";
        entity.IsActive = false;

        var dto = new NotificationTemplateUpdateDto
        {
            Subject = "Welcome to {ClinicName}",
            BodyEmail = "<p>Hi @Model.UserName</p>",
            BodySms = "Hi @Model.UserName",
            IsActive = true,
            ConcurrencyStamp = "client-side-stamp",
        };

        NotificationTemplatesAppService.ApplyUpdate(dto, entity);

        entity.Subject.ShouldBe("Welcome to {ClinicName}");
        entity.BodyEmail.ShouldBe("<p>Hi @Model.UserName</p>");
        entity.BodySms.ShouldBe("Hi @Model.UserName");
        entity.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void ApplyUpdate_PreservesImmutableFields()
    {
        var entity = NewEntity(out var originalId, out var originalTenantId, out var originalTypeId);
        var originalCode = entity.TemplateCode;
        var originalDescription = entity.Description;

        var dto = new NotificationTemplateUpdateDto
        {
            Subject = "any",
            BodyEmail = "any",
            BodySms = "any",
            IsActive = true,
            ConcurrencyStamp = "any",
        };

        NotificationTemplatesAppService.ApplyUpdate(dto, entity);

        entity.Id.ShouldBe(originalId);
        entity.TenantId.ShouldBe(originalTenantId);
        entity.TemplateCode.ShouldBe(originalCode);
        entity.TemplateTypeId.ShouldBe(originalTypeId);
        entity.Description.ShouldBe(originalDescription);
    }

    [Fact]
    public void ApplyUpdate_NullSubject_PersistsAsNull()
    {
        var entity = NewEntity(out _, out _, out _);
        entity.Subject = "previous";

        var dto = ValidDto();
        dto.Subject = null;

        NotificationTemplatesAppService.ApplyUpdate(dto, entity);

        entity.Subject.ShouldBeNull();
    }

    [Fact]
    public void ApplyUpdate_ToggleIsActive_FromTrueToFalse()
    {
        var entity = NewEntity(out _, out _, out _);
        entity.IsActive = true;

        var dto = ValidDto();
        dto.IsActive = false;

        NotificationTemplatesAppService.ApplyUpdate(dto, entity);

        entity.IsActive.ShouldBeFalse();
    }

    // ------------------------------------------------------------------
    // Verbatim 59-code list parity
    // ------------------------------------------------------------------

    [Fact]
    public void Codes_All_Has59Codes()
    {
        // OLD has 16 + 43 = 59 events.
        NotificationTemplateConsts.Codes.All.Length.ShouldBe(59);
    }

    [Fact]
    public void Codes_All_FixesOldTypos()
    {
        // Verifies the four typo fixes recorded in the audit doc:
        // 1. Joint (not Join) Declaration Document
        // 2. Stakeholder (not Stackholder)
        // 3. CancellationApproved (not Apprvd / Apporved)
        // 4. UserRegistered (already correct in C# constant; HTML filename
        //    typo "User-Registed.html" doesn't affect NEW since NEW owns
        //    the seeded body)
        NotificationTemplateConsts.Codes.All.ShouldContain(
            NotificationTemplateConsts.Codes.RejectedJointDeclarationDocument);
        NotificationTemplateConsts.Codes.All.ShouldNotContain("RejectedJoinDeclarationDocument");

        NotificationTemplateConsts.Codes.All.ShouldContain(
            NotificationTemplateConsts.Codes.AppointmentApprovedStakeholderEmails);
        NotificationTemplateConsts.Codes.All.ShouldNotContain("AppointmentApprovedStackholderEmails");

        NotificationTemplateConsts.Codes.All.ShouldContain(
            NotificationTemplateConsts.Codes.PatientAppointmentCancellationApproved);
        NotificationTemplateConsts.Codes.All.ShouldNotContain("PatientAppointmentCancellationApprvd");

        NotificationTemplateConsts.Codes.All.ShouldContain(
            NotificationTemplateConsts.Codes.UserRegistered);
    }

    [Fact]
    public void Codes_All_ContainsKnownDbManagedAndDiskHtmlMembers()
    {
        // Spot-check a few from each OLD source to catch accidental drops.
        // DB-managed (TemplateCode enum):
        NotificationTemplateConsts.Codes.All.ShouldContain(
            NotificationTemplateConsts.Codes.AppointmentBooked);
        NotificationTemplateConsts.Codes.All.ShouldContain(
            NotificationTemplateConsts.Codes.SubmitQuery);
        NotificationTemplateConsts.Codes.All.ShouldContain(
            NotificationTemplateConsts.Codes.AppointmentCancelledByAdmin);

        // On-disk HTML (EmailTemplate static class):
        NotificationTemplateConsts.Codes.All.ShouldContain(
            NotificationTemplateConsts.Codes.PatientAppointmentApprovedExt);
        NotificationTemplateConsts.Codes.All.ShouldContain(
            NotificationTemplateConsts.Codes.JointAgreementLetterUploaded);
        NotificationTemplateConsts.Codes.All.ShouldContain(
            NotificationTemplateConsts.Codes.PendingAppointmentDailyNotification);
    }

    [Fact]
    public void Codes_All_AreUnique()
    {
        var distinct = new System.Collections.Generic.HashSet<string>(
            NotificationTemplateConsts.Codes.All);
        distinct.Count.ShouldBe(NotificationTemplateConsts.Codes.All.Length);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static NotificationTemplateUpdateDto ValidDto() => new()
    {
        Subject = "Welcome",
        BodyEmail = "<p>Welcome</p>",
        BodySms = "Welcome",
        IsActive = true,
        ConcurrencyStamp = "stamp",
    };

    private static NotificationTemplate NewEntity(out Guid id, out Guid? tenantId, out Guid templateTypeId)
    {
        id = Guid.NewGuid();
        tenantId = Guid.NewGuid();
        templateTypeId = Guid.Parse("c0000001-0000-4000-9000-000000000001");
        return new NotificationTemplate(
            id: id,
            tenantId: tenantId,
            templateCode: NotificationTemplateConsts.Codes.AppointmentApproved,
            templateTypeId: templateTypeId,
            subject: "seed subject",
            bodyEmail: "<p>seed body</p>",
            bodySms: "seed sms",
            description: "seed description",
            isActive: true);
    }
}
