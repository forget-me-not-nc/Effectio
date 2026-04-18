using Effectio.Entities;

namespace Effectio.Statuses
{
    /// <summary>
    /// Optional opt-in extension of <see cref="IStatusEngine"/> exposing stack-level
    /// mutation operations. Status engines that don't implement this interface are
    /// presumed not to support partial-stack consumption; consumers (e.g.
    /// <see cref="Reactions.ReactionEngine"/>'s stack-aware reactions) skip
    /// stack-decrement work for them.
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
        /// Partial decrements do NOT fire any event; only the full-removal
        /// transition fires <see cref="IStatusEngine.OnStatusRemoved"/>.
        /// </remarks>
        void RemoveStacks(IEffectioEntity entity, string statusKey, int count);
    }
}
