using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Settings;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications.Delivery;

/// <summary>
/// <see cref="OfficePacketCopyEmailHandler"/> on the real rig: the office inbox gets ONE email carrying the
/// attorney / claim examiner packet and the patient packet (never the doctor packet), once both are
/// generated, whichever finishes second. Only the packet-copy dispatcher is replaced, so each Fact sees the
/// recipients, the attachments and the context tag the outbox would dedupe on.
///
/// <para>Decoys, so "only the inbox" is shown against people who could have been included: an active Intake
/// user of the same office and a doctor packet that is Generated. Office B's inbox is set in the "no inbox"
/// Fact, to show office A never borrows another office's address. Every address is a synthetic test.local
/// one.</para>
/// </summary>
public class OfficePacketCopyEmailHandlerTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string InboxA = "TEST-copy-inbox-a@test.local";
    private const string InboxB = "TEST-copy-inbox-b@test.local";

    private readonly CopyRecorder _recorder = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IPacketCopyDispatcher>(_recorder));
    }

    [Fact]
    public async Task OnceBothPacketsAreGenerated_TheInboxGetsOneMailWithExactlyThosePackets_NotIntakeNorTheDoctorPacket()
    {
        await SetInboxAsync(TenantsTestData.TenantARef, InboxA);
        var intake = await SeedIntakeAsync();
        var packets = await SeedPacketsAsync(PacketGenerationStatus.Generated, PacketGenerationStatus.Generated, PacketGenerationStatus.Generated);

        await RaiseAsync(PacketKind.AttorneyClaimExaminer);

        var sent = _recorder.Sent.ShouldHaveSingleItem();
        sent.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.OfficePacketCopy);
        sent.Recipients.ShouldBe(new[] { InboxA });
        sent.Recipients.ShouldNotContain(intake);
        sent.Packets.Select(p => p.Kind).ShouldBe(new[] { PacketKind.AttorneyClaimExaminer, PacketKind.Patient });
        sent.Packets.Select(p => p.PacketId).ShouldBe(new[] { packets[PacketKind.AttorneyClaimExaminer], packets[PacketKind.Patient] });
        sent.Packets.ShouldAllBe(p => p.AppointmentId == AppointmentsTestData.Appointment1Id);
        sent.Packets.ShouldNotContain(p => p.Kind == PacketKind.Doctor);
        sent.ContextTag.ShouldBe($"OfficePacketCopy/{AppointmentsTestData.Appointment1Id}/v0");
        sent.Variables["AppointmentRequestConfirmationNumber"].ShouldNotBeNull();
    }

    [Fact]
    public async Task TheCopyWaitsForBothPackets_AndTheSecondToFinishSendsIt_AttorneyLast()
    {
        await SetInboxAsync(TenantsTestData.TenantARef, InboxA);
        await SeedPacketsAsync(PacketGenerationStatus.Generating, PacketGenerationStatus.Generated, PacketGenerationStatus.Generated);

        await RaiseAsync(PacketKind.Patient);
        _recorder.Sent.ShouldBeEmpty("the attorney packet is still rendering");

        await MarkGeneratedAsync(PacketKind.AttorneyClaimExaminer);
        await RaiseAsync(PacketKind.AttorneyClaimExaminer);
        _recorder.Sent.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task TheCopyWaitsForBothPackets_AndTheSecondToFinishSendsIt_PatientLast()
    {
        await SetInboxAsync(TenantsTestData.TenantARef, InboxA);
        await SeedPacketsAsync(PacketGenerationStatus.Generated, PacketGenerationStatus.Failed, PacketGenerationStatus.Generated);

        await RaiseAsync(PacketKind.AttorneyClaimExaminer);
        _recorder.Sent.ShouldBeEmpty("the patient packet has not been generated");

        await MarkGeneratedAsync(PacketKind.Patient);
        await RaiseAsync(PacketKind.Patient);
        _recorder.Sent.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task BothEventsOfOneSet_AskForTheSameCopy_SoTheOutboxCollapsesThemToOne()
    {
        await SetInboxAsync(TenantsTestData.TenantARef, InboxA);
        await SeedPacketsAsync(PacketGenerationStatus.Generated, PacketGenerationStatus.Generated, PacketGenerationStatus.Generated);

        await RaiseAsync(PacketKind.AttorneyClaimExaminer);
        var first = _recorder.Sent.Single().ContextTag;
        await RaiseAsync(PacketKind.Patient);

        _recorder.Sent.Single().ContextTag.ShouldBe(first, "the same set must carry the same tag, which the outbox collapses");
    }

    [Fact]
    public async Task AReRenderAfterAPartyChange_IsANewVersion_AndSoANewCopy()
    {
        await SetInboxAsync(TenantsTestData.TenantARef, InboxA);
        await SeedPacketsAsync(PacketGenerationStatus.Generated, PacketGenerationStatus.Generated, PacketGenerationStatus.Generated);
        await RaiseAsync(PacketKind.AttorneyClaimExaminer);
        var original = _recorder.Sent.Single().ContextTag;

        await RecordReplacedFileAsync(PacketKind.AttorneyClaimExaminer);
        await RaiseAsync(PacketKind.AttorneyClaimExaminer);

        var again = _recorder.Sent.ShouldHaveSingleItem();
        original.ShouldEndWith("/v0");
        again.ContextTag.ShouldBe($"OfficePacketCopy/{AppointmentsTestData.Appointment1Id}/v1");
    }

    [Fact]
    public async Task NoOfficeInbox_SendsNothing_AndNeverBorrowsAnotherOfficesAddress()
    {
        await SetInboxAsync(TenantsTestData.TenantBRef, InboxB);
        await SeedIntakeAsync();
        await SeedPacketsAsync(PacketGenerationStatus.Generated, PacketGenerationStatus.Generated, PacketGenerationStatus.Generated);

        await RaiseAsync(PacketKind.AttorneyClaimExaminer);

        _recorder.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task ADoctorPacketEvent_SendsNothing_EvenWithBothOtherPacketsGenerated()
    {
        // Positive control: the first Fact, the same rows, an attorney-packet event.
        await SetInboxAsync(TenantsTestData.TenantARef, InboxA);
        await SeedPacketsAsync(PacketGenerationStatus.Generated, PacketGenerationStatus.Generated, PacketGenerationStatus.Generated);

        await RaiseAsync(PacketKind.Doctor);

        _recorder.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnAppointmentWithoutAGeneratedAttorneyPacket_SendsNoCopy_AndAnUnknownAppointmentSendsNothing()
    {
        await SetInboxAsync(TenantsTestData.TenantARef, InboxA);
        await SeedPacketsAsync(null, PacketGenerationStatus.Generated, PacketGenerationStatus.Generated);

        await RaiseAsync(PacketKind.Patient);
        await RaiseAsync(PacketKind.Patient, appointmentId: Guid.NewGuid());

        _recorder.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task NullEvent_SendsNothing()
    {
        _recorder.Clear();

        await WithUnitOfWorkAsync(() => GetRequiredService<OfficePacketCopyEmailHandler>().HandleEventAsync(null!));

        _recorder.Sent.ShouldBeEmpty();
    }

    // ------------------------------------------------------------------------

    private async Task RaiseAsync(PacketKind kind, Guid? appointmentId = null)
    {
        _recorder.Clear();
        await WithUnitOfWorkAsync(() => GetRequiredService<OfficePacketCopyEmailHandler>().HandleEventAsync(new PacketGeneratedEto
        {
            AppointmentId = appointmentId ?? AppointmentsTestData.Appointment1Id,
            TenantId = TenantsTestData.TenantARef,
            PacketId = Guid.NewGuid(),
            Kind = kind,
            OccurredAt = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc),
        }));
    }

    /// <summary>
    /// Writes the three packet rows of appointment 1 in office A; a null status leaves that kind out (no
    /// attorney packet row yet). Returns each kind's packet id.
    /// </summary>
    private async Task<Dictionary<PacketKind, Guid>> SeedPacketsAsync(
        PacketGenerationStatus? attyCe, PacketGenerationStatus patient, PacketGenerationStatus doctor)
    {
        var ids = new Dictionary<PacketKind, Guid>();
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                var repository = GetRequiredService<IRepository<AppointmentPacket, Guid>>();
                foreach (var (kind, status) in new (PacketKind, PacketGenerationStatus?)[]
                {
                    (PacketKind.AttorneyClaimExaminer, attyCe),
                    (PacketKind.Patient, patient),
                    (PacketKind.Doctor, doctor),
                })
                {
                    if (status == null)
                    {
                        continue;
                    }
                    var packet = new AppointmentPacket(
                        Guid.NewGuid(), TenantsTestData.TenantARef, AppointmentsTestData.Appointment1Id,
                        kind, $"TEST-copy-{kind}-{Guid.NewGuid():N}.pdf", status.Value);
                    await repository.InsertAsync(packet, autoSave: true);
                    ids[kind] = packet.Id;
                }
            }
        });
        return ids;
    }

    private Task MarkGeneratedAsync(PacketKind kind) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            var repository = GetRequiredService<IRepository<AppointmentPacket, Guid>>();
            var packet = (await repository.GetListAsync(p => p.AppointmentId == AppointmentsTestData.Appointment1Id && p.Kind == kind)).Single();
            packet.Status = PacketGenerationStatus.Generated;
            await repository.UpdateAsync(packet, autoSave: true);
        }
    });

    /// <summary>What a re-render leaves behind: a version row for the file the packet used to point at.</summary>
    private Task RecordReplacedFileAsync(PacketKind kind) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            var packet = (await GetRequiredService<IRepository<AppointmentPacket, Guid>>()
                .GetListAsync(p => p.AppointmentId == AppointmentsTestData.Appointment1Id && p.Kind == kind)).Single();
            await GetRequiredService<IRepository<AppointmentPacketVersion, Guid>>()
                .InsertAsync(new AppointmentPacketVersion(Guid.NewGuid(), packet, replacedByUserId: null), autoSave: true);
        }
    });

    private Task SetInboxAsync(Guid officeId, string inbox) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(officeId))
        {
            await GetRequiredService<ISettingManager>().SetForCurrentTenantAsync(
                CaseEvaluationSettings.NotificationsPolicy.OfficeEmail, inbox);
        }
    });

    private async Task<string> SeedIntakeAsync()
    {
        var email = string.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                (email, _) = await StaffSeeder.CreateAsync(
                    GetRequiredService<IdentityUserManager>(), GetRequiredService<IdentityRoleManager>(),
                    TenantsTestData.TenantARef, "Intake Staff", "copy-intake");
            }
        });
        return email;
    }

    public sealed record CopySent(
        string TemplateCode,
        IReadOnlyList<string> Recipients,
        IReadOnlyDictionary<string, object?> Variables,
        string ContextTag,
        IReadOnlyList<PacketAttachmentRef> Packets);

    public sealed class CopyRecorder : IPacketCopyDispatcher
    {
        private readonly List<CopySent> _sent = new();

        public IReadOnlyList<CopySent> Sent => _sent;

        public void Clear() => _sent.Clear();

        public Task DispatchWithPacketsAsync(
            NotificationCase notificationCase,
            string templateCode,
            IReadOnlyCollection<NotificationRecipient> recipients,
            IReadOnlyDictionary<string, object?> variables,
            string contextTag,
            IReadOnlyList<PacketAttachmentRef> packetRefs,
            CancellationToken cancellationToken = default)
        {
            _sent.Add(new CopySent(templateCode, recipients.Select(r => r.Email).ToList(), variables, contextTag, packetRefs.ToList()));
            return Task.CompletedTask;
        }
    }
}
