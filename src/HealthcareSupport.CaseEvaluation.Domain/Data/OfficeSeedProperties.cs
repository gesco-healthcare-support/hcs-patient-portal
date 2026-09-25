namespace HealthcareSupport.CaseEvaluation.Data;

/// <summary>
/// DataSeedContext property keys carrying a runtime-created office's owner doctor
/// (first name / last name / email) from the New Practice create flow
/// (DoctorTenantAppService) into the tenant-scope seed (DoctorProfileDataSeedContributor).
/// Absent on the DbMigrator seed path, which falls back to the OfficeSeedData config or,
/// failing that, the tenant-name placeholder.
/// </summary>
public static class OfficeSeedProperties
{
    public const string DoctorFirstName = "OfficeDoctorFirstName";
    public const string DoctorLastName = "OfficeDoctorLastName";
    public const string DoctorEmail = "OfficeDoctorEmail";
}
