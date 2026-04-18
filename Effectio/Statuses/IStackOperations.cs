using System;
using Effectio.Entities;

namespace Effectio.Statuses
{
    /// <summary>
    /// Optional opt-in extension of <see cref="IStatusEngine"/> exposing stack-level
    /// mutation operations and stack-change notifications. Status engines that do not
    /// implement this interface are presumed not to support partial-stack consumption;
    /// consumers (e.g. <see cref="Effectio.Reactions.ReactionEngine"/>'s stack-aware reactions)
    /// skip stack-decrement work for them.
    /// </summary>
    /// <remarks>
    /// Kept as a separate interface (rather than added to <see cref="IStatusEngine"/>)
    /// to preserve binary compatibility for any v1.0 consumer that implemented
    /// <see cref="IStatusEngine"/> directly.
    /// </remarks>
    public interface IStackOperations
    {
        /// <summary>
        /// Decrement <paramref name="statusKey"/>'s stack counter on
        /// <paramref name="entity"/> by <paramref name="count"/>.
        /// If the resulting count would be &lt;= 0, the status is removed entirely
        /// (and <see cref="IStatusEngine.OnStatusRemoved"/> fires).
        /// If <paramref name="count"/> is &lt;= 0 or the entity does not currently
        /// have the status, this is a no-op.
        /// </summary>
        /// <remarks>
        /// v1.1 semantics: operates on the combined stack counter held by
        /// <see cref="StatusEngine"/>. If a future release distinguishes
        /// individual stacks (each with its own expiration), the default
        /// refinement will be "remove the <paramref name="count"/> oldest stacks".
        /// Partial decrements do NOT fire <see cref="IStatusEngine.OnStatusRemoved"/>;
        /// only the full-removal transition does. They DO fire
        /// <see cref="OnStatusStacked"/> so consumers can react to the count change.
        /// </remarks>
        void RemoveStacks(IEffectioEntity entity, string statusKey, int count);

        /// <summary>
        /// Fires whenever a status's stack count changes WITHOUT the status
        /// being newly applied or fully removed. Triggered both by
        /// <see cref="IStatusEngine.ApplyStatus"/> when it increments an existing
        /// status's stacks, and by <see cref="RemoveStacks"/> when it performs a
        /// partial decrement. Does NOT fire when <see cref="IStatusEngine.ApplyStatus"/>
        /// is called against a status already at its <c>MaxStacks</c> cap (the
        /// counter does not change in that path - duration just refreshes).
        /// </summary>
        /// <remarks>
        /// The reaction engine subscribes to this event (via
        /// <see cref="Effectio.Core.EffectioManager"/>) so that
        /// <see cref="Effectio.Reactions.IStackAwareReaction.RequiredStacks"/> thresholds
        /// re-evaluate as stacks accumulate. Without this event, a reaction
        /// requiring 3 stacks of Burning would never fire because v1.0's
        /// <see cref="IStatusEngine.OnStatusApplied"/> only fires on the first
        /// application of a status, not on subsequent stack increments.
        /// </remarks>
        event Action<IEffectioEntity, string> OnStatusStacked;
    }
}
