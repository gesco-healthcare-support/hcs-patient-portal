using System;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

public class CaseEvaluationEntityFrameworkCoreFixture : IDisposable
{
    public void Dispose()
    {
        // Nothing to release: the fixture holds no unmanaged handle and no
        // IDisposable of its own. SuppressFinalize is still correct (CA1816) --
        // it costs nothing here and keeps the contract right for any derived
        // type that later introduces a finalizer.
        GC.SuppressFinalize(this);
    }
}
