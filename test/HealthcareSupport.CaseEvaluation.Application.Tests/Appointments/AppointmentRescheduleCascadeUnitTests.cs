using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 11j (2026-05-04) -- per-child-entity deep-clone tests.
/// Verifies that every clone helper preserves the source's scalars
/// while re-pointing the parent FK and giving the new row a fresh
/// Id + tenant assignment.
/// </summary>
public class AppointmentRescheduleCascadeUnitTests
{
    private static AppointmentInjuryDetail MakeInjurySource()
    {
        var src = new AppointmentInjuryDetail(
            id: Guid.NewGuid(),
            appointmentId: Guid.NewGuid(),
            dateOfInjury: new DateTime(2024, 6, 1),
            claimNumber: "CLM-12345",
            isCumulativeInjury: true,
            bodyPartsSummary: "lumbar spine",
            toDateOfInjury: new DateTime(2024, 6, 30),
            wcabAdj: "ADJ-99",
            wcabOfficeId: Guid.NewGuid());
        src.TenantId = Guid.NewGuid();
        return src;
    }

    [Fact]
    public void CloneInjuryDetailFor_PreservesScalarsAndRepointsAppointment()
    {
        var src = MakeInjurySource();
        var newId = Guid.NewGuid();
        var newApptId = Guid.NewGuid();
        var newTenant = Guid.NewGuid();

        var clone = AppointmentRescheduleCloner.CloneInjuryDetailFor(src, newId, newApptId, newTenant);

        clone.Id.ShouldBe(newId);
        clone.AppointmentId.ShouldBe(newApptId);
        clone.TenantId.ShouldBe(newTenant);
        clone.DateOfInjury.ShouldBe(src.DateOfInjury);
        clone.ToDateOfInjury.ShouldBe(src.ToDateOfInjury);
        clone.ClaimNumber.ShouldBe(src.ClaimNumber);
        clone.IsCumulativeInjury.ShouldBe(src.IsCumulativeInjury);
        clone.BodyPartsSummary.ShouldBe(src.BodyPartsSummary);
        clone.WcabAdj.ShouldBe(src.WcabAdj);
        clone.WcabOfficeId.ShouldBe(src.WcabOfficeId);
        clone.Id.ShouldNotBe(src.Id);
        clone.AppointmentId.ShouldNotBe(src.AppointmentId);
    }

