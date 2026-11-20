using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Stands in for <see cref="LookupResponseFloor"/> in the multi-office harness: counts each wait instead of
/// sleeping, so the lookup tests stay fast and can assert which answers were padded.
/// </summary>
public sealed class RecordingLookupResponseFloor : LookupResponseFloor
{
    private int _pads;

    public int Pads => Volatile.Read(ref _pads);

    public void Reset() => Interlocked.Exchange(ref _pads, 0);

    public override Task PadAsync(long startTimestamp)
    {
        Interlocked.Increment(ref _pads);
        return Task.CompletedTask;
    }
}
