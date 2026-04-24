using System;
using Effectio.Entities;

namespace Effectio.Statuses
{
    public interface IStatusEngine
    {
        void RegisterStatus(IStatus status);
        void ApplyStatus(IEffectioEntity entity, string statusKey);
        void RemoveStatus(IEffectioEntity entity, string statusKey);
        bool HasStatus(IEffectioEntity entity, string statusKey);
        int GetStacks(IEffectioEntity entity, string statusKey);
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
