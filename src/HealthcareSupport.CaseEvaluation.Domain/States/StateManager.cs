using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.ClaimExaminers;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.WcabOffices;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.States;

public class StateManager : DomainService
{
    protected IStateRepository _stateRepository;
    protected IRepository<Location, Guid> _locationRepository;
    protected IRepository<WcabOffice, Guid> _wcabOfficeRepository;
    protected IRepository<Patient, Guid> _patientRepository;
    protected IRepository<ApplicantAttorney, Guid> _applicantAttorneyRepository;
    protected IRepository<DefenseAttorney, Guid> _defenseAttorneyRepository;
    protected IRepository<ClaimExaminer, Guid> _claimExaminerRepository;

    // State is host-scoped (no IMultiTenant), but several referencing masters
    // are tenant-scoped. Disabling the IMultiTenant filter during the in-use
    // probe ensures a reference in any tenant blocks deletion of the shared
    // host-scoped State row. Mirrors LocationManager.EnsureCanDeleteAsync.
    protected IDataFilter<IMultiTenant> _multiTenantFilter;

    public StateManager(
        IStateRepository stateRepository,
        IRepository<Location, Guid> locationRepository,
        IRepository<WcabOffice, Guid> wcabOfficeRepository,
        IRepository<Patient, Guid> patientRepository,
        IRepository<ApplicantAttorney, Guid> applicantAttorneyRepository,
        IRepository<DefenseAttorney, Guid> defenseAttorneyRepository,
        IRepository<ClaimExaminer, Guid> claimExaminerRepository,
        IDataFilter<IMultiTenant> multiTenantFilter)
    {
        _stateRepository = stateRepository;
        _locationRepository = locationRepository;
        _wcabOfficeRepository = wcabOfficeRepository;
        _patientRepository = patientRepository;
        _applicantAttorneyRepository = applicantAttorneyRepository;
        _defenseAttorneyRepository = defenseAttorneyRepository;
        _claimExaminerRepository = claimExaminerRepository;
        _multiTenantFilter = multiTenantFilter;
    }

    public virtual async Task<State> CreateAsync(string name)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        // Admin-created rows are never system rows.
        var state = new State(GuidGenerator.Create(), name, isSystem: false);
        return await _stateRepository.InsertAsync(state);
    }

    public virtual async Task<State> UpdateAsync(Guid id, string name, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        var state = await _stateRepository.GetAsync(id);
        EnsureNotSystem(state);
        state.Name = name;
        state.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _stateRepository.UpdateAsync(state);
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        var entity = await _stateRepository.FindAsync(id);
        if (entity == null)
        {
            return;
        }
        EnsureNotSystem(entity);
        await EnsureNotInUseAsync(id);
        await _stateRepository.DeleteAsync(entity);
    }

    /// <summary>
    /// Block deleting a state still referenced by any entity master via its
    /// StateId (Location, WcabOffice, Patient, ApplicantAttorney,
    /// DefenseAttorney, ClaimExaminer). Mirrors the AppointmentDocumentType
    /// in-use guard, summing references across the masters.
    /// </summary>
    private async Task EnsureNotInUseAsync(Guid id)
    {
        long total;
        using (_multiTenantFilter.Disable())
        {
            total = await _locationRepository.CountAsync(x => x.StateId == id)
                + await _wcabOfficeRepository.CountAsync(x => x.StateId == id)
                + await _patientRepository.CountAsync(x => x.StateId == id)
                + await _applicantAttorneyRepository.CountAsync(x => x.StateId == id)
                + await _defenseAttorneyRepository.CountAsync(x => x.StateId == id)
                + await _claimExaminerRepository.CountAsync(x => x.StateId == id);
        }

        if (total > 0)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.StateInUse);
        }
    }

    private static void EnsureNotSystem(State entity)
    {
        if (entity.IsSystem)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.StateSystemReadOnly);
        }
    }
}
