using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Jobs;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Pdf;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-11-24: the DWC Employee's Disability Questionnaire (DEU form 100) through the REAL packet token resolver and
/// the REAL packet job on the real database. An unrepresented Panel QME patient gets the <c>patient-deu</c> template,
/// filled from the records, as the one Patient-kind packet; a typed attorney email or an attorney link keeps the
/// standard patient packet.
///
/// <para>Stand-ins: the packet-renderer sidecar (a stub that records what it was asked to render), the packets and
/// office-signature blob containers, and the background job queue. Values are synthetic.</para>
/// </summary>
public class EfCoreDeuQuestionnairePacketTests
    : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private static readonly Guid BookerId = IdentityUsersTestData.ApplicantAttorney1UserId;

    private readonly MemoryPacketContainer _files = new();
    private readonly IHtmlPacketRenderer _renderer = Substitute.For<IHtmlPacketRenderer>();
    private readonly IBackgroundJobManager _jobs = Substitute.For<IBackgroundJobManager>();

    private readonly GenerateAppointmentPacketJob _packetJob;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<AppointmentEmployerDetail, Guid> _employers;
    private readonly IRepository<ApplicantAttorney, Guid> _applicantAttorneys;
    private readonly IRepository<AppointmentApplicantAttorney, Guid> _applicantLinks;
    private readonly IRepository<AppointmentPacket, Guid> _packets;
    private readonly IRepository<AppointmentType, Guid> _types;
    private readonly INotificationTemplateRepository _templates;
    private readonly INotificationTemplateTypeRepository _templateTypes;
    private readonly ICurrentTenant _currentTenant;

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IBlobContainer<AppointmentPacketsContainer>>(_files));
        services.Replace(ServiceDescriptor.Singleton(Substitute.For<IBlobContainer<OfficeSignaturesContainer>>()));
        services.Replace(ServiceDescriptor.Singleton(_renderer));
        services.Replace(ServiceDescriptor.Singleton(_jobs));
    }

    public EfCoreDeuQuestionnairePacketTests()
    {
        _renderer.RenderAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Encoding.ASCII.GetBytes("%PDF-TEST-ok"));

        _packetJob = GetRequiredService<GenerateAppointmentPacketJob>();
        _appointmentRepository = GetRequiredService<IAppointmentRepository>();
        _patientRepository = GetRequiredService<IRepository<Patient, Guid>>();
        _employers = GetRequiredService<IRepository<AppointmentEmployerDetail, Guid>>();
        _applicantAttorneys = GetRequiredService<IRepository<ApplicantAttorney, Guid>>();
        _applicantLinks = GetRequiredService<IRepository<AppointmentApplicantAttorney, Guid>>();
        _packets = GetRequiredService<IRepository<AppointmentPacket, Guid>>();
        _types = GetRequiredService<IRepository<AppointmentType, Guid>>();
        _templates = GetRequiredService<INotificationTemplateRepository>();
        _templateTypes = GetRequiredService<INotificationTemplateTypeRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task An_unrepresented_panel_qme_patient_gets_the_deu_template_filled_from_the_records()
    {
        var token = NewToken();
        var id = await CreateApprovedPanelQmeAsync(token, attorneyEmail: null, linkAttorney: false);

        await _packetJob.ExecuteAsync(new GenerateAppointmentPacketArgs { AppointmentId = id, TenantId = TenantsTestData.TenantARef });

        var (template, tokens) = PatientRender();
        template.ShouldBe(PacketTemplateNames.PatientDeu);
        tokens["##Deu.FirstName##"].ShouldBe($"TEST-{token}".ToUpperInvariant());
        tokens["##Deu.Ssn##"].ShouldBe("000112222");
        tokens["##Deu.Employer##"].ShouldBe($"TEST NEWER EMPLOYER {token}".ToUpperInvariant());
        tokens["##Deu.Claim1##"].ShouldBeEmpty("this appointment has no injury row, so the claim lines are fillable");

        var rows = await InOfficeAsync(() => _packets.GetListAsync(p => p.AppointmentId == id));
        rows.Count(p => p.Kind == PacketKind.Patient).ShouldBe(1, "the 14-page packet is still the one Patient packet");
        rows.Single(p => p.Kind == PacketKind.Patient).BlobName.ShouldContain($"/{id}/packet/patient/");
    }

    [Theory]
    [InlineData("aa@example.test", false)]
    [InlineData(null, true)]
    public async Task A_typed_attorney_email_or_an_attorney_link_keeps_the_standard_patient_packet(
        string? attorneyEmail, bool linkAttorney)
    {
        var id = await CreateApprovedPanelQmeAsync(NewToken(), attorneyEmail, linkAttorney);

        await _packetJob.ExecuteAsync(new GenerateAppointmentPacketArgs { AppointmentId = id, TenantId = TenantsTestData.TenantARef });

        PatientRender().Template.ShouldBe(PacketTemplateNames.Patient);
    }

    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>The one patient render the stub was asked for: its template and its tokens.</summary>
    private (string Template, IReadOnlyDictionary<string, string> Tokens) PatientRender()
    {
        var patientCalls = _renderer.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IHtmlPacketRenderer.RenderAsync))
            .Select(c => ((string)c.GetArguments()[0]!, (IReadOnlyDictionary<string, string>)c.GetArguments()[1]!))
            .Where(c => c.Item1 is PacketTemplateNames.Patient or PacketTemplateNames.PatientDeu)
            .ToList();
        patientCalls.Count.ShouldBe(1);
        return patientCalls[0];
    }

    private Task<T> InOfficeAsync<T>(Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await action();
            }
        });

    private async Task<Guid> CreateApprovedPanelQmeAsync(string token, string? attorneyEmail, bool linkAttorney)
    {
        var appointmentId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var email = $"pt-deu-{token}@example.test";
        await InOfficeAsync(async () =>
        {
            await SeedTemplatesAsync();
            // A Panel QME type of this office's own (the catalog is per office); the job matches "PANEL".
            var panelQme = await _types.InsertAsync(
                new AppointmentType(Guid.NewGuid(), $"TEST Panel Qualified Medical Examination {token} (PQME)"), autoSave: true);
            await _patientRepository.InsertAsync(new Patient(
                id: patientId,
                stateId: null,
                appointmentLanguageId: null,
                identityUserId: null,
                tenantId: TenantsTestData.TenantARef,
                firstName: $"TEST-{token}",
                lastName: $"TEST-{token}",
                email: email,
                genderId: (Gender)PatientsTestData.PatientGenderIdValue,
                dateOfBirth: PatientsTestData.FixedDateOfBirth,
                phoneNumberTypeId: (PhoneNumberType)PatientsTestData.PatientPhoneNumberTypeIdValue,
                socialSecurityNumber: "000-11-2222"), autoSave: true);

            var appointment = new Appointment(
                id: appointmentId,
                patientId: patientId,
                identityUserId: BookerId,
                appointmentTypeId: panelQme.Id,
                locationId: LocationsTestData.Location1Id,
                doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot1Id,
                appointmentDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                requestConfirmationNumber: $"A9-DEU-{token}",
                appointmentStatus: AppointmentStatusType.Approved)
            {
                TenantId = TenantsTestData.TenantARef,
                PatientEmail = email,
                ApplicantAttorneyEmail = attorneyEmail,
            };
            appointment.RecordBookedBy(BookerId);
            await _appointmentRepository.InsertAsync(appointment, autoSave: true);

            var at = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            await _employers.InsertAsync(new AppointmentEmployerDetail(Guid.NewGuid(), appointmentId, null,
                $"TEST Older Employer {token}", "TEST Role")
            { CreationTime = at }, autoSave: true);
            await _employers.InsertAsync(new AppointmentEmployerDetail(Guid.NewGuid(), appointmentId, null,
                $"TEST Newer Employer {token}", "TEST Role")
            { CreationTime = at.AddDays(1) }, autoSave: true);

            if (linkAttorney)
            {
                var attorney = await _applicantAttorneys.InsertAsync(
                    new ApplicantAttorney(Guid.NewGuid(), null, null, $"TEST Firm {token}"), autoSave: true);
                await _applicantLinks.InsertAsync(
                    new AppointmentApplicantAttorney(Guid.NewGuid(), appointmentId, attorney.Id, null), autoSave: true);
            }

            return true;
        });
        return appointmentId;
    }

    private async Task SeedTemplatesAsync()
    {
        // Reuse a template type if the seed left one in this office.
        var typeId = (await _templateTypes.GetListAsync()).FirstOrDefault()?.Id
            ?? (await _templateTypes.InsertAsync(new NotificationTemplateType(Guid.NewGuid(), "Email"), autoSave: true)).Id;

        var existing = (await _templates.GetQueryableAsync()).Select(x => x.TemplateCode).ToList();
        foreach (var code in NotificationTemplateConsts.Codes.All.Where(c => !existing.Contains(c)))
        {
            await _templates.InsertAsync(new NotificationTemplate(
                id: Guid.NewGuid(),
                tenantId: TenantsTestData.TenantARef,
                templateCode: code,
                templateTypeId: typeId,
                subject: $"[{code}] -- TEST",
                bodyEmail: $"<p>Stub for {code}</p>",
                bodySms: $"Stub for {code}",
                description: null,
                isActive: true), autoSave: true);
        }
    }

    /// <summary>The packets container in memory.</summary>
    private sealed class MemoryPacketContainer : IBlobContainer<AppointmentPacketsContainer>
    {
        private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);

        public async Task SaveAsync(string name, Stream stream, bool overrideExisting = false, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            _blobs[name] = ms.ToArray();
        }

        public Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(_blobs.TryRemove(name, out _));

        public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(_blobs.ContainsKey(name));

        public async Task<Stream> GetAsync(string name, CancellationToken cancellationToken = default) =>
            await GetOrNullAsync(name, cancellationToken) ?? throw new AbpException($"TEST: no blob {name}");

        public Task<Stream?> GetOrNullAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream?>(_blobs.TryGetValue(name, out var b) ? new MemoryStream(b) : null);
    }
}
