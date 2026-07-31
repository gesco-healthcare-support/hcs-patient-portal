using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.States;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Builds the intake payload with the REAL resolvers over mocked repositories, so the assertions
/// cover actual contract fidelity rather than a mock's opinion of it. All fixture data is synthetic.
///
/// <para>The most valuable assertion here is the negative one: the serialized JSON must contain no
/// patient identifier. The portal has no cross-office patient identity and CalMed mints a new id per
/// claim, so publishing ours would hand the receiver something that looks authoritative and is not.</para>
/// </summary>
public class IntakePayloadBuilderTests
{
    private const string SyntheticPanelNumber = "PN-SAMPLE";

    private static readonly Guid TenantId = new("b8844bba-414c-e238-4a71-3a22841f21af");
    private static readonly Guid AppointmentId = new("ada5e3c5-0034-ebde-253c-3a2293631dee");
    private static readonly Guid SourceAppointmentId = new("3c9d1b77-2e40-4a51-8bb2-77f0a1c9d233");
    private static readonly Guid AppointmentTypeId = new("a1c2e3f4-5566-4778-9900-aabbccddeeff");
    private static readonly Guid LocationId = new("c0ffee0a-bcde-4f01-9abc-de0123456f7a");
    private static readonly Guid SlotId = new("d1e2f3a4-b5c6-4d7e-8f90-a1b2c3d4e5fa");
    private static readonly Guid PatientId = new("e5f6a7b8-c9d0-4e1f-a2b3-c4d5e6f7a8bc");
    private static readonly Guid DoctorId = new("f7a8b9c0-d1e2-4f30-a415-263748596a7b");

    private static Appointment NewAppointment(
        Guid id,
        string confirmation,
        EvaluationKind kind = EvaluationKind.Evaluation,
        Guid? originalAppointmentId = null)
    {
        return new Appointment(
            id, PatientId, identityUserId: null, AppointmentTypeId, LocationId, SlotId,
            appointmentDate: new DateTime(2026, 8, 15, 9, 30, 0, DateTimeKind.Utc),
            requestConfirmationNumber: confirmation,
            appointmentStatus: AppointmentStatusType.Approved,
            panelNumber: SyntheticPanelNumber)
        {
            TenantId = TenantId,
            EvaluationKind = kind,
            OriginalAppointmentId = originalAppointmentId,
            AppointmentApproveDate = new DateTime(2026, 7, 27, 18, 30, 12, DateTimeKind.Utc),
        };
    }

    private static IntakePayloadBuilder Build(
        Appointment appointment,
        Appointment? sourceAppointment = null,
        List<AppointmentDocument>? documents = null,
        List<AppointmentPacket>? packets = null,
        List<AppointmentInjuryDetailWithNavigationProperties>? injuries = null,
        bool withDoctor = true)
    {
        var appointmentRepo = Substitute.For<IRepository<Appointment, Guid>>();
        appointmentRepo.GetAsync(appointment.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(appointment));
        appointmentRepo.FindAsync(SourceAppointmentId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(sourceAppointment));

        var slotRepo = Substitute.For<IRepository<DoctorAvailability, Guid>>();
        slotRepo.FindAsync(SlotId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DoctorAvailability?>(new DoctorAvailability(
                SlotId, LocationId,
                availableDate: new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
                fromTime: new TimeOnly(9, 30),
                toTime: new TimeOnly(10, 30),
                bookingStatusId: BookingStatus.Available)));

        var typeRepo = Substitute.For<IRepository<AppointmentType, Guid>>();
        typeRepo.FindAsync(AppointmentTypeId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AppointmentType?>(new AppointmentType(
                AppointmentTypeId, "Panel Qualified Medical Examination (PQME)")));

        var patientRepo = Substitute.For<IRepository<Patient, Guid>>();
        patientRepo.FindAsync(PatientId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Patient?>(new Patient(
                PatientId, stateId: null, appointmentLanguageId: null, identityUserId: null,
                tenantId: TenantId,
                firstName: "Jordan", lastName: "Sample", email: "jordan.sample@example.test",
                genderId: Gender.Male,
                dateOfBirth: new DateTime(1985, 4, 12, 0, 0, 0, DateTimeKind.Utc),
                phoneNumberTypeId: PhoneNumberType.Home,
                middleName: "A", phoneNumber: "555-0142", cellPhoneNumber: "555-0177")));

        var doctors = withDoctor
            ? new List<Doctor>
            {
                new(DoctorId, "Morgan", "Reyes", "morgan.reyes@example.test", Gender.Male),
            }
            : new List<Doctor>();
        var doctorRepo = Substitute.For<IRepository<Doctor, Guid>>();
        doctorRepo.GetQueryableAsync().Returns(_ => doctors.AsQueryable());

        var locationRepo = Substitute.For<IRepository<Location, Guid>>();
        locationRepo.FindAsync(LocationId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Location?>(new Location(
                LocationId, stateId: null, name: "North Clinic", parkingFee: 10m, isActive: true,
                address: "120 Example Blvd", city: "Encino", zipCode: "91316", facilityId: "FAC-SAMPLE")));

        var tenantStore = Substitute.For<ITenantStore>();
        tenantStore.FindAsync(TenantId)
            .Returns(Task.FromResult<TenantConfiguration?>(new TenantConfiguration(TenantId, "Reyes Medical Group")));

        var configuration = Substitute.For<IConfiguration>();
        configuration["BlobStoring:Minio:BucketName"].Returns("case-evaluation-documents");

        var documentRepo = Substitute.For<IRepository<AppointmentDocument, Guid>>();
        documentRepo.GetListAsync(
                Arg.Any<Expression<Func<AppointmentDocument, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(documents ?? new List<AppointmentDocument>()));

        var packetRepo = Substitute.For<IRepository<AppointmentPacket, Guid>>();
        packetRepo.GetListAsync(
                Arg.Any<Expression<Func<AppointmentPacket, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(packets ?? new List<AppointmentPacket>()));

        var documentTypeRepo = Substitute.For<IRepository<AppointmentDocumentType, Guid>>();
        documentTypeRepo.GetListAsync(
                Arg.Any<Expression<Func<AppointmentDocumentType, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<AppointmentDocumentType>()));

