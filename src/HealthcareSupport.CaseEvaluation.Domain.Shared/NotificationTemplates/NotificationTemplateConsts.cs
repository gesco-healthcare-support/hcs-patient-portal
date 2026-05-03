namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

public static class NotificationTemplateConsts
{
    public const int TemplateCodeMaxLength = 100;
    public const int SubjectMaxLength = 200;
    public const int DescriptionMaxLength = 200;

    // Template-code constants -- match OLD's EmailTemplate / TemplateCode enum
    // names verbatim for strict parity (Phase 1.3, 2026-05-01).
    public static class Codes
    {
        public const string UserRegistered = "UserRegistered";
        public const string ResetPassword = "ResetPassword";
        public const string PasswordChange = "PasswordChange";
        public const string AddInternalUser = "AddInternalUser";
        public const string AppointmentRequest = "AppointmentRequest";
        public const string AppointmentApproved = "AppointmentApproved";
        public const string AppointmentApprovedStakeholder = "AppointmentApprovedStakeholder";
        public const string AppointmentRejected = "AppointmentRejected";
        public const string ResponsibleUserAssigned = "ResponsibleUserAssigned";
        public const string PatientDocumentUploaded = "PatientDocumentUploaded";
        public const string PatientDocumentAccepted = "PatientDocumentAccepted";
        public const string PatientDocumentRejected = "PatientDocumentRejected";
        public const string PackageDocumentsReminder = "PackageDocumentsReminder";
        public const string JDFReminder = "JDFReminder";
        public const string JDFAutoCancelled = "JDFAutoCancelled";
        public const string DueDateApproachingReminder = "DueDateApproachingReminder";
        public const string CancellationRequestSubmitted = "CancellationRequestSubmitted";
        public const string CancellationRequestAccepted = "CancellationRequestAccepted";
        public const string CancellationRequestRejected = "CancellationRequestRejected";
        public const string RescheduleRequested = "RescheduleRequested";
        public const string RescheduleApproved = "RescheduleApproved";
        public const string RescheduleRejected = "RescheduleRejected";
        public const string AccessorInvited = "AccessorInvited";
    }
}
