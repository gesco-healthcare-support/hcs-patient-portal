using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.WcabOffices;
using Volo.Saas.Tenants;
using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;
using HealthcareSupport.CaseEvaluation.AppointmentStatuses;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using System;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.States;
using System.Linq;
using System.Collections.Generic;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;
using HealthcareSupport.CaseEvaluation.Books;

[assembly: MapperDefaults(RequiredMappingStrategy = RequiredMappingStrategy.Target)]

namespace HealthcareSupport.CaseEvaluation;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class CaseEvaluationBookToBookDtoMapper : MapperBase<Book, BookDto>
{
    public override partial BookDto Map(Book source);
    public override partial void Map(Book source, BookDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class CaseEvaluationCreateUpdateBookDtoToBookMapper : MapperBase<CreateUpdateBookDto, Book>
{
    [MapperIgnoreTarget(nameof(Book.ConcurrencyStamp))]
    [MapperIgnoreTarget(nameof(Book.CreationTime))]
    [MapperIgnoreTarget(nameof(Book.CreatorId))]
    [MapperIgnoreTarget(nameof(Book.LastModificationTime))]
    [MapperIgnoreTarget(nameof(Book.LastModifierId))]
    public override partial Book Map(CreateUpdateBookDto source);

    [MapperIgnoreTarget(nameof(Book.ConcurrencyStamp))]
    [MapperIgnoreTarget(nameof(Book.CreationTime))]
    [MapperIgnoreTarget(nameof(Book.CreatorId))]
    [MapperIgnoreTarget(nameof(Book.LastModificationTime))]
    [MapperIgnoreTarget(nameof(Book.LastModifierId))]
    public override partial void Map(CreateUpdateBookDto source, Book destination);
}

[Mapper]
public partial class StateToStateDtoMappers : MapperBase<State, StateDto>
{
    public override partial StateDto Map(State source);
    public override partial void Map(State source, StateDto destination);
}

[Mapper]
public partial class AppointmentTypeToAppointmentTypeDtoMappers : MapperBase<AppointmentType, AppointmentTypeDto>
{
    public override partial AppointmentTypeDto Map(AppointmentType source);
    public override partial void Map(AppointmentType source, AppointmentTypeDto destination);
}

[Mapper]
public partial class AppointmentTypeToAppointmentTypeExcelDtoMappers : MapperBase<AppointmentType, AppointmentTypeExcelDto>
{
    public override partial AppointmentTypeExcelDto Map(AppointmentType source);
    public override partial void Map(AppointmentType source, AppointmentTypeExcelDto destination);
}

[Mapper]
public partial class AppointmentStatusToAppointmentStatusDtoMappers : MapperBase<AppointmentStatus, AppointmentStatusDto>
{
    public override partial AppointmentStatusDto Map(AppointmentStatus source);
    public override partial void Map(AppointmentStatus source, AppointmentStatusDto destination);
}

[Mapper]
public partial class AppointmentLanguageToAppointmentLanguageDtoMappers : MapperBase<AppointmentLanguage, AppointmentLanguageDto>
{
    public override partial AppointmentLanguageDto Map(AppointmentLanguage source);
    public override partial void Map(AppointmentLanguage source, AppointmentLanguageDto destination);
}

[Mapper]
public partial class LocationToLocationDtoMappers : MapperBase<Location, LocationDto>
{
    public override partial LocationDto Map(Location source);
    public override partial void Map(Location source, LocationDto destination);
}

[Mapper]
public partial class LocationWithNavigationPropertiesToLocationWithNavigationPropertiesDtoMapper : MapperBase<LocationWithNavigationProperties, LocationWithNavigationPropertiesDto>
{
    public override partial LocationWithNavigationPropertiesDto Map(LocationWithNavigationProperties source);
    public override partial void Map(LocationWithNavigationProperties source, LocationWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class StateToLookupDtoGuidMapper : MapperBase<State, LookupDto<Guid>>
{
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial LookupDto<Guid> Map(State source);
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial void Map(State source, LookupDto<Guid> destination);

    public override void AfterMap(State source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Name;
    }
}

[Mapper]
public partial class AppointmentTypeToLookupDtoGuidMapper : MapperBase<AppointmentType, LookupDto<Guid>>
{
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial LookupDto<Guid> Map(AppointmentType source);
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial void Map(AppointmentType source, LookupDto<Guid> destination);

    public override void AfterMap(AppointmentType source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Name;
    }
}

[Mapper]
public partial class WcabOfficeToWcabOfficeDtoMappers : MapperBase<WcabOffice, WcabOfficeDto>
{
    public override partial WcabOfficeDto Map(WcabOffice source);
    public override partial void Map(WcabOffice source, WcabOfficeDto destination);
}

[Mapper]
public partial class WcabOfficeToWcabOfficeExcelDtoMappers : MapperBase<WcabOffice, WcabOfficeExcelDto>
{
    public override partial WcabOfficeExcelDto Map(WcabOffice source);
    public override partial void Map(WcabOffice source, WcabOfficeExcelDto destination);
}

[Mapper]
public partial class WcabOfficeWithNavigationPropertiesToWcabOfficeWithNavigationPropertiesDtoMapper : MapperBase<WcabOfficeWithNavigationProperties, WcabOfficeWithNavigationPropertiesDto>
{
    public override partial WcabOfficeWithNavigationPropertiesDto Map(WcabOfficeWithNavigationProperties source);
    public override partial void Map(WcabOfficeWithNavigationProperties source, WcabOfficeWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class WcabOfficeToLookupDtoGuidMapper : MapperBase<WcabOffice, LookupDto<Guid>>
{
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial LookupDto<Guid> Map(WcabOffice source);
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial void Map(WcabOffice source, LookupDto<Guid> destination);

    public override void AfterMap(WcabOffice source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Name;
    }
}

[Mapper]
public partial class DoctorToDoctorDtoMappers : MapperBase<Doctor, DoctorDto>
{
    public override partial DoctorDto Map(Doctor source);
    public override partial void Map(Doctor source, DoctorDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class DoctorWithNavigationPropertiesToDoctorWithNavigationPropertiesDtoMapper : MapperBase<DoctorWithNavigationProperties, DoctorWithNavigationPropertiesDto>
{
    public override partial DoctorWithNavigationPropertiesDto Map(DoctorWithNavigationProperties source);
    public override partial void Map(DoctorWithNavigationProperties source, DoctorWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class IdentityUserToLookupDtoGuidMapper : MapperBase<IdentityUser, LookupDto<Guid>>
{
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial LookupDto<Guid> Map(IdentityUser source);
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial void Map(IdentityUser source, LookupDto<Guid> destination);

    public override void AfterMap(IdentityUser source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Email;
    }
}

[Mapper]
public partial class TenantToLookupDtoGuidMapper : MapperBase<Tenant, LookupDto<Guid>>
{
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial LookupDto<Guid> Map(Tenant source);
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial void Map(Tenant source, LookupDto<Guid> destination);

    public override void AfterMap(Tenant source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Name;
    }
}

[Mapper]
public partial class LocationToLookupDtoGuidMapper : MapperBase<Location, LookupDto<Guid>>
{
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial LookupDto<Guid> Map(Location source);
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial void Map(Location source, LookupDto<Guid> destination);

    public override void AfterMap(Location source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Name;
    }
}

[Mapper]
public partial class DoctorAvailabilityToDoctorAvailabilityDtoMappers : MapperBase<DoctorAvailability, DoctorAvailabilityDto>
{
    public override partial DoctorAvailabilityDto Map(DoctorAvailability source);
    public override partial void Map(DoctorAvailability source, DoctorAvailabilityDto destination);
}

[Mapper]
public partial class DoctorAvailabilityWithNavigationPropertiesToDoctorAvailabilityWithNavigationPropertiesDtoMapper : MapperBase<DoctorAvailabilityWithNavigationProperties, DoctorAvailabilityWithNavigationPropertiesDto>
{
    public override partial DoctorAvailabilityWithNavigationPropertiesDto Map(DoctorAvailabilityWithNavigationProperties source);
    public override partial void Map(DoctorAvailabilityWithNavigationProperties source, DoctorAvailabilityWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class PatientToPatientDtoMappers : MapperBase<Patient, PatientDto>
{
    public override partial PatientDto Map(Patient source);
    public override partial void Map(Patient source, PatientDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class PatientWithNavigationPropertiesToPatientWithNavigationPropertiesDtoMapper : MapperBase<PatientWithNavigationProperties, PatientWithNavigationPropertiesDto>
{
    public override partial PatientWithNavigationPropertiesDto Map(PatientWithNavigationProperties source);
    public override partial void Map(PatientWithNavigationProperties source, PatientWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class AppointmentLanguageToLookupDtoGuidMapper : MapperBase<AppointmentLanguage, LookupDto<Guid>>
{
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial LookupDto<Guid> Map(AppointmentLanguage source);
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial void Map(AppointmentLanguage source, LookupDto<Guid> destination);

    public override void AfterMap(AppointmentLanguage source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Name;
    }
}

[Mapper]
public partial class AppointmentToAppointmentDtoMappers : MapperBase<Appointment, AppointmentDto>
{
    public override partial AppointmentDto Map(Appointment source);
    public override partial void Map(Appointment source, AppointmentDto destination);
}

[Mapper]
public partial class AppointmentDocumentToDtoMapper : MapperBase<HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocument, HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocumentDto>
{
    public override partial HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocumentDto Map(HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocument source);
    public override partial void Map(HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocument source, HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocumentDto destination);
}

[Mapper]
public partial class AppointmentPacketToDtoMapper : MapperBase<HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentPacket, HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentPacketDto>
{
    public override partial HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentPacketDto Map(HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentPacket source);
    public override partial void Map(HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentPacket source, HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentPacketDto destination);
}

[Mapper]
public partial class AppointmentSendBackInfoToDtoMapper : MapperBase<AppointmentSendBackInfo, AppointmentSendBackInfoDto>
{
    [MapperIgnoreTarget(nameof(AppointmentSendBackInfoDto.FlaggedFields))]
    public override partial AppointmentSendBackInfoDto Map(AppointmentSendBackInfo source);
    [MapperIgnoreTarget(nameof(AppointmentSendBackInfoDto.FlaggedFields))]
    public override partial void Map(AppointmentSendBackInfo source, AppointmentSendBackInfoDto destination);

    public override void AfterMap(AppointmentSendBackInfo source, AppointmentSendBackInfoDto destination)
    {
        destination.FlaggedFields = source.GetFlaggedFields().ToList();
    }
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class AppointmentWithNavigationPropertiesToAppointmentWithNavigationPropertiesDtoMapper : MapperBase<AppointmentWithNavigationProperties, AppointmentWithNavigationPropertiesDto>
{
    public override partial AppointmentWithNavigationPropertiesDto Map(AppointmentWithNavigationProperties source);
    public override partial void Map(AppointmentWithNavigationProperties source, AppointmentWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class AppointmentStatusToLookupDtoGuidMapper : MapperBase<AppointmentStatus, LookupDto<Guid>>
{
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial LookupDto<Guid> Map(AppointmentStatus source);
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial void Map(AppointmentStatus source, LookupDto<Guid> destination);

    public override void AfterMap(AppointmentStatus source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Name;
    }
}

[Mapper]
public partial class PatientToLookupDtoGuidMapper : MapperBase<Patient, LookupDto<Guid>>
{
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial LookupDto<Guid> Map(Patient source);
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial void Map(Patient source, LookupDto<Guid> destination);

    public override void AfterMap(Patient source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.Email;
    }
}

[Mapper]
public partial class DoctorAvailabilityToLookupDtoGuidMapper : MapperBase<DoctorAvailability, LookupDto<Guid>>
{
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial LookupDto<Guid> Map(DoctorAvailability source);
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial void Map(DoctorAvailability source, LookupDto<Guid> destination);

    public override void AfterMap(DoctorAvailability source, LookupDto<Guid> destination)
    {
        destination.DisplayName = $"{source.AvailableDate:yyyy-MM-dd} {source.FromTime}-{source.ToTime}";
    }
}

[Mapper]
public partial class AppointmentEmployerDetailToAppointmentEmployerDetailDtoMappers : MapperBase<AppointmentEmployerDetail, AppointmentEmployerDetailDto>
{
    public override partial AppointmentEmployerDetailDto Map(AppointmentEmployerDetail source);
    public override partial void Map(AppointmentEmployerDetail source, AppointmentEmployerDetailDto destination);
}

[Mapper]
public partial class AppointmentEmployerDetailWithNavigationPropertiesToAppointmentEmployerDetailWithNavigationPropertiesDtoMapper : MapperBase<AppointmentEmployerDetailWithNavigationProperties, AppointmentEmployerDetailWithNavigationPropertiesDto>
{
    public override partial AppointmentEmployerDetailWithNavigationPropertiesDto Map(AppointmentEmployerDetailWithNavigationProperties source);
    public override partial void Map(AppointmentEmployerDetailWithNavigationProperties source, AppointmentEmployerDetailWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class AppointmentToLookupDtoGuidMapper : MapperBase<Appointment, LookupDto<Guid>>
{
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial LookupDto<Guid> Map(Appointment source);
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial void Map(Appointment source, LookupDto<Guid> destination);

    public override void AfterMap(Appointment source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.RequestConfirmationNumber;
    }
}

[Mapper]
public partial class AppointmentAccessorToAppointmentAccessorDtoMappers : MapperBase<AppointmentAccessor, AppointmentAccessorDto>
{
    public override partial AppointmentAccessorDto Map(AppointmentAccessor source);
    public override partial void Map(AppointmentAccessor source, AppointmentAccessorDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class AppointmentAccessorWithNavigationPropertiesToAppointmentAccessorWithNavigationPropertiesDtoMapper : MapperBase<AppointmentAccessorWithNavigationProperties, AppointmentAccessorWithNavigationPropertiesDto>
{
    public override partial AppointmentAccessorWithNavigationPropertiesDto Map(AppointmentAccessorWithNavigationProperties source);
    public override partial void Map(AppointmentAccessorWithNavigationProperties source, AppointmentAccessorWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class ApplicantAttorneyToApplicantAttorneyDtoMappers : MapperBase<ApplicantAttorney, ApplicantAttorneyDto>
{
    public override partial ApplicantAttorneyDto Map(ApplicantAttorney source);
    public override partial void Map(ApplicantAttorney source, ApplicantAttorneyDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class ApplicantAttorneyWithNavigationPropertiesToApplicantAttorneyWithNavigationPropertiesDtoMapper : MapperBase<ApplicantAttorneyWithNavigationProperties, ApplicantAttorneyWithNavigationPropertiesDto>
{
    public override partial ApplicantAttorneyWithNavigationPropertiesDto Map(ApplicantAttorneyWithNavigationProperties source);
    public override partial void Map(ApplicantAttorneyWithNavigationProperties source, ApplicantAttorneyWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class AppointmentApplicantAttorneyToAppointmentApplicantAttorneyDtoMappers : MapperBase<AppointmentApplicantAttorney, AppointmentApplicantAttorneyDto>
{
    public override partial AppointmentApplicantAttorneyDto Map(AppointmentApplicantAttorney source);
    public override partial void Map(AppointmentApplicantAttorney source, AppointmentApplicantAttorneyDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class AppointmentApplicantAttorneyWithNavigationPropertiesToAppointmentApplicantAttorneyWithNavigationPropertiesDtoMapper : MapperBase<AppointmentApplicantAttorneyWithNavigationProperties, AppointmentApplicantAttorneyWithNavigationPropertiesDto>
{
    public override partial AppointmentApplicantAttorneyWithNavigationPropertiesDto Map(AppointmentApplicantAttorneyWithNavigationProperties source);
    public override partial void Map(AppointmentApplicantAttorneyWithNavigationProperties source, AppointmentApplicantAttorneyWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class ApplicantAttorneyToLookupDtoGuidMapper : MapperBase<ApplicantAttorney, LookupDto<Guid>>
{
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial LookupDto<Guid> Map(ApplicantAttorney source);
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial void Map(ApplicantAttorney source, LookupDto<Guid> destination);

    public override void AfterMap(ApplicantAttorney source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.FirmName ?? string.Empty;
    }
}

[Mapper]
public partial class DefenseAttorneyToDefenseAttorneyDtoMappers : MapperBase<DefenseAttorney, DefenseAttorneyDto>
{
    public override partial DefenseAttorneyDto Map(DefenseAttorney source);
    public override partial void Map(DefenseAttorney source, DefenseAttorneyDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class DefenseAttorneyWithNavigationPropertiesToDefenseAttorneyWithNavigationPropertiesDtoMapper : MapperBase<DefenseAttorneyWithNavigationProperties, DefenseAttorneyWithNavigationPropertiesDto>
{
    public override partial DefenseAttorneyWithNavigationPropertiesDto Map(DefenseAttorneyWithNavigationProperties source);
    public override partial void Map(DefenseAttorneyWithNavigationProperties source, DefenseAttorneyWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class AppointmentDefenseAttorneyToAppointmentDefenseAttorneyDtoMappers : MapperBase<AppointmentDefenseAttorney, AppointmentDefenseAttorneyDto>
{
    public override partial AppointmentDefenseAttorneyDto Map(AppointmentDefenseAttorney source);
    public override partial void Map(AppointmentDefenseAttorney source, AppointmentDefenseAttorneyDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class AppointmentDefenseAttorneyWithNavigationPropertiesToAppointmentDefenseAttorneyWithNavigationPropertiesDtoMapper : MapperBase<AppointmentDefenseAttorneyWithNavigationProperties, AppointmentDefenseAttorneyWithNavigationPropertiesDto>
{
    public override partial AppointmentDefenseAttorneyWithNavigationPropertiesDto Map(AppointmentDefenseAttorneyWithNavigationProperties source);
    public override partial void Map(AppointmentDefenseAttorneyWithNavigationProperties source, AppointmentDefenseAttorneyWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class DefenseAttorneyToLookupDtoGuidMapper : MapperBase<DefenseAttorney, LookupDto<Guid>>
{
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial LookupDto<Guid> Map(DefenseAttorney source);
    [MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]
    public override partial void Map(DefenseAttorney source, LookupDto<Guid> destination);

    public override void AfterMap(DefenseAttorney source, LookupDto<Guid> destination)
    {
        destination.DisplayName = source.FirmName ?? string.Empty;
    }
}

[Mapper]
public partial class AppointmentTypeFieldConfigToAppointmentTypeFieldConfigDtoMapper : MapperBase<HealthcareSupport.CaseEvaluation.AppointmentTypeFieldConfigs.AppointmentTypeFieldConfig, HealthcareSupport.CaseEvaluation.AppointmentTypeFieldConfigs.AppointmentTypeFieldConfigDto>
{
    public override partial HealthcareSupport.CaseEvaluation.AppointmentTypeFieldConfigs.AppointmentTypeFieldConfigDto Map(HealthcareSupport.CaseEvaluation.AppointmentTypeFieldConfigs.AppointmentTypeFieldConfig source);
    public override partial void Map(HealthcareSupport.CaseEvaluation.AppointmentTypeFieldConfigs.AppointmentTypeFieldConfig source, HealthcareSupport.CaseEvaluation.AppointmentTypeFieldConfigs.AppointmentTypeFieldConfigDto destination);
}

[Mapper]
public partial class AppointmentInjuryDetailToAppointmentInjuryDetailDtoMapper : MapperBase<AppointmentInjuryDetail, AppointmentInjuryDetailDto>
{
    public override partial AppointmentInjuryDetailDto Map(AppointmentInjuryDetail source);
    public override partial void Map(AppointmentInjuryDetail source, AppointmentInjuryDetailDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public partial class AppointmentInjuryDetailWithNavigationPropertiesToAppointmentInjuryDetailWithNavigationPropertiesDtoMapper : MapperBase<AppointmentInjuryDetailWithNavigationProperties, AppointmentInjuryDetailWithNavigationPropertiesDto>
{
    public override partial AppointmentInjuryDetailWithNavigationPropertiesDto Map(AppointmentInjuryDetailWithNavigationProperties source);
    public override partial void Map(AppointmentInjuryDetailWithNavigationProperties source, AppointmentInjuryDetailWithNavigationPropertiesDto destination);
}

[Mapper]
public partial class AppointmentBodyPartToAppointmentBodyPartDtoMapper : MapperBase<AppointmentBodyPart, AppointmentBodyPartDto>
{
    public override partial AppointmentBodyPartDto Map(AppointmentBodyPart source);
    public override partial void Map(AppointmentBodyPart source, AppointmentBodyPartDto destination);
}

[Mapper]
public partial class AppointmentClaimExaminerToAppointmentClaimExaminerDtoMapper : MapperBase<AppointmentClaimExaminer, AppointmentClaimExaminerDto>
{
    public override partial AppointmentClaimExaminerDto Map(AppointmentClaimExaminer source);
    public override partial void Map(AppointmentClaimExaminer source, AppointmentClaimExaminerDto destination);
}

[Mapper]
public partial class AppointmentPrimaryInsuranceToAppointmentPrimaryInsuranceDtoMapper : MapperBase<AppointmentPrimaryInsurance, AppointmentPrimaryInsuranceDto>
{
    public override partial AppointmentPrimaryInsuranceDto Map(AppointmentPrimaryInsurance source);
    public override partial void Map(AppointmentPrimaryInsurance source, AppointmentPrimaryInsuranceDto destination);
}
