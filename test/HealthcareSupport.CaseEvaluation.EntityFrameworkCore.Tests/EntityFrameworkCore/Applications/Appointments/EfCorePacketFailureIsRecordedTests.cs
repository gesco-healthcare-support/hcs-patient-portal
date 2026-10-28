using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Jobs;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Pdf;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-10-27: a packet that cannot be rendered is RECORDED, on the real unit of work. The packet job runs in
/// one unit of work; it used to mark the kind Failed and then throw so Hangfire would retry, and the throw
/// rolled back the Failed row together with the kinds just Generated. Staff saw "no packets yet" with no
/// error, and the Case Tracker stalled-set release, which looks for a non-Generated row, never fired.
///
/// <para>Stand-ins: the packet-renderer sidecar (a stub that fails the attorney template only), the packets
/// blob container (in memory), the token resolver and the background job queue. The job, its unit of work,
/// the packet manager and the database are the production code. Values are synthetic.</para>
/// </summary>
public class EfCorePacketFailureIsRecordedTests
    : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private static readonly Guid BookerId = IdentityUsersTestData.ApplicantAttorney1UserId;

    private readonly MemoryPacketContainer _files = new();
    private readonly IHtmlPacketRenderer _renderer = Substitute.For<IHtmlPacketRenderer>();
    private readonly IBackgroundJobManager _jobs = Substitute.For<IBackgroundJobManager>();

    private readonly GenerateAppointmentPacketJob _packetJob;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<AppointmentPacket, Guid> _packets;
    private readonly INotificationTemplateRepository _templates;
    private readonly INotificationTemplateTypeRepository _templateTypes;
    private readonly ICurrentTenant _currentTenant;

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IBlobContainer<AppointmentPacketsContainer>>(_files));
        services.Replace(ServiceDescriptor.Singleton(_renderer));
        services.Replace(ServiceDescriptor.Singleton(_jobs));
        var tokens = Substitute.For<IPacketTokenResolver>();
        tokens.ResolveAsync(Arg.Any<Guid>()).Returns(new PacketTokenContext { AppointmentType = "TEST AME" });
        services.Replace(ServiceDescriptor.Singleton(tokens));
    }

    public EfCorePacketFailureIsRecordedTests()
    {
        _renderer.RenderAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Encoding.ASCII.GetBytes("%PDF-TEST-ok"));
        _renderer.RenderAsync(PacketTemplateNames.AttorneyAme, Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("TEST attorney template render failure"));

        _packetJob = GetRequiredService<GenerateAppointmentPacketJob>();
        _appointmentRepository = GetRequiredService<IAppointmentRepository>();
        _patientRepository = GetRequiredService<IRepository<Patient, Guid>>();
        _packets = GetRequiredService<IRepository<AppointmentPacket, Guid>>();
        _templates = GetRequiredService<INotificationTemplateRepository>();
        _templateTypes = GetRequiredService<INotificationTemplateTypeRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task A_kind_that_fails_to_render_is_kept_as_Failed_with_its_error_and_the_others_are_kept_Generated()
    {
        var id = await CreateApprovedAppointmentAsync(Guid.NewGuid().ToString("N")[..8]);

        // The job completes: the failure is a rendering error, not a transport one, so a retry would not help.
        await _packetJob.ExecuteAsync(new GenerateAppointmentPacketArgs { AppointmentId = id, TenantId = TenantsTestData.TenantARef });

        var rows = await InOfficeAsync(() => _packets.GetListAsync(p => p.AppointmentId == id));
        rows.Count.ShouldBe(3, "every kind has a row after the unit of work commits");
        rows.Single(p => p.Kind == PacketKind.Patient).Status.ShouldBe(PacketGenerationStatus.Generated);
        rows.Single(p => p.Kind == PacketKind.Doctor).Status.ShouldBe(PacketGenerationStatus.Generated);

        var failed = rows.Single(p => p.Kind == PacketKind.AttorneyClaimExaminer);
        failed.Status.ShouldBe(PacketGenerationStatus.Failed, "staff see the failure and can regenerate it");
        failed.ErrorMessage.ShouldNotBeNull();
        failed.ErrorMessage!.ShouldContain("TEST attorney template render failure");
    }

    private Task<T> InOfficeAsync<T>(Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await action();
            }
        });

    private async Task<Guid> CreateApprovedAppointmentAsync(string token)
    {
        var appointmentId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var email = $"pt-pf-{token}@example.test";
        await InOfficeAsync(async () =>
        {
            await SeedTemplatesAsync();
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
                phoneNumberTypeId: (PhoneNumberType)PatientsTestData.PatientPhoneNumberTypeIdValue), autoSave: true);

            var appointment = new Appointment(
                id: appointmentId,
                patientId: patientId,
                identityUserId: BookerId,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                locationId: LocationsTestData.Location1Id,
                doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot1Id,
                appointmentDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                requestConfirmationNumber: $"A9-PF-{token}",
                appointmentStatus: AppointmentStatusType.Approved)
            {
                TenantId = TenantsTestData.TenantARef,
                PatientEmail = email,
            };
            appointment.RecordBookedBy(BookerId);
            await _appointmentRepository.InsertAsync(appointment, autoSave: true);
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
