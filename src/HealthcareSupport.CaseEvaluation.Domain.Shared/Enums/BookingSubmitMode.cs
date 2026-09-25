namespace HealthcareSupport.CaseEvaluation.Enums
{
    /// <summary>
    /// Which booking flow an atomic submit represents. Item B PR2 (2026-08-22).
    ///
    /// <para><b>Why this exists rather than reusing <c>AppointmentLifecycleFlow</c>.</b> That enum
    /// lives in the Domain project, and Application.Contracts references only Domain.Shared, so a
    /// request DTO cannot carry it. This is the wire vocabulary; the app service maps it onto
    /// <c>AppointmentLifecycleFlow</c> explicitly. The mapping is written out rather than relying on
    /// the numeric values lining up, so renumbering either enum cannot silently redirect a flow.</para>
    ///
    /// <para>Every value except <see cref="Create"/> requires a source confirmation number and runs
    /// that flow's own eligibility gate. Those gates are authorization, not decoration -- the submit
    /// path must not become a way around them.</para>
    /// </summary>
    public enum BookingSubmitMode
    {
        /// <summary>
        /// A first booking with no antecedent. The default, so an older client that omits the field
        /// keeps behaving exactly as it did.
        /// </summary>
        Create = 0,

        /// <summary>
        /// Re-entering a REJECTED request under its original confirmation number.
        /// </summary>
        ReSubmit = 1,

        /// <summary>
        /// A follow-up to an evaluation that DID happen. Links on the re-eval chain.
        /// </summary>
        Reval = 2,

        /// <summary>
        /// Booking again after an appointment that did NOT happen (cancelled, no-showed, not-seen).
        /// Links on the replacement chain, not the re-eval chain.
        /// </summary>
        ReBook = 3,
    }
}
