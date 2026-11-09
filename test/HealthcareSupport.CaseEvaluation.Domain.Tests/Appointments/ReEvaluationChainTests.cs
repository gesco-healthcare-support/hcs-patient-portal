using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// W2 (2026-11-08) -- <see cref="ReEvaluationChain.NewestEligible"/>: which case a re-evaluation follows on
/// from when the booker typed an older number. Pure, so every shape of chain is checked here; the
/// database-backed tests cover the lookup, the submit and the forgotten-number email on top of it.
/// </summary>
public class ReEvaluationChainTests
{
    private static readonly Guid Patient = Guid.NewGuid();
    private static readonly DateTime Day0 = new(2027, 1, 4, 9, 0, 0, DateTimeKind.Unspecified);

    private static Appointment Case(
        int day,
        AppointmentStatusType status = AppointmentStatusType.Approved,
        Guid? patientId = null,
        Appointment? reEvaluationOf = null,
        Appointment? replaces = null,
        EvaluationKind? kind = null)
    {
        var appointment = new Appointment(
            id: Guid.NewGuid(),
            patientId: patientId ?? Patient,
            identityUserId: null,
            appointmentTypeId: Guid.NewGuid(),
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: Day0.AddDays(day),
            requestConfirmationNumber: "A" + day.ToString("D5"),
            appointmentStatus: status);
        appointment.OriginalAppointmentId = reEvaluationOf?.Id;
        appointment.RescheduledFromAppointmentId = replaces?.Id;
        appointment.EvaluationKind = kind ?? (reEvaluationOf != null ? EvaluationKind.ReEvaluation : EvaluationKind.Evaluation);
        return appointment;
    }

    private static Appointment? Newest(Appointment start, params Appointment[] others) =>
        ReEvaluationChain.NewestEligible(others.Append(start).ToList(), new[] { start });

    [Fact]
    public void A_case_with_no_successors_is_its_own_answer()
    {
        var first = Case(0);

        Newest(first).ShouldBe(first);
    }

    [Fact]
    public void An_approved_re_evaluation_of_the_typed_case_wins()
    {
        var first = Case(0);
        var reEval = Case(100, reEvaluationOf: first);

        Newest(first, reEval).ShouldBe(reEval);
    }

    [Fact]
    public void A_re_evaluation_of_a_re_evaluation_is_followed_to_the_end()
    {
        var first = Case(0);
        var second = Case(100, reEvaluationOf: first);
        var third = Case(200, reEvaluationOf: second);

        Newest(first, second, third).ShouldBe(third);
    }

    [Fact]
    public void A_rescheduled_re_evaluation_is_followed_to_its_live_replacement()
    {
        // RescheduleFinalizer copies OriginalAppointmentId onto the replacement and points
        // RescheduledFromAppointmentId at the old row. Following only the first link would still reach
        // the replacement here; the next test is the one only the second link reaches.
        var first = Case(0);
        var reEval = Case(100, AppointmentStatusType.RescheduledNoBill, reEvaluationOf: first);
        var replacement = Case(120, reEvaluationOf: first, replaces: reEval);

        Newest(first, reEval, replacement).ShouldBe(replacement);
    }

    [Fact]
    public void A_rescheduled_first_exam_is_followed_through_the_replacement_link_alone()
    {
        // The first exam's replacement has no OriginalAppointmentId: only RescheduledFromAppointmentId reaches it.
        var first = Case(0, AppointmentStatusType.RescheduledLate);
        var replacement = Case(30, replaces: first);

        Newest(first, replacement).ShouldBe(replacement);
    }

    [Fact]
    public void A_pending_re_evaluation_is_skipped_for_the_approved_case_before_it()
    {
        var first = Case(0);
        var approved = Case(100, reEvaluationOf: first);
        var pending = Case(200, AppointmentStatusType.Pending, reEvaluationOf: approved);

        Newest(first, approved, pending).ShouldBe(approved);
    }

    [Fact]
    public void A_re_evaluation_that_was_a_no_show_counts_but_a_first_exam_no_show_does_not()
    {
        var first = Case(0);
        var reEvalNoShow = Case(100, AppointmentStatusType.NoShow, reEvaluationOf: first);
        Newest(first, reEvalNoShow).ShouldBe(reEvalNoShow, "a no-show re-evaluation may be re-evaluated again");

        var firstNoShow = Case(0, AppointmentStatusType.NoShow);
        Newest(firstNoShow).ShouldBeNull("nothing in this chain can be re-evaluated");
    }

    [Fact]
    public void Two_re_evaluations_of_one_case_resolve_to_the_newest_eligible_of_either_branch()
    {
        var first = Case(0);
        var branchA = Case(100, reEvaluationOf: first);
        var branchB = Case(150, reEvaluationOf: first);
        var branchAChild = Case(300, AppointmentStatusType.CancelledNoBill, reEvaluationOf: branchA);

        Newest(first, branchA, branchB, branchAChild).ShouldBe(branchB);
    }

    [Fact]
    public void A_successor_booked_for_another_patient_is_never_followed()
    {
        // The submit does not force a re-evaluation onto its source's patient, so a chain can, in
        // principle, change patient. The date of birth proven for the typed case would not hold there.
        var first = Case(0);
        var otherPatientsReEval = Case(100, patientId: Guid.NewGuid(), reEvaluationOf: first);
        var beyondIt = Case(200, patientId: Guid.NewGuid(), reEvaluationOf: otherPatientsReEval);

        Newest(first, otherPatientsReEval, beyondIt).ShouldBe(first);
    }

    [Fact]
    public void Older_cases_of_the_same_patient_are_not_reached_backwards()
    {
        // The walk goes forward only: typing a later number never falls back to an earlier case.
        var first = Case(0);
        var reEval = Case(100, AppointmentStatusType.Pending, reEvaluationOf: first);

        Newest(reEval, first).ShouldBeNull("the typed case cannot be re-evaluated and nothing follows it");
    }

    [Fact]
    public void A_separate_chain_of_the_same_patient_is_not_reached()
    {
        var first = Case(0);
        var unrelated = Case(400);

        Newest(first, unrelated).ShouldBe(first);
    }

    [Fact]
    public void Several_starts_resolve_to_the_newest_eligible_reachable_from_any_of_them()
    {
        // The forgotten-number email starts from every case carrying the claim.
        var first = Case(0);
        var reEval = Case(100, reEvaluationOf: first);
        var laterUnderTheSameClaim = Case(50);
        var all = new List<Appointment> { first, reEval, laterUnderTheSameClaim };

        ReEvaluationChain.NewestEligible(all, new[] { first, laterUnderTheSameClaim }).ShouldBe(reEval);
    }

    [Fact]
    public void A_cycle_in_bad_data_still_ends()
    {
        var a = Case(0);
        var b = Case(10, reEvaluationOf: a);
        a.RescheduledFromAppointmentId = b.Id;

        Newest(a, b).ShouldBe(b);
    }

    [Fact]
    public void The_walk_stops_at_the_cap()
    {
        var chain = new List<Appointment> { Case(0) };
        for (var i = 1; i < ReEvaluationChain.MaxCasesWalked + 20; i++)
        {
            chain.Add(Case(i, reEvaluationOf: chain[i - 1]));
        }

        var newest = ReEvaluationChain.NewestEligible(chain, new[] { chain[0] });

        newest.ShouldBe(chain[ReEvaluationChain.MaxCasesWalked - 1], "the walk visits at most the cap");
    }
}
