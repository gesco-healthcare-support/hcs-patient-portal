using System;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The removal shape in a document-update array (contract section G): the id the receiver already
/// holds, the tombstone flag, and the stamp its staleness guard compares against.
///
/// <para>Deliberately NOT an <see cref="IntakeDocumentEntry"/> with a flag. A deletion carries no
/// <c>objectKey</c>, no file name and no size, because the portal is repudiating the document --
/// sending a key alongside <c>deleted: true</c> would invite the receiver to fetch bytes it has
/// just been told to drop.</para>
/// </summary>
public class DocumentDeletionEntry
{
    /// <summary>The document or packet id previously published. Stable across re-uploads.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Always true. The type exists only to express a removal, so a <c>false</c> here would be
    /// meaningless -- an un-deletion is just a normal entry.
    /// </summary>
    public bool Deleted { get; set; } = true;

    /// <summary>ISO-8601 UTC. Lets the receiver ignore a tombstone older than what it holds.</summary>
    public string UpdatedAt { get; set; } = string.Empty;
}
