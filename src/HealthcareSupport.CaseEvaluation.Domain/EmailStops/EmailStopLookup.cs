using System;
using System.Collections.Generic;
using System.Linq;

namespace HealthcareSupport.CaseEvaluation.EmailStops;

/// <summary>
/// 2026-11-05 -- the answer to "which of these addresses are stopped", loaded once by
/// <see cref="EmailStopPolicy"/> for a whole send (or a whole drain batch) and then asked per address. An
/// all-cases stop wins over a case stop when both exist.
/// </summary>
public sealed class EmailStopLookup
{
    private readonly HashSet<string> _allCases;
    private readonly HashSet<(Guid AppointmentId, string Address)> _cases;

    public static readonly EmailStopLookup Empty = new(Array.Empty<string>(), Array.Empty<(Guid, string)>());

    public EmailStopLookup(
        IEnumerable<string> allCasesNormalized,
        IEnumerable<(Guid AppointmentId, string NormalizedAddress)> caseStops)
    {
        _allCases = new HashSet<string>(allCasesNormalized, StringComparer.Ordinal);
        _cases = new HashSet<(Guid, string)>(caseStops);
    }

    /// <summary>True when nothing at all is stopped (the common case: no filtering work).</summary>
    public bool IsEmpty => _allCases.Count == 0 && _cases.Count == 0;

    /// <summary>The scope that stops this address on this case, or null when it gets the email.</summary>
    public EmailStopScope? ScopeOf(string? email, Guid? appointmentId)
    {
        var normalized = EmailStopAddress.Normalize(email);
        if (normalized == null)
        {
            return null;
        }
        if (_allCases.Contains(normalized))
        {
            return EmailStopScope.AllCases;
        }
        if (appointmentId.HasValue && _cases.Contains((appointmentId.Value, normalized)))
        {
            return EmailStopScope.Case;
        }
        return null;
    }

    public bool IsStopped(string? email, Guid? appointmentId) => ScopeOf(email, appointmentId).HasValue;
}

/// <summary>
/// 2026-11-05 -- one email's addressing after the stop filter. A stopped To is replaced by the FIRST
/// remaining CC (agreed 2026-11-05: the email still reaches the people who were not stopped); when nobody
/// is left, <see cref="To"/> is null and the email is not sent at all.
/// </summary>
public sealed record EmailStopAddressing(string? To, IReadOnlyList<string> Cc, IReadOnlyList<string> Dropped)
{
    public bool IsDropped => To == null;

    public bool Changed => Dropped.Count > 0;

    /// <summary>Applies the stop filter to one email's To + CC list.</summary>
    public static EmailStopAddressing Apply(
        string to,
        IEnumerable<string>? cc,
        EmailStopLookup lookup,
        Guid? appointmentId)
    {
        var dropped = new List<string>();
        var keptCc = new List<string>();
        foreach (var address in cc ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                continue;
            }
            if (lookup.IsStopped(address, appointmentId))
            {
                dropped.Add(address);
            }
            else
            {
                keptCc.Add(address);
            }
        }

        if (!lookup.IsStopped(to, appointmentId))
        {
            return new EmailStopAddressing(to, keptCc, dropped);
        }

        dropped.Insert(0, to);
        if (keptCc.Count == 0)
        {
            return new EmailStopAddressing(null, keptCc, dropped);
        }
        return new EmailStopAddressing(keptCc[0], keptCc.Skip(1).ToList(), dropped);
    }
}