    [Fact]
    public void CloneInjuryDetailFor_NullSource_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            AppointmentRescheduleCloner.CloneInjuryDetailFor(null!, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void CloneBodyPartFor_PreservesDescriptionAndRepointsParent()
    {
        var src = new AppointmentBodyPart(
            id: Guid.NewGuid(),
            appointmentInjuryDetailId: Guid.NewGuid(),
            bodyPartDescription: "cervical spine");
        src.TenantId = Guid.NewGuid();

        var newId = Guid.NewGuid();
        var newInjuryId = Guid.NewGuid();
        var newTenant = Guid.NewGuid();

        var clone = AppointmentRescheduleCloner.CloneBodyPartFor(src, newId, newInjuryId, newTenant);

        clone.Id.ShouldBe(newId);
        clone.AppointmentInjuryDetailId.ShouldBe(newInjuryId);
        clone.BodyPartDescription.ShouldBe(src.BodyPartDescription);
        clone.TenantId.ShouldBe(newTenant);
        clone.Id.ShouldNotBe(src.Id);
        clone.AppointmentInjuryDetailId.ShouldNotBe(src.AppointmentInjuryDetailId);
    }

    [Fact]
    public void CloneClaimExaminerFor_PreservesEveryScalar()
    {
        var src = new AppointmentClaimExaminer(
            id: Guid.NewGuid(),
            appointmentInjuryDetailId: Guid.NewGuid(),
            isActive: true);
        src.TenantId = Guid.NewGuid();
        src.Name = "Jane Doe";
        src.ClaimExaminerNumber = "CE-001";
        src.Email = "ce@test.local";
        src.PhoneNumber = "555-0100";
        src.Fax = "555-0199";
        src.Street = "123 Main";
        src.City = "Phoenix";
        src.Zip = "85001";
        src.StateId = Guid.NewGuid();

        var newId = Guid.NewGuid();
        var newInjuryId = Guid.NewGuid();
        var newTenant = Guid.NewGuid();

        var clone = AppointmentRescheduleCloner.CloneClaimExaminerFor(src, newId, newInjuryId, newTenant);

        clone.Id.ShouldBe(newId);
        clone.AppointmentInjuryDetailId.ShouldBe(newInjuryId);
        clone.TenantId.ShouldBe(newTenant);
        clone.IsActive.ShouldBe(src.IsActive);
        clone.Name.ShouldBe(src.Name);
        clone.ClaimExaminerNumber.ShouldBe(src.ClaimExaminerNumber);
        clone.Email.ShouldBe(src.Email);
        clone.PhoneNumber.ShouldBe(src.PhoneNumber);
        clone.Fax.ShouldBe(src.Fax);
        clone.Street.ShouldBe(src.Street);
        clone.City.ShouldBe(src.City);
        clone.Zip.ShouldBe(src.Zip);
        clone.StateId.ShouldBe(src.StateId);
    }

    [Fact]
    public void ClonePrimaryInsuranceFor_PreservesEveryScalar()
    {
        var src = new AppointmentPrimaryInsurance(
            id: Guid.NewGuid(),
            appointmentInjuryDetailId: Guid.NewGuid(),
            isActive: true);
        src.TenantId = Guid.NewGuid();
        src.Name = "ACME Insurance";
        src.InsuranceNumber = "POL-001";
        src.Attention = "Claims Dept";
        src.PhoneNumber = "555-0101";
        src.FaxNumber = "555-0299";
        src.Street = "456 Oak";
        src.City = "Tucson";
        src.Zip = "85700";
        src.StateId = Guid.NewGuid();

        var newId = Guid.NewGuid();
        var newInjuryId = Guid.NewGuid();
        var newTenant = Guid.NewGuid();

        var clone = AppointmentRescheduleCloner.ClonePrimaryInsuranceFor(src, newId, newInjuryId, newTenant);

        clone.Id.ShouldBe(newId);
        clone.AppointmentInjuryDetailId.ShouldBe(newInjuryId);
        clone.TenantId.ShouldBe(newTenant);
        clone.IsActive.ShouldBe(src.IsActive);
        clone.Name.ShouldBe(src.Name);
        clone.InsuranceNumber.ShouldBe(src.InsuranceNumber);
        clone.Attention.ShouldBe(src.Attention);
        clone.PhoneNumber.ShouldBe(src.PhoneNumber);
        clone.FaxNumber.ShouldBe(src.FaxNumber);
        clone.Street.ShouldBe(src.Street);
        clone.City.ShouldBe(src.City);
        clone.Zip.ShouldBe(src.Zip);
        clone.StateId.ShouldBe(src.StateId);
    }

    [Fact]
    public void CloneEmployerDetailFor_PreservesAllFields()
    {
        var src = new AppointmentEmployerDetail(
            id: Guid.NewGuid(),
            appointmentId: Guid.NewGuid(),
            stateId: Guid.NewGuid(),
            employerName: "ACME Corp",
            occupation: "Welder");
        src.TenantId = Guid.NewGuid();
        src.PhoneNumber = "555-0123";
        src.Street = "789 Elm";
        src.City = "Mesa";
        src.ZipCode = "85201";

        var newId = Guid.NewGuid();
        var newApptId = Guid.NewGuid();
        var newTenant = Guid.NewGuid();

        var clone = AppointmentRescheduleCloner.CloneEmployerDetailFor(src, newId, newApptId, newTenant);

        clone.Id.ShouldBe(newId);
        clone.AppointmentId.ShouldBe(newApptId);
        clone.TenantId.ShouldBe(newTenant);
        clone.StateId.ShouldBe(src.StateId);
        clone.EmployerName.ShouldBe(src.EmployerName);
        clone.Occupation.ShouldBe(src.Occupation);
        clone.PhoneNumber.ShouldBe(src.PhoneNumber);
        clone.Street.ShouldBe(src.Street);
        clone.City.ShouldBe(src.City);
        clone.ZipCode.ShouldBe(src.ZipCode);
    }

    [Fact]
    public void CloneApplicantAttorneyFor_RepointsAppointmentKeepsAttorneyAndUser()
    {
        var src = new AppointmentApplicantAttorney(
            id: Guid.NewGuid(),
            appointmentId: Guid.NewGuid(),
            applicantAttorneyId: Guid.NewGuid(),
            identityUserId: Guid.NewGuid());
        src.TenantId = Guid.NewGuid();

        var newId = Guid.NewGuid();
        var newApptId = Guid.NewGuid();
        var newTenant = Guid.NewGuid();

        var clone = AppointmentRescheduleCloner.CloneApplicantAttorneyFor(src, newId, newApptId, newTenant);

        clone.Id.ShouldBe(newId);
        clone.AppointmentId.ShouldBe(newApptId);
        clone.ApplicantAttorneyId.ShouldBe(src.ApplicantAttorneyId);
        clone.IdentityUserId.ShouldBe(src.IdentityUserId);
        clone.TenantId.ShouldBe(newTenant);
    }

    [Fact]
    public void CloneDefenseAttorneyFor_RepointsAppointmentKeepsAttorneyAndUser()
    {
        var src = new AppointmentDefenseAttorney(
            id: Guid.NewGuid(),
            appointmentId: Guid.NewGuid(),
            defenseAttorneyId: Guid.NewGuid(),
            identityUserId: Guid.NewGuid());
        src.TenantId = Guid.NewGuid();

        var newId = Guid.NewGuid();
        var newApptId = Guid.NewGuid();
        var newTenant = Guid.NewGuid();

        var clone = AppointmentRescheduleCloner.CloneDefenseAttorneyFor(src, newId, newApptId, newTenant);

        clone.Id.ShouldBe(newId);
        clone.AppointmentId.ShouldBe(newApptId);
        clone.DefenseAttorneyId.ShouldBe(src.DefenseAttorneyId);
        clone.IdentityUserId.ShouldBe(src.IdentityUserId);
        clone.TenantId.ShouldBe(newTenant);
    }

    [Fact]
    public void CloneAccessorFor_PreservesAccessTypeAndIdentityUser()
    {
        var src = new AppointmentAccessor(
            id: Guid.NewGuid(),
            identityUserId: Guid.NewGuid(),
            appointmentId: Guid.NewGuid(),
            accessTypeId: AccessType.Edit);
        src.TenantId = Guid.NewGuid();

        var newId = Guid.NewGuid();
        var newApptId = Guid.NewGuid();
        var newTenant = Guid.NewGuid();

        var clone = AppointmentRescheduleCloner.CloneAccessorFor(src, newId, newApptId, newTenant);

        clone.Id.ShouldBe(newId);
        clone.AppointmentId.ShouldBe(newApptId);
        clone.IdentityUserId.ShouldBe(src.IdentityUserId);
        clone.AccessTypeId.ShouldBe(src.AccessTypeId);
        clone.TenantId.ShouldBe(newTenant);
    }

    [Fact]
    public void AllChildClones_NullSource_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            AppointmentRescheduleCloner.CloneBodyPartFor(null!, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        Should.Throw<ArgumentNullException>(() =>
            AppointmentRescheduleCloner.CloneClaimExaminerFor(null!, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        Should.Throw<ArgumentNullException>(() =>
            AppointmentRescheduleCloner.ClonePrimaryInsuranceFor(null!, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        Should.Throw<ArgumentNullException>(() =>
            AppointmentRescheduleCloner.CloneEmployerDetailFor(null!, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        Should.Throw<ArgumentNullException>(() =>
            AppointmentRescheduleCloner.CloneApplicantAttorneyFor(null!, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        Should.Throw<ArgumentNullException>(() =>
            AppointmentRescheduleCloner.CloneDefenseAttorneyFor(null!, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        Should.Throw<ArgumentNullException>(() =>
            AppointmentRescheduleCloner.CloneAccessorFor(null!, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
    }
}
