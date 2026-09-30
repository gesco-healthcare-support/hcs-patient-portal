using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications.Context;

/// <summary>
/// <see cref="DocumentEmailContextResolver"/> on the real rig, for the paths no other test reaches:
/// how a document is LABELLED in an email, how its uploader is NAMED, and how an attorney-created
/// booking is PROMOTED so the notice greets that attorney.
///
/// <para>The attorney greeting is read from the attorney's master record, matched by email. The OFFICE
/// decoy is a record in office B with the SAME email and a different name. It is inserted BEFORE office
/// A's record on purpose: the lookup is an unordered <c>FirstOrDefault</c> that SQLite answers in
/// insertion order, so a lookup that lost its office filter would reach office B's record first.
/// That makes the decoy fail every time rather than by the luck of row order.</para>
/// </summary>
public class DocumentEmailContextResolverTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string PromotedAttorneyEmail = "TEST-promoted-attorney@test.local";

    [Fact]
    public async Task ACategorisedDocument_IsLabelledWithTheCategorysName()
    {
        var typeId = await CreateDocumentTypeAsync("TEST-Medical Records");
        var documentId = await CreateDocumentAsync(typeId, otherTypeName: "TEST-ignored other text");

        var context = await ResolveAsync(documentId);

        context.DocumentId.ShouldBe(documentId);
        context.DocumentLabel.ShouldBe("TEST-Medical Records");
    }

    [Fact]
    public async Task ADocumentWhoseCategoryWasDeleted_IsLabelledWithItsOtherText()
    {
        var typeId = await CreateDocumentTypeAsync("TEST-Deleted category");
        var documentId = await CreateDocumentAsync(typeId, otherTypeName: "TEST-other text");
        await InOfficeAAsync(() => GetRequiredService<IRepository<AppointmentDocumentType, Guid>>().DeleteAsync(typeId, autoSave: true));

        (await ResolveAsync(documentId)).DocumentLabel.ShouldBe("TEST-other text");
    }

    [Fact]
    public async Task ADocumentWithNoCategoryAndNoOtherText_IsLabelledWithItsOwnName()
    {
        var documentId = await CreateDocumentAsync(typeId: null, otherTypeName: null);

        (await ResolveAsync(documentId)).DocumentLabel.ShouldBe("TEST-Document name");
    }

    [Theory]
    [InlineData("TEST-Up", "TEST-Loader", "TEST-Up TEST-Loader")]
    [InlineData("TEST-Up", null, "TEST-Up")]
    [InlineData(null, "TEST-Loader", "TEST-Loader")]
    public async Task TheUploader_IsNamedFromWhateverNamePartsTheyHave(string? name, string? surname, string expected)
    {
        await NameTenantAdminAsync(name, surname);
        var documentId = await CreateDocumentAsync(typeId: null, otherTypeName: null);

        (await ResolveAsync(documentId)).UploaderFullName.ShouldBe(expected);
    }

    [Fact]
    public async Task AnAttorneyCreatedBooking_IsPromoted_AndGreetsTheAttorneyFromThisOfficesRecord()
    {
        // Decoy FIRST, then office A's own record (see the class summary for why the order matters).
        await CreateApplicantAttorneyAsync(TenantsTestData.TenantBRef, "TEST-Bea", "TEST-Wrongoffice");
        await CreateApplicantAttorneyAsync(TenantsTestData.TenantARef, "TEST-Ada", "TEST-Promoted");
        await SetCreatorAndAttorneyEmailAsync(IdentityUsersTestData.ApplicantAttorney1UserId);

        var context = await ResolveAsync(documentId: null);

        context.IsPromoted.ShouldBeTrue();
        context.PrimaryRecipientEmail.ShouldBe(PromotedAttorneyEmail);
        context.CreatorEmail.ShouldBe(IdentityUsersTestData.ApplicantAttorney1Email);
        context.GreetingName.ShouldBe("TEST-Ada TEST-Promoted");
    }

    [Fact]
    public async Task ABookingWhoseCreatorCannotBeFound_IsNotPromoted()
    {
        // Positive control: the Fact above, the same attorney email, with a creator who exists.
        await CreateApplicantAttorneyAsync(TenantsTestData.TenantARef, "TEST-Ada", "TEST-Promoted");
        await SetCreatorAndAttorneyEmailAsync(Guid.NewGuid());

        var context = await ResolveAsync(documentId: null);

        context.IsPromoted.ShouldBeFalse();
        context.CreatorEmail.ShouldBeNull();
        context.GreetingName.ShouldBeNull();
    }

    // ------------------------------------------------------------------------

    private async Task<DocumentEmailContext> ResolveAsync(Guid? documentId)
    {
        DocumentEmailContext? context = null;
        await InOfficeAAsync(async () =>
        {
            context = await GetRequiredService<DocumentEmailContextResolver>()
                .ResolveAsync(AppointmentsTestData.Appointment1Id, documentId);
        });
        return context.ShouldNotBeNull();
    }

    private async Task<Guid> CreateDocumentTypeAsync(string name)
    {
        var id = Guid.NewGuid();
        await InOfficeAAsync(() => GetRequiredService<IRepository<AppointmentDocumentType, Guid>>().InsertAsync(
            new AppointmentDocumentType(id, name, tenantId: TenantsTestData.TenantARef), autoSave: true));
        return id;
    }

    private async Task<Guid> CreateDocumentAsync(Guid? typeId, string? otherTypeName)
    {
        var id = Guid.NewGuid();
        await InOfficeAAsync(() => GetRequiredService<IRepository<AppointmentDocument, Guid>>().InsertAsync(
            new AppointmentDocument(
                id,
                TenantsTestData.TenantARef,
                AppointmentsTestData.Appointment1Id,
                documentName: "TEST-Document name",
                fileName: "TEST-document.pdf",
                blobName: $"TEST-docs/{id:N}.pdf",
                contentType: "application/pdf",
                fileSize: 1024,
                uploadedByUserId: IdentityUsersTestData.TenantAdmin1UserId,
                appointmentDocumentTypeId: typeId,
                otherDocumentTypeName: otherTypeName),
            autoSave: true));
        return id;
    }

    private Task CreateApplicantAttorneyAsync(Guid officeId, string firstName, string lastName) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(officeId))
            {
                await GetRequiredService<IRepository<ApplicantAttorney, Guid>>().InsertAsync(
                    new ApplicantAttorney(Guid.NewGuid(), stateId: null, identityUserId: null, email: PromotedAttorneyEmail)
                    {
                        TenantId = officeId,
                        FirstName = firstName,
                        LastName = lastName,
                    },
                    autoSave: true);
            }
        });

    private Task SetCreatorAndAttorneyEmailAsync(Guid creatorId) => InOfficeAAsync(async () =>
    {
        var repository = GetRequiredService<IRepository<Appointment, Guid>>();
        var appointment = await repository.GetAsync(AppointmentsTestData.Appointment1Id);
        appointment.ApplicantAttorneyEmail = PromotedAttorneyEmail;
        // CreatorId is an audit property with a protected setter; ABP's own helper sets it.
        ObjectHelper.TrySetProperty(appointment, a => a.CreatorId, () => (Guid?)creatorId);
        await repository.UpdateAsync(appointment, autoSave: true);
    });

    private Task NameTenantAdminAsync(string? name, string? surname) => InOfficeAAsync(async () =>
    {
        var repository = GetRequiredService<IRepository<IdentityUser, Guid>>();
        var user = await repository.GetAsync(IdentityUsersTestData.TenantAdmin1UserId);
        user.Name = name;
        user.Surname = surname;
        await repository.UpdateAsync(user, autoSave: true);
    });

    private Task InOfficeAAsync(Func<Task> action) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            await action();
        }
    });
}
