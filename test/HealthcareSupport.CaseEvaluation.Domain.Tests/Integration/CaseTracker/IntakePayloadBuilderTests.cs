using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Branding;
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

    private static readonly Guid TenantId = new("0ff1ce00-0000-4000-8000-000000000001");
    private static readonly Guid AppointmentId = new("ada5e3c5-0034-ebde-253c-3a2293631dee");
    private static readonly Guid SourceAppointmentId = new("3c9d1b77-2e40-4a51-8bb2-77f0a1c9d233");
    private static readonly Guid AppointmentTypeId = new("a1c2e3f4-5566-4778-9900-aabbccddeeff");
    private static readonly Guid LocationId = new("c0ffee0a-bcde-4f01-9abc-de0123456f7a");
    private static readonly Guid SlotId = new("d1e2f3a4-b5c6-4d7e-8f90-a1b2c3d4e5fa");
    private static readonly Guid PatientId = new("e5f6a7b8-c9d0-4e1f-a2b3-c4d5e6f7a8bc");
    private static readonly Guid DoctorId = new("f7a8b9c0-d1e2-4f30-a415-263748596a7b");
    private static readonly Guid OtherOfficeId = new("0ff1ce00-0000-4000-8000-000000000002");
    private static readonly Guid EmployerStateId = new("5a7e0000-0000-4000-8000-0000000000ca");

    private static Appointment NewAppointment(
        Guid id,
        string confirmation,
        EvaluationKind kind = EvaluationKind.Evaluation,
        Guid? originalAppointmentId = null,
        AppointmentStatusType status = AppointmentStatusType.Approved,
        string? cancellationReason = null,
        Guid? rescheduledFromAppointmentId = null)
    {
        return new Appointment(
            id, PatientId, identityUserId: null, AppointmentTypeId, LocationId, SlotId,
            appointmentDate: new DateTime(2026, 8, 15, 9, 30, 0, DateTimeKind.Utc),
            requestConfirmationNumber: confirmation,
            appointmentStatus: status,
            panelNumber: SyntheticPanelNumber)
        {
            TenantId = TenantId,
            EvaluationKind = kind,
            OriginalAppointmentId = originalAppointmentId,
            AppointmentApproveDate = new DateTime(2026, 7, 27, 18, 30, 12, DateTimeKind.Utc),
            CancellationReason = cancellationReason,
            RescheduledFromAppointmentId = rescheduledFromAppointmentId,
        };
    }

    private static IntakePayloadBuilder Build(
        Appointment appointment,
        Appointment? sourceAppointment = null,
        List<AppointmentDocument>? documents = null,
        List<AppointmentPacket>? packets = null,
        List<AppointmentInjuryDetailWithNavigationProperties>? injuries = null,
        bool withDoctor = true,
        Appointment? successor = null,
        OfficeLetterheadResolver? letterheadResolver = null,
        ICurrentTenant? currentTenant = null,
        List<AppointmentEmployerDetail>? employers = null)
    {
        var appointmentRepo = Substitute.For<IRepository<Appointment, Guid>>();
        appointmentRepo.GetAsync(appointment.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(appointment));
        appointmentRepo.FindAsync(SourceAppointmentId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(sourceAppointment));

        // Phase 4e: the FORWARD reschedule link is stored on the successor, so the resolver asks by
        // predicate rather than by id. It uses GetListAsync because FirstOrDefaultAsync is an
        // EXTENSION method and therefore unsubstitutable -- arranging it compiles, silently does
        // nothing, and the real extension runs against this substitute instead.
        appointmentRepo.GetListAsync(
                Arg.Any<Expression<Func<Appointment, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                successor == null ? new List<Appointment>() : new List<Appointment> { successor }));

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
        stateRepo.FindAsync(EmployerStateId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<State?>(new State(EmployerStateId, "California")));

        var employerRepo = Substitute.For<IRepository<AppointmentEmployerDetail, Guid>>();
        employerRepo.GetListAsync(
                Arg.Any<Expression<Func<AppointmentEmployerDetail, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(employers ?? new List<AppointmentEmployerDetail>()));

        return new IntakePayloadBuilder(
            appointmentRepo,
            new AppointmentCoreResolver(appointmentRepo, slotRepo, typeRepo),
            new PartyResolver(new AppointmentPatientSnapshotResolver(patientRepo), doctorRepo, stateRepo),
            new TenantLocationResolver(
                locationRepo, tenantStore, configuration,
                letterheadResolver ?? LetterheadResolverReturning(SyntheticLetterhead()),
                currentTenant ?? new SwitchableTenant(TenantId).Tenant),
            new DocumentListResolver(documentRepo, packetRepo, documentTypeRepo),
            new InjuryResolver(injuryRepo),
            new PartyDetailResolver(insuranceRepo, examinerRepo, stateRepo),
            new EmployerResolver(employerRepo, stateRepo),
            new ChangeAttributionResolver(EmptyChangeRequestRepo()),
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

    // Phase 2 (2026-07-31): billing intent used to be implicit in the status string and the
    // cancellation reason was not sent at all -- the Case Tracker could not tell whether to bill a
    // cancelled case, nor why it was cancelled.
    [Fact]
    public async Task BuildAsync_ForAnApprovedAppointment_SendsNoBillingIntentAndNoReason()
    {
        var builder = Build(NewAppointment(AppointmentId, "A00065"));

        var data = (await builder.BuildAsync(AppointmentId)).Data;

        // Always present, so the receiver never distinguishes "absent" from "nothing to bill".
        data.BillingStatus.ShouldBe("NONE");
        data.CancellationReason.ShouldBeNull();
    }

    [Theory]
    [InlineData(AppointmentStatusType.CancelledNoBill, "NO_BILL")]
    [InlineData(AppointmentStatusType.CancelledLate, "LATE")]
    public async Task BuildAsync_ForACancelledAppointment_SendsTheReasonAndBillingIntent(
        AppointmentStatusType status,
        string expectedBillingStatus)
    {
        var builder = Build(NewAppointment(
            AppointmentId, "A00065", status: status, cancellationReason: "Patient moved out of state"));

        var data = (await builder.BuildAsync(AppointmentId)).Data;

        data.Status.ShouldBe(status.ToString());
        data.BillingStatus.ShouldBe(expectedBillingStatus);
        data.CancellationReason.ShouldBe("Patient moved out of state");
    }

    [Fact]
    public async Task BuildAsync_WhenNoReasonWasRecorded_SendsNullRatherThanEmptyString()
    {
        // Null distinguishes "no reason recorded" from "reason recorded as blank", which matters
        // because the receiver renders this field to staff.
        var builder = Build(NewAppointment(
            AppointmentId, "A00065", status: AppointmentStatusType.CancelledNoBill));

        var data = (await builder.BuildAsync(AppointmentId)).Data;

        data.CancellationReason.ShouldBeNull();
        data.BillingStatus.ShouldBe("NO_BILL");
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
        // The RESCHEDULE chain is a separate pair and stays empty on a re-evaluation. This is the
        // assertion that stops the two link kinds being conflated again.
        data.RescheduledFromAppointmentId.ShouldBeNull();
        data.SupersededByAppointmentId.ShouldBeNull();
    }

    // ---- Phase 4e (2026-08-06): the reschedule chain ----

    [Fact]
    public async Task BuildAsync_ForAReplacement_LinksBackToTheAppointmentItReplaced()
    {
        var replacement = NewAppointment(
            AppointmentId, "A00065", rescheduledFromAppointmentId: SourceAppointmentId);
        var replaced = NewAppointment(
            SourceAppointmentId, "A00041", status: AppointmentStatusType.RescheduledNoBill);
        var builder = Build(replacement, sourceAppointment: replaced);

        var data = (await builder.BuildAsync(AppointmentId)).Data;

        data.RescheduledFromAppointmentId.ShouldBe(SourceAppointmentId);
        data.RescheduledFromConfirmationNumber.ShouldBe("A00041");
        // A replacement is still a FIRST evaluation -- rescheduling does not make it a re-eval.
        data.EvaluationKind.ShouldBe("EVAL");
        data.PreviousAppointmentId.ShouldBeNull();
        // It is the successor, so nothing superseded IT.
        data.SupersededByAppointmentId.ShouldBeNull();
        data.SupersededReason.ShouldBeNull();
    }

    [Theory]
    [InlineData(AppointmentStatusType.RescheduledNoBill, "NO_BILL")]
    [InlineData(AppointmentStatusType.RescheduledLate, "LATE")]
    public async Task BuildAsync_ForAReplacedAppointment_PointsForwardAndSaysWhy(
        AppointmentStatusType status,
        string expectedBillingStatus)
    {
        var replaced = NewAppointment(AppointmentId, "A00041", status: status);
        var replacement = NewAppointment(
            SourceAppointmentId, "A00065", rescheduledFromAppointmentId: AppointmentId);
        var builder = Build(replaced, successor: replacement);

        var data = (await builder.BuildAsync(AppointmentId)).Data;

        data.SupersededByAppointmentId.ShouldBe(SourceAppointmentId);
        data.SupersededReason.ShouldBe("RESCHEDULED");
        // The billing signal that lets Case Tracker close or bill the old date. It needed no new
        // code -- only for 4d's suppression to be removed.
        data.BillingStatus.ShouldBe(expectedBillingStatus);
        data.Status.ShouldBe(status.ToString());
    }

    [Fact]
    public async Task BuildAsync_ForAnOrdinaryAppointment_SendsNoRescheduleChainAtAll()
    {
        var builder = Build(NewAppointment(AppointmentId, "A00065"));

        var data = (await builder.BuildAsync(AppointmentId)).Data;

        data.RescheduledFromAppointmentId.ShouldBeNull();
        data.RescheduledFromConfirmationNumber.ShouldBeNull();
        data.SupersededByAppointmentId.ShouldBeNull();
        data.SupersededReason.ShouldBeNull();
    }

    /// <summary>
    /// The contract change IS the wire shape, so this asserts the serialized JSON rather than the
    /// object: a field renamed in C# without updating the contract would pass an object-level
    /// assertion and still break the receiver.
    /// </summary>
    [Fact]
    public async Task BuildAsync_SerializesTheRescheduleChainWithTheContractedNames()
    {
        var replacement = NewAppointment(
            AppointmentId, "A00065", rescheduledFromAppointmentId: SourceAppointmentId);
        var replaced = NewAppointment(
            SourceAppointmentId, "A00041", status: AppointmentStatusType.RescheduledNoBill);
        var builder = Build(replacement, sourceAppointment: replaced);

        var json = IntakePayloadSerializer.Serialize(await builder.BuildAsync(AppointmentId));

        json.ShouldContain($"\"rescheduledFromAppointmentId\":\"{SourceAppointmentId}\"");
        json.ShouldContain("\"rescheduledFromConfirmationNumber\":\"A00041\"");
        // Nulls are deliberately NOT omitted (IntakePayloadSerializer), so the forward pair is
        // present and explicitly null rather than absent.
        json.ShouldContain("\"supersededByAppointmentId\":null");
        json.ShouldContain("\"supersededReason\":null");
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

    // 2026-11-15: data.office carries the office's letterhead facts exactly as the patient packet
    // prints them, so the Case Tracker can print the same address on its own letters.
    [Fact]
    public async Task BuildAsync_SendsTheOfficeLetterheadAsThePacketPrintsIt()
    {
        var builder = Build(NewAppointment(AppointmentId, "A00065"));

        var office = (await builder.BuildAsync(AppointmentId)).Data.Office;

        office.PracticeName.ShouldBe("TEST Sample Spine Center");
        office.Phone.ShouldBe("(555) 010-0100");
        office.Fax.ShouldBe("(555) 010-0101");
        office.RecordsReleaseAddress.ShouldBe("TEST RECORDS DEPT.\n100 SAMPLE WAY, STE. 1\nSAMPLETOWN, CA 90000");
    }

    [Fact]
    public async Task BuildAsync_WhenTheLetterheadIsUnset_FallsBackForTheNameAndSendsNullForTheRest()
    {
        var unset = OfficeLetterhead.Compose(
            new OfficeLetterheadValues(), displayName: "TEST Display Practice", doctorFirstName: "Morgan", doctorLastName: "Reyes");
        var builder = Build(NewAppointment(AppointmentId, "A00065"), letterheadResolver: LetterheadResolverReturning(unset));

        var office = (await builder.BuildAsync(AppointmentId)).Data.Office;

        office.PracticeName.ShouldBe("TEST Display Practice", "the packet's own fallback: the office display name");
        office.RecordsReleaseAddress.ShouldBeNull();
        office.Phone.ShouldBeNull();
        office.Fax.ShouldBeNull();
    }

    [Fact]
    public async Task BuildAsync_ForAnAppointmentWithNoOffice_SendsAnAllNullOfficeAndReadsNoLetterhead()
    {
        var appointment = NewAppointment(AppointmentId, "A00065");
        appointment.TenantId = null;
        var resolver = LetterheadResolverReturning(SyntheticLetterhead());
        var builder = Build(appointment, letterheadResolver: resolver);

        var office = (await builder.BuildAsync(AppointmentId)).Data.Office;

        office.PracticeName.ShouldBeNull();
        office.RecordsReleaseAddress.ShouldBeNull();
        office.Phone.ShouldBeNull();
        office.Fax.ShouldBeNull();
        await resolver.DidNotReceive().ResolveAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The wire shape the Case Tracker reads: <c>data.office</c> with exactly four camelCase keys, a
    /// blank value present as JSON null (the serializer never omits nulls), the address rows joined
    /// by a bare newline, and nothing office-shaped in a document-update body.
    /// </summary>
    [Fact]
    public async Task SerializedPayload_CarriesDataOfficeWithExactlyItsFourKeys()
    {
        var letterhead = OfficeLetterhead.Compose(
            new OfficeLetterheadValues
            {
                PracticeName = "TEST Sample Spine Center",
                Phone = "(555) 010-0100",
                RecordsReleaseAddress = "TEST RECORDS DEPT.\r\n100 SAMPLE WAY",
            },
            displayName: null, doctorFirstName: "Morgan", doctorLastName: "Reyes");
        var builder = Build(NewAppointment(AppointmentId, "A00065"), letterheadResolver: LetterheadResolverReturning(letterhead));
        var envelope = await builder.BuildAsync(AppointmentId);

        using var json = System.Text.Json.JsonDocument.Parse(IntakePayloadSerializer.Serialize(envelope));
        var office = json.RootElement.GetProperty("data").GetProperty("office");

        office.EnumerateObject().Select(p => p.Name)
            .ShouldBe(new[] { "practiceName", "recordsReleaseAddress", "phone", "fax" }, ignoreOrder: true);
        office.GetProperty("practiceName").GetString().ShouldBe("TEST Sample Spine Center");
        office.GetProperty("recordsReleaseAddress").GetString().ShouldBe("TEST RECORDS DEPT.\n100 SAMPLE WAY");
        office.GetProperty("phone").GetString().ShouldBe("(555) 010-0100");
        office.GetProperty("fax").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);

        IntakePayloadSerializer.SerializeDocumentEntries(envelope.Data.Documents)
            .ShouldNotContain("office", Case.Insensitive);
    }

    // ----------------------------------------------------------------- data.employer (2026-11-23)

    /// <summary>
    /// The Case Tracker reads EXACTLY these seven keys (agreed 2026-11-23). A rename here is a contract break on
    /// their side, so this pins the set: renaming, adding or dropping a property fails CI.
    /// </summary>
    [Fact]
    public async Task SerializedPayload_CarriesDataEmployerWithExactlyItsSevenKeys()
    {
        var row = Employer("TEST Acme Logistics", new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), stateId: EmployerStateId);
        var builder = Build(NewAppointment(AppointmentId, "A00065"), employers: new List<AppointmentEmployerDetail> { row });

        using var json = System.Text.Json.JsonDocument.Parse(IntakePayloadSerializer.Serialize(await builder.BuildAsync(AppointmentId)));
        var employer = json.RootElement.GetProperty("data").GetProperty("employer");

        employer.EnumerateObject().Select(p => p.Name).ShouldBe(
            new[] { "name", "occupation", "phoneNumber", "street", "city", "state", "zipCode" }, ignoreOrder: true);
        employer.GetProperty("name").GetString().ShouldBe("TEST Acme Logistics");
        employer.GetProperty("occupation").GetString().ShouldBe("TEST Warehouse Lead");
        employer.GetProperty("phoneNumber").GetString().ShouldBe("(555) 010-0142");
        employer.GetProperty("street").GetString().ShouldBe("100 Sample Way");
        employer.GetProperty("city").GetString().ShouldBe("Encino");
        employer.GetProperty("state").GetString().ShouldBe("California", "the State NAME, never its id");
        employer.GetProperty("zipCode").GetString().ShouldBe("91316");
    }

    /// <summary>
    /// No employer row: the key is still on the wire, as an explicit null. The Case Tracker reads null as "none on
    /// record" and clears what it holds, so an omitted key would leave a stale employer on their side.
    /// </summary>
    [Fact]
    public async Task SerializedPayload_CarriesEmployerNull_WhenTheAppointmentHasNoEmployerRow()
    {
        var builder = Build(NewAppointment(AppointmentId, "A00065"));

        var envelope = await builder.BuildAsync(AppointmentId);
        var body = IntakePayloadSerializer.Serialize(envelope);

        envelope.Data.Employer.ShouldBeNull();
        using var json = System.Text.Json.JsonDocument.Parse(body);
        json.RootElement.GetProperty("data").GetProperty("employer").ValueKind
            .ShouldBe(System.Text.Json.JsonValueKind.Null);
        body.ShouldContain("\"employer\":null");
    }

    /// <summary>
    /// More than one live row is reachable (no unique index). The NEWEST by creation time is sent -- the row the
    /// edit screens load and save -- with the id as a tie-break, so the choice never depends on query order.
    /// </summary>
    [Fact]
    public async Task BuildAsync_Employer_IsTheNewestRow_WithTheIdBreakingATie()
    {
        var older = Employer("TEST Older Employer", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        var tiedLow = Employer("TEST Tied Low Id", new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            id: new Guid("00000000-0000-4000-8000-000000000001"));
        var tiedHigh = Employer("TEST Tied High Id", new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            id: new Guid("ffffffff-0000-4000-8000-000000000001"));

        foreach (var order in new[] { new[] { older, tiedLow, tiedHigh }, new[] { tiedHigh, older, tiedLow } })
        {
            var builder = Build(NewAppointment(AppointmentId, "A00065"), employers: order.ToList());

            (await builder.BuildAsync(AppointmentId)).Data.Employer!.Name.ShouldBe("TEST Tied High Id");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BuildAsync_EmployerState_IsNull_WhenTheRowHasNoStateOrItDoesNotResolve(bool unresolvable)
    {
        var row = Employer("TEST Acme Logistics", new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            stateId: unresolvable ? Guid.NewGuid() : null);
        var builder = Build(NewAppointment(AppointmentId, "A00065"), employers: new List<AppointmentEmployerDetail> { row });

        (await builder.BuildAsync(AppointmentId)).Data.Employer!.State.ShouldBeNull();
    }

    private static AppointmentEmployerDetail Employer(string name, DateTime createdUtc, Guid? id = null, Guid? stateId = null) =>
        new(id ?? Guid.NewGuid(), AppointmentId, stateId, name, "TEST Warehouse Lead")
        {
            PhoneNumber = "(555) 010-0142",
            Street = "100 Sample Way",
            City = "Encino",
            ZipCode = "91316",
            CreationTime = createdUtc,
        };

    /// <summary>
    /// The letterhead is read in the APPOINTMENT's office, not whichever office is current. A
    /// mismatched caller (another office, or the host) must still publish this appointment's own
    /// practice and address, never another office's and never the empty host defaults. Afterwards
    /// the caller's scope is restored.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BuildAsync_ReadsTheLetterheadOfTheAppointmentsOffice_NotTheCurrentOne(bool callerIsInAnotherOffice)
    {
        Guid? callerScope = callerIsInAnotherOffice ? OtherOfficeId : null;
        var tenant = new SwitchableTenant(callerScope);
        var otherOffice = OfficeLetterhead.Compose(
            new OfficeLetterheadValues { PracticeName = "TEST Other Office Clinic", Phone = "(555) 010-0199" },
            displayName: null, doctorFirstName: "Avery", doctorLastName: "Other");
        var resolver = Substitute.For<OfficeLetterheadResolver>(
            Substitute.For<IReadOnlyRepository<Doctor, Guid>>(),
            Substitute.For<IReadOnlyRepository<OfficeBranding, Guid>>(),
            tenant.Tenant);
        resolver.ResolveAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(
            tenant.Tenant.Id == TenantId ? SyntheticLetterhead()
            : tenant.Tenant.Id == OtherOfficeId ? otherOffice
            : OfficeLetterhead.Compose(null, null, null, null)));
        var builder = Build(NewAppointment(AppointmentId, "A00065"), letterheadResolver: resolver, currentTenant: tenant.Tenant);

        var office = (await builder.BuildAsync(AppointmentId)).Data.Office;

        office.PracticeName.ShouldBe("TEST Sample Spine Center");
        office.Phone.ShouldBe("(555) 010-0100");
        tenant.Tenant.Id.ShouldBe(callerScope, "the caller's own scope is restored afterwards");
    }

    /// <summary>An <see cref="ICurrentTenant"/> whose <c>Change</c> really switches and restores <c>Id</c>.</summary>
    private sealed class SwitchableTenant
    {
        private Guid? _id;

        public SwitchableTenant(Guid? id)
        {
            _id = id;
            Tenant.Id.Returns(_ => _id);
            Tenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(ci =>
            {
                var previous = _id;
                _id = ci.ArgAt<Guid?>(0);
                return new Restore(() => _id = previous);
            });
        }

        public ICurrentTenant Tenant { get; } = Substitute.For<ICurrentTenant>();
    }

    private sealed class Restore(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }

    /// <summary>A synthetic letterhead with every field the Case Tracker reads filled in.</summary>
    private static OfficeLetterhead SyntheticLetterhead() => OfficeLetterhead.Compose(
        new OfficeLetterheadValues
        {
            PracticeName = "TEST Sample Spine Center",
            Phone = "(555) 010-0100",
            Fax = "(555) 010-0101",
            RecordsReleaseAddress = "TEST RECORDS DEPT.\n100 SAMPLE WAY, STE. 1\nSAMPLETOWN, CA 90000",
        },
        displayName: "TEST Display Practice",
        doctorFirstName: "Morgan",
        doctorLastName: "Reyes");

    /// <summary>
    /// The letterhead resolver with its read substituted: it is covered on its own by
    /// <c>OfficeLetterheadResolverTests</c>, and here only its result matters.
    /// </summary>
    private static OfficeLetterheadResolver LetterheadResolverReturning(OfficeLetterhead letterhead)
    {
        var resolver = Substitute.For<OfficeLetterheadResolver>(
            Substitute.For<IReadOnlyRepository<Doctor, Guid>>(),
            Substitute.For<IReadOnlyRepository<OfficeBranding, Guid>>(),
            Substitute.For<ICurrentTenant>());
        resolver.ResolveAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(letterhead));
        return resolver;
    }

    /// <summary>
    /// A change-request repository holding nothing. Configured EXPLICITLY: an unconfigured
    /// NSubstitute call returning Task&lt;List&lt;T&gt;&gt; yields NULL, not an empty list, which the
    /// resolver would then order over. Production repositories never return null.
    /// </summary>
    private static IRepository<AppointmentChangeRequest, Guid> EmptyChangeRequestRepo()
    {
        var repo = Substitute.For<IRepository<AppointmentChangeRequest, Guid>>();
        repo.GetListAsync(
                Arg.Any<System.Linq.Expressions.Expression<Func<AppointmentChangeRequest, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<AppointmentChangeRequest>()));
        return repo;
    }
}
