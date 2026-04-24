using System;
using Effectio.Entities;

namespace Effectio.Statuses
{
    public interface IStatusEngine
    {
        void RegisterStatus(IStatus status);

        /// <summary>
        /// Apply the status to <paramref name="entity"/>. If the status is not yet
        /// present, creates it (stacks = 1, RemainingDuration = <see cref="IStatus.Duration"/>)
        /// and fires <see cref="OnStatusApplied"/>. If the status is already present and
        /// <em>below</em> its <see cref="IStatus.MaxStacks"/> cap, increments stacks and
        /// fires <see cref="IStackOperations.OnStatusStacked"/>. In all
        /// existing-status paths (including at-max), refreshes the combined
        /// <c>RemainingDuration</c> back to <see cref="IStatus.Duration"/>.
        /// </summary>
        /// <remarks>
        /// See <see cref="IStatus.Duration"/> for the full v1.x stack-expiration
        /// contract: all stacks share one duration; expiration removes the entire
        /// status as a single event.
        /// </remarks>
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
    }
}
