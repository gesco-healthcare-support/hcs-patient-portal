using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// Pure unit tests for <see cref="RequiredDocumentEvaluator"/> (no DB / DI).
/// Synthetic GUIDs only.
/// </summary>
public class RequiredDocumentEvaluatorTests
{
    private static readonly Guid DocA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid DocB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid DocC = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    [Fact]
    public void Empty_required_yields_empty_result()
    {
        var result = RequiredDocumentEvaluator.Evaluate(
            Array.Empty<(Guid, string)>(),
            new (Guid?, DocumentStatus)[] { (DocA, DocumentStatus.Accepted) });

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Accepted_row_satisfies_the_requirement()
    {
        var result = RequiredDocumentEvaluator.Evaluate(
            new[] { (DocA, "Medical Records") },
            new (Guid?, DocumentStatus)[] { (DocA, DocumentStatus.Accepted) });

        result.ShouldBeEmpty();
    }

    [Fact]
    public void No_row_means_NotUploaded()
    {
        var result = RequiredDocumentEvaluator.Evaluate(
            new[] { (DocA, "Medical Records") },
            Array.Empty<(Guid?, DocumentStatus)>());

        var only = result.ShouldHaveSingleItem();
        only.DocumentId.ShouldBe(DocA);
        only.Name.ShouldBe("Medical Records");
        only.State.ShouldBe(RequiredDocumentState.NotUploaded);
    }

    [Fact]
    public void Only_pending_placeholder_means_NotUploaded()
    {
        var result = RequiredDocumentEvaluator.Evaluate(
            new[] { (DocA, "Medical Records") },
            new (Guid?, DocumentStatus)[] { (DocA, DocumentStatus.Pending) });

        result.ShouldHaveSingleItem().State.ShouldBe(RequiredDocumentState.NotUploaded);
    }

    [Fact]
    public void Uploaded_row_means_AwaitingReview()
    {
        var result = RequiredDocumentEvaluator.Evaluate(
            new[] { (DocA, "Medical Records") },
            new (Guid?, DocumentStatus)[] { (DocA, DocumentStatus.Uploaded) });

        result.ShouldHaveSingleItem().State.ShouldBe(RequiredDocumentState.AwaitingReview);
    }

    [Fact]
    public void Rejected_row_means_Rejected()
    {
        var result = RequiredDocumentEvaluator.Evaluate(
            new[] { (DocA, "Medical Records") },
            new (Guid?, DocumentStatus)[] { (DocA, DocumentStatus.Rejected) });

        result.ShouldHaveSingleItem().State.ShouldBe(RequiredDocumentState.Rejected);
    }

    [Fact]
    public void Uploaded_outranks_Rejected_when_both_present()
    {
        // A doc rejected once then re-uploaded: the newer Uploaded row means it
        // is awaiting review, the more accurate (less alarming) state.
        var result = RequiredDocumentEvaluator.Evaluate(
            new[] { (DocA, "Medical Records") },
            new (Guid?, DocumentStatus)[]
            {
                (DocA, DocumentStatus.Rejected),
                (DocA, DocumentStatus.Uploaded),
            });

        result.ShouldHaveSingleItem().State.ShouldBe(RequiredDocumentState.AwaitingReview);
    }

    [Fact]
    public void Accepted_wins_over_other_rows_for_the_same_requirement()
    {
        var result = RequiredDocumentEvaluator.Evaluate(
            new[] { (DocA, "Medical Records") },
            new (Guid?, DocumentStatus)[]
            {
                (DocA, DocumentStatus.Rejected),
                (DocA, DocumentStatus.Accepted),
            });

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Rows_with_null_source_do_not_satisfy_anything()
    {
        // Ad-hoc uploads (no SourceDocumentId) never count toward a requirement.
        var result = RequiredDocumentEvaluator.Evaluate(
            new[] { (DocA, "Medical Records") },
            new (Guid?, DocumentStatus)[] { (null, DocumentStatus.Accepted) });

        result.ShouldHaveSingleItem().State.ShouldBe(RequiredDocumentState.NotUploaded);
    }

    [Fact]
    public void Duplicate_required_id_is_collapsed_for_union_of_packages()
    {
        var result = RequiredDocumentEvaluator.Evaluate(
            new[] { (DocA, "Medical Records"), (DocA, "Medical Records (dup)") },
            Array.Empty<(Guid?, DocumentStatus)>());

        var only = result.ShouldHaveSingleItem();
        only.Name.ShouldBe("Medical Records"); // first name wins
    }

    [Fact]
    public void Multiple_requirements_report_only_the_unsatisfied_in_input_order()
    {
        var result = RequiredDocumentEvaluator.Evaluate(
            new[] { (DocA, "A"), (DocB, "B"), (DocC, "C") },
            new (Guid?, DocumentStatus)[]
            {
                (DocB, DocumentStatus.Accepted), // satisfied -> dropped
                (DocC, DocumentStatus.Uploaded), // awaiting review
                // DocA has no row -> not uploaded
            });

        result.Select(r => r.DocumentId).ShouldBe(new[] { DocA, DocC });
        result.Single(r => r.DocumentId == DocA).State.ShouldBe(RequiredDocumentState.NotUploaded);
        result.Single(r => r.DocumentId == DocC).State.ShouldBe(RequiredDocumentState.AwaitingReview);
    }
}
