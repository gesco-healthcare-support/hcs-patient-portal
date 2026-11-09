using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Services;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-11-08 (W2) -- the case a re-evaluation should follow on from: the NEWEST one in the chain the
/// booker pointed at, not necessarily the one whose number they typed. A booker who types the first
/// exam's number while a re-evaluation of it has since been approved should get the form filled from
/// that re-evaluation, because it holds the current parties, claims and details.
///
/// <para><b>The chain has two forward links</b>, and the walk follows both:</para>
/// <list type="bullet">
///   <item><see cref="Appointment.OriginalAppointmentId"/>: a re-evaluation of the case. A reschedule copies
///   it onto the replacement (<c>RescheduleFinalizer</c>), so a rescheduled re-evaluation still points back.</item>
///   <item><see cref="Appointment.RescheduledFromAppointmentId"/>: the case was replaced (rescheduled, re-booked
///   after a no-show, or re-requested after a rejection).</item>
/// </list>
/// <para>Following only the first would skip a re-evaluation's live replacement after a reschedule.</para>
///
/// <para><b>Same patient only.</b> A successor booked for a different patient is never followed, so
/// anything proven about the typed case's patient (the date of birth, in particular) holds for every
/// case the walk can reach.</para>
///
/// <para><b>Newest eligible wins.</b> Among the typed case and everything reachable from it, the latest
/// exam date (then the latest booking) that <see cref="AppointmentLifecycleValidators.CanCreateReval"/>
/// accepts. A newer re-evaluation that is still pending is not eligible, so the approved one before it
/// is used. Branches (two re-evaluations of one case) need no special handling: every branch is
/// collected and the newest eligible case among all of them wins.</para>
///
/// <para>The patient's appointments are loaded once and walked in memory: no query per level, and no
/// index needed on <see cref="Appointment.OriginalAppointmentId"/>.</para>
/// </summary>
public class ReEvaluationChain : DomainService
{
    /// <summary>
    /// The most cases one walk visits. Real chains are a handful long; the cap only bounds a
    /// pathological patient record. A cycle cannot be built (a successor is always created after its
    /// source), and the visited set guards against one anyway.
    /// </summary>
    public const int MaxCasesWalked = 100;

    private readonly IAppointmentRepository _appointmentRepository;

    public ReEvaluationChain(IAppointmentRepository appointmentRepository)
    {
        _appointmentRepository = appointmentRepository;
    }

    /// <summary>
    /// The newest re-evaluable case in <paramref name="start"/>'s chain, or <paramref name="start"/>
    /// itself when nothing in the chain can be re-evaluated (so the caller's own status refusal applies
    /// to the case the booker typed).
    /// </summary>
    public virtual async Task<Appointment> FindNewestEligibleAsync(Appointment start)
    {
        if (start.PatientId == Guid.Empty)
        {
            return start;
        }

        var patientCases = await _appointmentRepository.GetListAsync(a => a.PatientId == start.PatientId);
        return NewestEligible(patientCases, new[] { start }) ?? start;
    }

    /// <summary>
    /// The newest re-evaluable case among <paramref name="starts"/> and every case reachable from them
    /// through either forward link, keeping to each start's patient; null when none can be re-evaluated.
    /// <paramref name="patientCases"/> must hold the patient's appointments; a start missing from it is
    /// still considered.
    /// </summary>
    public static Appointment? NewestEligible(IReadOnlyCollection<Appointment> patientCases, IEnumerable<Appointment> starts)
    {
        var successors = patientCases
            .SelectMany(a => SourcesOf(a).Select(sourceId => (SourceId: sourceId, Successor: a)))
            .ToLookup(x => x.SourceId, x => x.Successor);

        var visited = new HashSet<Guid>();
        var reached = new List<Appointment>();
        var queue = new Queue<Appointment>();
        foreach (var start in starts)
        {
            if (visited.Add(start.Id))
            {
                queue.Enqueue(start);
            }
        }

        while (queue.Count > 0 && reached.Count < MaxCasesWalked)
        {
            var current = queue.Dequeue();
            reached.Add(current);
            foreach (var next in successors[current.Id])
            {
                if (next.PatientId == current.PatientId && visited.Add(next.Id))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return reached
            .Where(a => AppointmentLifecycleValidators.CanCreateReval(
                a.AppointmentStatus, a.EvaluationKind, callerIsItAdmin: false))
            .OrderByDescending(a => a.AppointmentDate)
            .ThenByDescending(a => a.CreationTime)
            .ThenBy(a => a.Id)
            .FirstOrDefault();
    }

    /// <summary>The cases <paramref name="appointment"/> follows on from: its re-evaluation source and the case it replaced.</summary>
    private static IEnumerable<Guid> SourcesOf(Appointment appointment)
    {
        if (appointment.OriginalAppointmentId is { } original)
        {
            yield return original;
        }

        if (appointment.RescheduledFromAppointmentId is { } replaced && replaced != appointment.OriginalAppointmentId)
        {
            yield return replaced;
        }
    }
}
