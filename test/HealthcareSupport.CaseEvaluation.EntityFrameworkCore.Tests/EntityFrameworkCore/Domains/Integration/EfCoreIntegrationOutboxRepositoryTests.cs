using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Exercises the atomic status-gated lease against a real (SQLite) EF context -- the DB-level
/// behaviour a List-backed mock cannot demonstrate. Proves the claim serializes: exactly one drain
/// wins a due row, a second is skipped WITHOUT an exception (the whole reason this repository exists
/// rather than an optimistic UpdateAsync), an expired lease is reclaimable, and terminal rows are
/// not leasable.
///
/// <para>Also pins the intake-exists query behind the #931 document gate. Its rules live in the
/// query, so this is the only layer where they can be proven: every status counts, only Intake rows
/// count, and the answer is per appointment.</para>
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreIntegrationOutboxRepositoryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private static readonly DateTime Now = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid AppointmentId = new("8f14e45f-ceea-467a-9f3a-1a2b3c4d5e6f");

    private readonly IIntegrationOutboxRepository _outboxRepository;

    public EfCoreIntegrationOutboxRepositoryTests()
    {
        _outboxRepository = GetRequiredService<IIntegrationOutboxRepository>();
    }

    private static IntegrationOutboxItem NewPending(Guid id) =>
        new(
            id,
            tenantId: null,
            IntegrationMessageType.Intake,
            targetPath: CaseTrackerEndpoints.Intake,
            appointmentId: AppointmentId,
            payload: "{\"data\":{}}",
            idempotencyKey: "key-" + id.ToString("N"));

    private Task<IntegrationOutboxItem> InsertAsync(IntegrationOutboxItem item) =>
        WithUnitOfWorkAsync(() => _outboxRepository.InsertAsync(item, autoSave: true));

    /// <summary>
    /// A row of the given type for an appointment of the test's own, so the intake-exists tests
    /// cannot see the Intake rows the lease tests insert for <see cref="AppointmentId"/>.
    /// </summary>
    private static IntegrationOutboxItem NewRow(Guid appointmentId, IntegrationMessageType messageType)
    {
        var id = Guid.NewGuid();
        return new(
            id,
            tenantId: null,
            messageType,
            targetPath: messageType == IntegrationMessageType.Intake
                ? CaseTrackerEndpoints.Intake
                : CaseTrackerEndpoints.DocumentUpdate(appointmentId),
            appointmentId: appointmentId,
            payload: "{\"data\":{}}",
            idempotencyKey: "key-" + id.ToString("N"));
    }

    private Task<bool> HasIntakeAsync(Guid appointmentId) =>
        WithUnitOfWorkAsync(() => _outboxRepository.HasIntakeAsync(appointmentId));

    [Theory]
    [InlineData(IntegrationOutboxStatus.Pending)]
    [InlineData(IntegrationOutboxStatus.Sent)]
    [InlineData(IntegrationOutboxStatus.Failed)]
    [InlineData(IntegrationOutboxStatus.Resolved)]
    public async Task HasIntakeAsync_WithAnIntakeRowInAnyStatus_ReturnsTrue(IntegrationOutboxStatus status)
    {
        // The rule a later reader is most likely to "tighten" to Sent. A Failed or still-Pending
        // intake is already in hand; holding back the document updates that follow it would strand
        // them behind a retry that is the outbox's job, not the gate's.
        var appointmentId = Guid.NewGuid();
        var row = NewRow(appointmentId, IntegrationMessageType.Intake);
        switch (status)
        {
            case IntegrationOutboxStatus.Sent:
                row.MarkSent(Now);
                break;
            case IntegrationOutboxStatus.Failed:
                row.MarkFatal(Now, "401 invalid token");
                break;
            case IntegrationOutboxStatus.Resolved:
                row.MarkFatal(Now, "401 invalid token");
                row.MarkResolved(Now);
                break;
        }

        // Guards the fixture: MarkResolved is a no-op on anything but a Failed row, so a wrong
        // sequence above would silently test a different status than the case claims.
        row.Status.ShouldBe(status);
        await InsertAsync(row);

        (await HasIntakeAsync(appointmentId)).ShouldBeTrue();
    }

    [Fact]
    public async Task HasIntakeAsync_WithOnlyADocumentUpdateRow_ReturnsFalse()
    {
        // Without the message-type filter the first document update would create the row that
        // authorises the second, and the gate would open itself.
        var appointmentId = Guid.NewGuid();
        await InsertAsync(NewRow(appointmentId, IntegrationMessageType.DocumentUpdate));

        (await HasIntakeAsync(appointmentId)).ShouldBeFalse();
    }

    [Fact]
    public async Task HasIntakeAsync_WithAnIntakeForAnotherAppointmentOnly_ReturnsFalse()
    {
        var appointmentId = Guid.NewGuid();
        await InsertAsync(NewRow(Guid.NewGuid(), IntegrationMessageType.Intake));

        (await HasIntakeAsync(appointmentId)).ShouldBeFalse();
    }

    [Fact]
    public async Task HasIntakeAsync_WithASoftDeletedIntakeRow_ReturnsFalse()
    {
        // Pins the hazard the interface documents rather than a behaviour anyone wants: nothing
        // deletes outbox rows today, and if a purge is ever added this is what it does to the #931
        // gate -- every later document update for the appointment is suppressed, silently. A change
        // that makes this test fail is the fix, not a regression.
        var appointmentId = Guid.NewGuid();
        var row = await InsertAsync(NewRow(appointmentId, IntegrationMessageType.Intake));
        await WithUnitOfWorkAsync(() => _outboxRepository.DeleteAsync(row.Id, autoSave: true));

        (await HasIntakeAsync(appointmentId)).ShouldBeFalse();
    }

    [Fact]
    public async Task TryLeaseAsync_FirstWins_SecondBlockedWithinLease()
    {
        var id = Guid.NewGuid();
        var leaseUntil = Now.AddSeconds(IntegrationOutboxConsts.LeaseDurationSeconds);
        await InsertAsync(NewPending(id));

        bool first = false, second = false;
        await WithUnitOfWorkAsync(async () =>
        {
            first = await _outboxRepository.TryLeaseAsync(id, Now, leaseUntil);
            second = await _outboxRepository.TryLeaseAsync(id, Now, leaseUntil);
        });

        first.ShouldBeTrue();   // won the row
        second.ShouldBeFalse(); // the unexpired lease no longer matches the claim gate

        await WithUnitOfWorkAsync(async () =>
        {
            var row = await _outboxRepository.GetAsync(id);
            row.LockedUntil.ShouldBe(leaseUntil); // the UPDATE persisted
        });
    }

    [Fact]
    public async Task TryLeaseAsync_ExpiredLease_IsReclaimable()
    {
        var id = Guid.NewGuid();
        await InsertAsync(NewPending(id));

        await WithUnitOfWorkAsync(async () =>
            (await _outboxRepository.TryLeaseAsync(id, Now, Now.AddSeconds(120))).ShouldBeTrue());

        // A drain running past the lease expiry reclaims the still-Pending row rather than leaving it
        // stranded by a worker that died mid-post.
        await WithUnitOfWorkAsync(async () =>
        {
            var later = Now.AddSeconds(200);
            (await _outboxRepository.TryLeaseAsync(id, later, later.AddSeconds(120))).ShouldBeTrue();
        });
    }

    [Fact]
    public async Task TryLeaseAsync_SentRow_IsNotLeasable()
    {
        var id = Guid.NewGuid();
        var sent = NewPending(id);
        sent.MarkSent(Now);
        await InsertAsync(sent);

        await WithUnitOfWorkAsync(async () =>
            (await _outboxRepository.TryLeaseAsync(id, Now, Now.AddSeconds(120))).ShouldBeFalse());
    }

    [Fact]
    public async Task TryLeaseAsync_DeadLetteredRow_IsNotLeasable()
    {
        // A fatally-failed row must never be picked up again by a drain -- only a human re-push.
        var id = Guid.NewGuid();
        var fatal = NewPending(id);
        fatal.MarkFatal(Now, "401 invalid token");
        await InsertAsync(fatal);

        await WithUnitOfWorkAsync(async () =>
            (await _outboxRepository.TryLeaseAsync(id, Now, Now.AddSeconds(120))).ShouldBeFalse());
    }

    [Fact]
    public async Task TryLeaseAsync_RowInBackoff_IsNotLeasableUntilDue()
    {
        var id = Guid.NewGuid();
        var backedOff = NewPending(id);
        backedOff.MarkFailed(Now, "503", TimeSpan.FromSeconds(IntegrationOutboxConsts.RetryBackoffSeconds));
        await InsertAsync(backedOff);

        await WithUnitOfWorkAsync(async () =>
            (await _outboxRepository.TryLeaseAsync(id, Now.AddSeconds(60), Now.AddSeconds(180))).ShouldBeFalse());

        await WithUnitOfWorkAsync(async () =>
        {
            var due = Now.AddSeconds(IntegrationOutboxConsts.RetryBackoffSeconds + 1);
            (await _outboxRepository.TryLeaseAsync(id, due, due.AddSeconds(120))).ShouldBeTrue();
        });
    }

    [Fact]
    public async Task GetForAppointmentAsync_ReturnsThatAppointmentsRowsOfThatType_NewestFirst_WithoutDeletedOnes()
    {
        // The #915 collapse rule compares an intake with the NEWEST intake row, so the order is
        // load-bearing, and it must not see another appointment's rows, the other message type, or a
        // soft-deleted row. Each insert is its own unit of work, so creation times strictly increase.
        var appointmentId = Guid.NewGuid();
        var oldest = await InsertAsync(NewRow(appointmentId, IntegrationMessageType.Intake));
        await InsertAsync(NewRow(appointmentId, IntegrationMessageType.DocumentUpdate));
        await InsertAsync(NewRow(Guid.NewGuid(), IntegrationMessageType.Intake));
        var deleted = await InsertAsync(NewRow(appointmentId, IntegrationMessageType.Intake));
        await WithUnitOfWorkAsync(() => _outboxRepository.DeleteAsync(deleted.Id, autoSave: true));
        var newest = await InsertAsync(NewRow(appointmentId, IntegrationMessageType.Intake));

        var rows = await WithUnitOfWorkAsync(() =>
            _outboxRepository.GetForAppointmentAsync(appointmentId, IntegrationMessageType.Intake));

        rows.Select(r => r.Id).ShouldBe(new[] { newest.Id, oldest.Id });
    }

    [Fact]
    public async Task AcquireAppointmentLockAsync_OnTheSqliteTestProvider_IsANoOp()
    {
        // The lock is a SQL Server application lock. On SQLite it must return quietly: sp_getapplock
        // is not SQLite syntax, so a missing provider check would fail every provider-backed test that
        // enqueues anything. Blocking is proven against SQL Server itself (see PR #1020), not here.
        await WithUnitOfWorkAsync(() => _outboxRepository.AcquireAppointmentLockAsync(Guid.NewGuid()));
    }

    [Fact]
    public void AppointmentLockResource_IsPerAppointmentAndWithinTheSqlServerNameLimit()
    {
        var a = EfCoreIntegrationOutboxRepository.AppointmentLockResource(AppointmentId);
        var b = EfCoreIntegrationOutboxRepository.AppointmentLockResource(Guid.NewGuid());

        a.ShouldNotBe(b);
        a.ShouldContain(AppointmentId.ToString("D"));
        a.Length.ShouldBeLessThanOrEqualTo(255); // sp_getapplock @Resource is nvarchar(255)
    }
}