        var injuryRepo = Substitute.For<IAppointmentInjuryDetailRepository>();
        injuryRepo.GetListWithNavigationPropertiesAsync(
                Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(injuries ?? new List<AppointmentInjuryDetailWithNavigationProperties>()));

        var insuranceRepo = Substitute.For<IRepository<AppointmentPrimaryInsurance, Guid>>();
        insuranceRepo.GetListAsync(
                Arg.Any<Expression<Func<AppointmentPrimaryInsurance, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<AppointmentPrimaryInsurance>()));

        var examinerRepo = Substitute.For<IRepository<AppointmentClaimExaminer, Guid>>();
        examinerRepo.GetListAsync(
                Arg.Any<Expression<Func<AppointmentClaimExaminer, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<AppointmentClaimExaminer>()));

        var stateRepo = Substitute.For<IRepository<State, Guid>>();
        stateRepo.GetListAsync(
                Arg.Any<Expression<Func<State, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<State>()));

        return new IntakePayloadBuilder(
            appointmentRepo,
            new AppointmentCoreResolver(appointmentRepo, slotRepo, typeRepo),
            new PartyResolver(patientRepo, doctorRepo),
            new TenantLocationResolver(locationRepo, tenantStore, configuration),
            new DocumentListResolver(documentRepo, packetRepo, documentTypeRepo),
            new InjuryResolver(injuryRepo),
            new PartyDetailResolver(insuranceRepo, examinerRepo, stateRepo),
            SimpleGuidGenerator.Instance);
    }

    [Fact]
    public async Task BuildAsync_PopulatesTheContractFields()
    {
        var builder = Build(NewAppointment(AppointmentId, "A00065"));

        var envelope = await builder.BuildAsync(AppointmentId);
        var data = envelope.Data;

        data.AppointmentId.ShouldBe(AppointmentId);
        data.ConfirmationNumber.ShouldBe("A00065");
        data.Status.ShouldBe("Approved");
        data.PanelNumber.ShouldBe(SyntheticPanelNumber);
        data.ApprovedAtUtc.ShouldNotBeNull();
        data.ApprovedAtUtc!.ShouldEndWith("Z");
        data.Tenant.FacilityId.ShouldBe("FAC-SAMPLE");
        data.Tenant.OfficeName.ShouldBe("Reyes Medical Group");
        data.Location.Name.ShouldBe("North Clinic");
        data.AppointmentType.Name.ShouldBe("Panel Qualified Medical Examination (PQME)");
        // The id is what the receiver matches on: their name-based matcher failed on the first live
        // push, so the stable identifier is the durable fix (2026-07-31).
        data.Doctor.Id.ShouldBe(DoctorId);
        data.Doctor.FirstName.ShouldBe("Morgan");
        data.Doctor.LastName.ShouldBe("Reyes");
        data.Storage.Bucket.ShouldBe("case-evaluation-documents");
        envelope.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task BuildAsync_DerivesScheduleFromTheSlot()
    {
        var builder = Build(NewAppointment(AppointmentId, "A00065"));

        var data = (await builder.BuildAsync(AppointmentId)).Data;

        data.AppointmentDateLocal.ShouldBe("2026-08-15");
        data.AppointmentTimeLocal.ShouldBe("09:30");
        data.TimeZone.ShouldBe("America/Los_Angeles");
        data.DurationMinutes.ShouldBe(60); // 09:30 -> 10:30, derived (no stored duration)
    }

    [Fact]
    public async Task BuildAsync_FormatsPatientFieldsForTheWire()
    {
        var builder = Build(NewAppointment(AppointmentId, "A00065"));

        var patient = (await builder.BuildAsync(AppointmentId)).Data.Patient;

        patient.FirstName.ShouldBe("Jordan");
        patient.MiddleName.ShouldBe("A");
        patient.LastName.ShouldBe("Sample");
        patient.DateOfBirth.ShouldBe("1985-04-12"); // date only, no time component
        patient.PhoneNumberType.ShouldBe("Home");
        patient.CellPhoneNumber.ShouldBe("555-0177");
    }

    [Fact]
    public async Task BuildAsync_ForAFirstEvaluation_SendsEvalAndNoPreviousLinks()
    {
        var builder = Build(NewAppointment(AppointmentId, "A00065"));

        var data = (await builder.BuildAsync(AppointmentId)).Data;

        data.EvaluationKind.ShouldBe("EVAL");
        data.PreviousAppointmentId.ShouldBeNull();
        data.PreviousConfirmationNumber.ShouldBeNull();
    }

    [Fact]
    public async Task BuildAsync_ForAReEvaluation_LinksBackToTheOriginal()
    {
        var reval = NewAppointment(
            AppointmentId, "A00065", EvaluationKind.ReEvaluation, SourceAppointmentId);
        var source = NewAppointment(SourceAppointmentId, "A00041");
        var builder = Build(reval, sourceAppointment: source);

        var data = (await builder.BuildAsync(AppointmentId)).Data;

        data.EvaluationKind.ShouldBe("RE_EVAL");
        data.PreviousAppointmentId.ShouldBe(SourceAppointmentId);
        // Display aid only -- the re-eval has its own fresh confirmation number.
        data.PreviousConfirmationNumber.ShouldBe("A00041");
    }

    [Fact]
    public async Task BuildAsync_OmitsNonFetchableDocuments()
    {
        var queued = AppointmentDocument.CreateQueued(
            Guid.NewGuid(), TenantId, AppointmentId, "Consent form", Guid.NewGuid());
        var uploaded = new AppointmentDocument(
            Guid.NewGuid(), TenantId, AppointmentId, "Medical records", "records.pdf",
            blobName: "tenantseg/apptseg/f97796c9365b4ad3a16408f72981cae3",
            contentType: "application/pdf", fileSize: 2048, uploadedByUserId: Guid.NewGuid())
        { Status = DocumentStatus.Accepted };

        var generating = new AppointmentPacket(
            Guid.NewGuid(), TenantId, AppointmentId, PacketKind.Doctor, "blob/doctor.pdf",
            PacketGenerationStatus.Generating);
        var generated = new AppointmentPacket(
            Guid.NewGuid(), TenantId, AppointmentId, PacketKind.Patient, "blob/patient.pdf",
            PacketGenerationStatus.Generated);

        var builder = Build(
            NewAppointment(AppointmentId, "A00065"),
            documents: new List<AppointmentDocument> { queued, uploaded },
            packets: new List<AppointmentPacket> { generating, generated });

        var documents = (await builder.BuildAsync(AppointmentId)).Data.Documents;

        // The queued placeholder and the still-rendering packet have no object to fetch.
        documents.Count.ShouldBe(2);
        documents.ShouldContain(d => d.Id == uploaded.Id);
        documents.ShouldContain(d => d.Id == generated.Id);
        documents.ShouldNotContain(d => d.Id == queued.Id);
        documents.ShouldNotContain(d => d.Id == generating.Id);
    }

    [Fact]
    public async Task BuildAsync_AtIntakeTime_HasAnEmptyDocumentList()
    {
        // Packets render asynchronously after approval, so the first push normally carries none.
        var builder = Build(NewAppointment(AppointmentId, "A00065"));

        (await builder.BuildAsync(AppointmentId)).Data.Documents.ShouldBeEmpty();
    }

    [Fact]
    public async Task SerializedPayload_IsCamelCase_AndCarriesNoPatientIdentifier()
    {
        var builder = Build(NewAppointment(AppointmentId, "A00065"));
        var envelope = await builder.BuildAsync(AppointmentId);

        var json = IntakePayloadSerializer.Serialize(envelope);

        json.ShouldContain("\"appointmentId\"");
        json.ShouldContain("\"evaluationKind\"");
        json.ShouldContain("\"dateOfBirth\"");
        json.ShouldContain("\"facilityId\"");
        // No patient identifier of any kind may appear -- see the class remarks.
        json.ShouldNotContain("\"patientId\"");
        json.ShouldNotContain("\"calMed\"");
    }

    [Fact]
    public async Task SerializedPayload_NeverCarriesTheRawPatientRowKey()
    {
        // Part 6 added a same-person hint. It MUST be the office-salted hash, never Patient.Id -- our row
        // key means nothing in CalMed's world and would invite something downstream to store it as a
        // patient identifier. This is the guard that keeps that decision honest.
        var builder = Build(NewAppointment(AppointmentId, "A00065"));
        var envelope = await builder.BuildAsync(AppointmentId);

        var json = IntakePayloadSerializer.Serialize(envelope);

        json.ShouldNotContain(PatientId.ToString("D"), Case.Insensitive);
        json.ShouldNotContain(PatientId.ToString("N"), Case.Insensitive);
        json.ShouldNotContain("\"portalPatientId\"");
        envelope.Data.Patient.SamePersonGroupKey
            .ShouldBe(SamePersonGroupKey.Compute(TenantId, PatientId));
    }

    [Fact]
    public async Task BuildAsync_PublishesEveryInjuryWithRawAndNormalizedIdentifiers()
    {
        var injury = new AppointmentInjuryDetailWithNavigationProperties
        {
            AppointmentInjuryDetail = new AppointmentInjuryDetail(
                new Guid("9f2c4b71-8ad3-4e15-b6c2-7e1f0a3d5b48"),
                AppointmentId,
                dateOfInjury: new DateTime(2025, 11, 14, 0, 0, 0, DateTimeKind.Utc),
                claimNumber: "wc-sample-a",
                isCumulativeInjury: false,
                bodyPartsSummary: "Lower back",
                wcabAdj: "adj-sample-a"),
        };

        var builder = Build(
            NewAppointment(AppointmentId, "A00065"),
            injuries: new List<AppointmentInjuryDetailWithNavigationProperties> { injury });

        var data = (await builder.BuildAsync(AppointmentId)).Data;

        var entry = data.Injuries.ShouldHaveSingleItem();
        entry.ClaimNumber.ShouldBe("wc-sample-a");
        entry.ClaimNumberNormalized.ShouldBe("WCSAMPLEA");
        entry.WcabAdjNormalized.ShouldBe("ADJSAMPLEA");
        entry.DateOfInjury.ShouldBe("2025-11-14");
    }

    [Fact]
    public async Task BuildAsync_WithNoClaimOrPartyData_ReturnsEmptyCollectionsAndNullAttorneys()
    {
        // The receiver renders "no claim information recorded" from this, so it must not be null.
        var builder = Build(NewAppointment(AppointmentId, "A00065"));

        var data = (await builder.BuildAsync(AppointmentId)).Data;

        data.Injuries.ShouldNotBeNull();
        data.Injuries.ShouldBeEmpty();
        data.PrimaryInsurances.ShouldBeEmpty();
        data.ClaimExaminers.ShouldBeEmpty();
        data.ApplicantAttorney.ShouldBeNull();
        data.DefenseAttorney.ShouldBeNull();
    }

    [Fact]
    public async Task PreExistingFields_AreUnchangedByThePart6Refactor()
    {
        // BuildAsync was split in two to stay under the method-length cap; this guards that the split
        // did not drop or reorder any field Parts 1-4 already publish.
        var builder = Build(NewAppointment(AppointmentId, "A00065"));

        var data = (await builder.BuildAsync(AppointmentId)).Data;

        data.AppointmentId.ShouldBe(AppointmentId);
        data.ConfirmationNumber.ShouldBe("A00065");
        data.Status.ShouldBe("Approved");
        data.Tenant.FacilityId.ShouldBe("FAC-SAMPLE");
        data.Location.Name.ShouldBe("North Clinic");
        data.Storage.Bucket.ShouldBe("case-evaluation-documents");
        data.Patient.LastName.ShouldBe("Sample");
        data.Doctor.LastName.ShouldBe("Reyes");
        data.AppointmentDateLocal.ShouldBe("2026-08-15");
        data.DurationMinutes.ShouldBe(60);
    }

    [Fact]
    public async Task WhenTheOfficeHasNoDoctor_TheIdIsNullRatherThanAnEmptyGuid()
    {
        // Null says "no doctor on file". Guid.Empty would look like a real identifier the receiver
        // could try to map, and their matcher now keys on this field.
        var builder = Build(NewAppointment(AppointmentId, "A00065"), withDoctor: false);

        var data = (await builder.BuildAsync(AppointmentId)).Data;

        data.Doctor.Id.ShouldBeNull();
        data.Doctor.FirstName.ShouldBeEmpty();
        data.Doctor.LastName.ShouldBeEmpty();
    }
}
