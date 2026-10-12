using System;

namespace HealthcareSupport.CaseEvaluation.Uploads;

/// <summary>
/// The scanner could not give a verdict: it was unreachable, timed out, closed the connection, or
/// answered something other than "clean" or "found". Callers refuse the upload (B11 fails closed).
/// </summary>
public class UploadScanUnavailableException : Exception
{
    public UploadScanUnavailableException(string message)
        : base(message)
    {
    }

    public UploadScanUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
