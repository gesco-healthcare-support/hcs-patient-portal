using System;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The Gesco cross-project response envelope, used here as the intake REQUEST body because the
/// Case Tracker's intake endpoint reads <c>data</c> out of it (contract section A).
/// </summary>
public class IntakeEnvelope
{
    public IntakePayload Data { get; set; } = new();

    public IntakeMeta Meta { get; set; } = new();

    /// <summary>Always empty on an outbound push; present because the envelope requires it.</summary>
    public List<IntakeError> Errors { get; set; } = new();
}

/// <summary>Correlation metadata. <see cref="RequestId"/> is what to quote when tracing a push.</summary>
public class IntakeMeta
{
    public Guid RequestId { get; set; }

    /// <summary>ISO-8601 UTC with <c>Z</c>.</summary>
    public string Timestamp { get; set; } = string.Empty;
}

/// <summary>Envelope error shape. Never populated outbound; defined so the contract is complete.</summary>
public class IntakeError
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Field { get; set; }
}
