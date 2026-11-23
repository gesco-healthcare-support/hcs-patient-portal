using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Stands in for <see cref="LookupResponseFloor"/> in the multi-office harness: counts each wait instead of
/// sleeping, so the lookup tests stay fast and can assert which answers were padded. <see cref="OnPad"/> runs at
/// the moment of the wait, so a test can check what has already happened by then.
/// </summary>
public sealed class RecordingLookupResponseFloor : LookupResponseFloor
{
    private int _pads;

    public int Pads => Volatile.Read(ref _pads);

    /// <summary>Optional: runs inside each wait, before it is counted.</summary>
    public Func<Task>? OnPad { get; set; }

    public void Reset()
    {
        Interlocked.Exchange(ref _pads, 0);
        OnPad = null;
    }

    public override async Task PadAsync(long startTimestamp)
    {
        if (OnPad != null)
        {
            await OnPad();
        }

        Interlocked.Increment(ref _pads);
    }
}
