using System;
using Effectio.Entities;

namespace Effectio.Statuses
{
    public interface IStatusEngine
    {
        void RegisterStatus(IStatus status);

        /// <summary>
        /// Applies the status to <paramref name="entity"/>, and says which of three things
        /// happened by which event it raises:
        ///
        /// <list type="bullet">
        /// <item><description>not present - created at one stack, <see cref="OnStatusApplied"/>
        /// fires;</description></item>
        /// <item><description>present and below <see cref="IStatus.MaxStacks"/> - the count
        /// goes up, <see cref="IStackOperations.OnStatusStacked"/> fires;</description></item>
        /// <item><description>present and at the cap - nothing changes but the timer, and
        /// neither of those events fires.</description></item>
        /// </list>
        ///
        /// All three refresh the duration. See <see cref="IStatus.Duration"/> for the whole
        /// stack-expiration contract, which is written down once and only there.
        /// </summary>
        void ApplyStatus(IEffectioEntity entity, string statusKey);

        void RemoveStatus(IEffectioEntity entity, string statusKey);
        bool HasStatus(IEffectioEntity entity, string statusKey);
        int GetStacks(IEffectioEntity entity, string statusKey);
        /// <summary>
        /// The registered definition for <paramref name="statusKey"/>, or <c>null</c> when
        /// none was registered. Returns null rather than throwing, unlike
        /// <see cref="Core.IEffectioManager.GetEntity"/> and
        /// <see cref="Entities.IEffectioEntity.GetStat"/> - a status key naming nothing is an
        /// ordinary state while content is being written, not a programming error.
        /// </summary>
        IStatus GetStatusDefinition(string statusKey);
        void Tick(float deltaTime);

        void GrantImmunity(IEffectioEntity entity, string statusKey);
        void RevokeImmunity(IEffectioEntity entity, string statusKey);
        bool IsImmune(IEffectioEntity entity, string statusKey);

        event Action<IEffectioEntity, string> OnStatusBlocked;

        event Action<IEffectioEntity, string> OnStatusApplied;
        event Action<IEffectioEntity, string> OnStatusRemoved;
        event Action<IEffectioEntity, string> OnStatusExpired;

        /// <summary>
        /// Fires every time ApplyStatus is called against an entity that already has the
        /// status (whether the stack counter increments or is already at MaxStacks).
        /// The combined RemainingDuration has been refreshed by the time this fires.
        /// Does NOT fire on first application (use OnStatusApplied) or on
        /// IStackOperations.RemoveStacks partial decrement.
        /// </summary>
        event Action<IEffectioEntity, string> OnStatusRefreshed;
    }
}
