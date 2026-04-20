namespace HealthcareSupport.CaseEvaluation.Shared;

public class LookupDto<TKey>
{
    public TKey Id { get; set; } = default!;

    public string DisplayName { get; set; } = null!;
}