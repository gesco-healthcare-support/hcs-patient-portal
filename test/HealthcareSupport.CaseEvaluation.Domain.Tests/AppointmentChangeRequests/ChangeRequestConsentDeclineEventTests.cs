using HealthcareSupport.CaseEvaluation.Notifications.Events;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Row 9d (2026-11-03): a side that DECLINES consent raises <see cref="ChangeRequestConsentDeclinedEto"/> so
/// the office inbox and the Intake Staff hear at once. Both ways a side answers -- the public consent link
/// and the in-portal Agree/Decline -- end in <see cref="ChangeRequestConsentManager.RecordDecisionAsync(ChangeRequestConsentMatch, bool, string?)"/>,
/// so that is where the event is pinned. An approval, an expired answer and a refused second answer raise
/// nothing. The result asserted is the event published on a substituted local bus. Synthetic data only.
/// </summary>
public class ChangeRequestConsentDeclineEventTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 18, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid AppointmentId = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private sealed class Rig
    {
        public ILocalEventBus Bus { get; } = Substitute.For<ILocalEventBus>();

        public ChangeRequestConsentManager Manager { get; }

        public Rig()
        {
            var clock = Substitute.For<IClock>();
            clock.Now.Returns(Now);
            Manager = new ChangeRequestConsentManager(
                Substitute.For<IAppointmentChangeRequestRepository>(),
                Substitute.For<IChangeRequestConsentRoundRepository>(),
                clock,
                Bus);
        }

        public List<ChangeRequestConsentDeclinedEto> Declines() =>
            Bus.ReceivedCalls().SelectMany(c => c.GetArguments()).OfType<ChangeRequestConsentDeclinedEto>().ToList();
    }

    private static AppointmentChangeRequest PendingCancellation(DateTime expiresAt)
    {
        var request = new AppointmentChangeRequest(
            Guid.NewGuid(), TenantId, AppointmentId, ChangeRequestType.Cancel,
            cancellationReason: "TEST-reason", reScheduleReason: null, newDoctorAvailabilityId: null);
        request.IssueSideConsent(ChangeRequestSide.SideB, "TEST-hash-b", expiresAt);
        return request;
    }

    private static ChangeRequestConsentRound PendingRound(Guid changeRequestId, int number)
    {
        var round = new ChangeRequestConsentRound(
            Guid.NewGuid(), TenantId, changeRequestId, number, Guid.NewGuid(), proposedByUserId: null);
        round.IssueSideConsent(ChangeRequestSide.SideA, "TEST-hash-a", Now.AddDays(7));
        return round;
    }

    [Fact]
    public async Task A_declined_cancellation_raises_one_declined_event_with_round_zero_and_the_side()
    {
        var rig = new Rig();
        var request = PendingCancellation(Now.AddDays(7));

        await rig.Manager.RecordDecisionAsync(
            new ChangeRequestConsentMatch(request, Round: null, ChangeRequestSide.SideB), approved: false, "TEST-da@test.local");

        var declined = rig.Declines().ShouldHaveSingleItem();
        declined.AppointmentId.ShouldBe(AppointmentId);
        declined.ChangeRequestId.ShouldBe(request.Id);
        declined.TenantId.ShouldBe(TenantId);
        declined.ChangeRequestType.ShouldBe(ChangeRequestType.Cancel);
        declined.RoundNumber.ShouldBe(0);
        declined.Side.ShouldBe(ChangeRequestSide.SideB);
    }

    [Fact]
    public async Task A_declined_reschedule_round_raises_the_event_with_its_round_number()
    {
        var rig = new Rig();
        var request = new AppointmentChangeRequest(
            Guid.NewGuid(), TenantId, AppointmentId, ChangeRequestType.Reschedule,
            cancellationReason: null, reScheduleReason: "TEST-reason", newDoctorAvailabilityId: null);
        var round = PendingRound(request.Id, number: 2);

        await rig.Manager.RecordDecisionAsync(
            new ChangeRequestConsentMatch(request, round, ChangeRequestSide.SideA), approved: false, respondedByEmail: null);

        var declined = rig.Declines().ShouldHaveSingleItem();
        declined.RoundNumber.ShouldBe(2);
        declined.Side.ShouldBe(ChangeRequestSide.SideA);
        declined.ChangeRequestType.ShouldBe(ChangeRequestType.Reschedule);
    }

    [Fact]
    public async Task An_approval_raises_no_declined_event()
    {
        var rig = new Rig();

        await rig.Manager.RecordDecisionAsync(
            new ChangeRequestConsentMatch(PendingCancellation(Now.AddDays(7)), Round: null, ChangeRequestSide.SideB),
            approved: true, "TEST-da@test.local");

        rig.Declines().ShouldBeEmpty();
    }

    [Fact]
    public async Task An_expired_answer_raises_no_declined_event()
    {
        var rig = new Rig();

        await Should.ThrowAsync<BusinessException>(() => rig.Manager.RecordDecisionAsync(
            new ChangeRequestConsentMatch(PendingCancellation(Now.AddMinutes(-1)), Round: null, ChangeRequestSide.SideB),
            approved: false, respondedByEmail: null));

        rig.Declines().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_second_answer_is_refused_and_raises_no_second_event()
    {
        var rig = new Rig();
        var request = PendingCancellation(Now.AddDays(7));
        var match = new ChangeRequestConsentMatch(request, Round: null, ChangeRequestSide.SideB);
        await rig.Manager.RecordDecisionAsync(match, approved: false, respondedByEmail: null);

        await Should.ThrowAsync<BusinessException>(() => rig.Manager.RecordDecisionAsync(
            match, approved: false, respondedByEmail: null));

        rig.Declines().Count.ShouldBe(1);
    }
}
