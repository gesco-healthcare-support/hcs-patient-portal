using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.EventBus;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;

/// <summary>
/// Re-pushes the owning appointment when a row it owns is created, updated or deleted, so the Case Tracker's copy
/// stays a current FULL snapshot. Every intake body carries every party block; a child edit must therefore re-send
/// the whole appointment, never a partial body.
///
/// <para>One subclass per child type, naming only how to find the owning appointment. The publishing rules, the
/// settle gate and the content-keyed enqueue all live in
/// <see cref="AppointmentChangedHandler.RePushAfterChildChangeAsync"/>, so a child edit cannot be published under
/// different rules from an appointment edit.</para>
///
/// <para>A soft delete raises <see cref="EntityDeletedEventData{TEntity}"/>; by then the row is filtered out, so
/// the rebuilt body no longer carries it. A child write may ALSO raise the appointment's own update event (ABP's
/// <c>PublishEntityUpdatedEventWhenNavigationChanges</c>); both builds are identical, so the outbox collapses them
/// onto one row. Subclassing does not depend on that option.</para>
/// </summary>
public abstract class AppointmentChildChangedHandler<TChild> :
    ILocalEventHandler<EntityCreatedEventData<TChild>>,
    ILocalEventHandler<EntityUpdatedEventData<TChild>>,
    ILocalEventHandler<EntityDeletedEventData<TChild>>
    where TChild : class
{
    private readonly AppointmentChangedHandler _rePush;

    protected AppointmentChildChangedHandler(AppointmentChangedHandler rePush)
    {
        _rePush = rePush;
    }

    /// <summary>The id of the appointment that owns <paramref name="child"/>.</summary>
    protected abstract Guid AppointmentIdOf(TChild child);

    [UnitOfWork]
    public virtual Task HandleEventAsync(EntityCreatedEventData<TChild> eventData) =>
        OnChangedAsync(eventData?.Entity, "created");

    [UnitOfWork]
    public virtual Task HandleEventAsync(EntityUpdatedEventData<TChild> eventData) =>
        OnChangedAsync(eventData?.Entity, "updated");

    [UnitOfWork]
    public virtual Task HandleEventAsync(EntityDeletedEventData<TChild> eventData) =>
        OnChangedAsync(eventData?.Entity, "deleted");

    private Task OnChangedAsync(TChild? child, string change)
    {
        if (child == null)
        {
            return Task.CompletedTask;
        }

        return _rePush.RePushAfterChildChangeAsync(AppointmentIdOf(child), $"{typeof(TChild).Name} {change}");
    }
}
