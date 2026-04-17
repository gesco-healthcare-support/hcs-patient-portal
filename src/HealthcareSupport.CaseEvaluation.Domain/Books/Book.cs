using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace HealthcareSupport.CaseEvaluation.Books;

public class Book : AuditedAggregateRoot<Guid>
{
    public string Name { get; set; } = null!;

    public BookType Type { get; set; }

    public DateTime PublishDate { get; set; }

    public float Price { get; set; }
}