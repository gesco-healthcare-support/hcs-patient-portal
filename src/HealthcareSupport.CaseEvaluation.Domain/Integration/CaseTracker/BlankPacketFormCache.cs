using System.Collections.Concurrent;
using System.Linq;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Rendered blank forms, keyed by their ETag. The ETag is a hash of everything that decides the bytes, so an entry
/// never goes stale: a changed template or letterhead is a different key. In memory and per API instance, so a
/// restart only costs one re-render; no table, no migration.
/// </summary>
public sealed class BlankPacketFormCache : ISingletonDependency
{
    private readonly ConcurrentDictionary<string, byte[]> _forms = new();

    /// <summary>The cached PDF for this ETag, or null.</summary>
    public byte[]? Find(string eTag) => _forms.TryGetValue(eTag, out var pdf) ? pdf : null;

    /// <summary>Keeps the PDF under this ETag. When the cache is full it drops everything: the next request re-renders.</summary>
    public void Store(string eTag, byte[] pdf)
    {
        if (_forms.Count >= CaseTrackerFormsConsts.MaxCachedForms && !_forms.ContainsKey(eTag))
        {
            foreach (var key in _forms.Keys.ToList())
            {
                _forms.TryRemove(key, out _);
            }
        }

        _forms[eTag] = pdf;
    }
}
