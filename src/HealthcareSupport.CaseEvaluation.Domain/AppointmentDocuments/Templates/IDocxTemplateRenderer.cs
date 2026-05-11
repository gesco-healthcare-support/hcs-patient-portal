namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;

/// <summary>
/// Renders a packet DOCX by performing run-aware token replacement plus
/// optional signature image stamping at <c>##Appointments.Signature##</c>.
///
/// <para>OLD parity: mirrors <c>ApplicationUtility.ReplaceTextOfWordDocument</c>
/// + <c>AppointmentDocumentDomain.InsertAPicture</c>, with two OLD bugs
/// silently fixed:</para>
/// <list type="number">
///   <item>OLD called <c>string.Replace</c> on the raw XML stream without
///   XML-escaping the replacement value -- token values containing
///   <c>&amp;</c>, <c>&lt;</c>, <c>&gt;</c>, <c>"</c>, <c>'</c> would
///   produce invalid DOCX. NEW writes through the OpenXml <c>Text</c>
///   element which escapes automatically.</item>
///   <item>OLD's signature placeholder match used
///   <c>x.Text.Equals("##Appointments.Signature##")</c> -- if Word split
///   the placeholder across multiple <c>&lt;w:t&gt;</c> runs (proof
///   error, smart tag), the match failed silently and no signature
///   was inserted. NEW reconstructs paragraph-level text and locates the
///   placeholder regardless of run splitting.</item>
/// </list>
/// </summary>
public interface IDocxTemplateRenderer
{
    /// <summary>
    /// Returns a fresh DOCX byte[] with all <c>##Group.Field##</c> tokens
    /// replaced from <paramref name="context"/>. The signature image is
    /// stamped at <c>##Appointments.Signature##</c> when
    /// <see cref="PacketTokenContext.ResponsibleUserSignature"/> is non-null;
    /// otherwise the placeholder text is removed (matches OLD silent-skip
    /// at <c>AppointmentDocumentDomain.cs:657</c>).
    /// </summary>
    byte[] Render(byte[] templateBytes, PacketTokenContext context);
}
