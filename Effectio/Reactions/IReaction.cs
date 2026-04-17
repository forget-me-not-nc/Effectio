using System;
using Effectio.Entities;
using Effectio.Statuses;

namespace Effectio.Reactions
{
    public enum ReactionResultType
    {
        RemoveStatus,
        ApplyStatus,
        ApplyEffect,
        AdjustStat,
        Custom
    }

    /// <summary>
    /// Context passed to <see cref="IReactionResult.Execute"/>. Bundles the entity the
    /// reaction is firing on, the status engine, and delegates for cross-cutting operations
    /// (stat adjust, effect apply) that the <see cref="ReactionEngine"/> wires to its host manager.
    /// </summary>
    public struct ReactionResultContext
    {
        public IEffectioEntity Entity;
        public IStatusEngine StatusEngine;
        public Action<IEffectioEntity, string, float> AdjustStat;
        public Action<IEffectioEntity, string> ApplyEffect;
    }

    public interface IReactionResult
    {
        ReactionResultType Type { get; }
        string TargetKey { get; }
        float Value { get; }

        /// <summary>Execute this result against <paramref name="ctx"/>.</summary>
        void Execute(in ReactionResultContext ctx);
    }

    public interface IReaction
    {
        string Key { get; }
        string[] RequiredStatusKeys { get; }
        string[] RequiredTags { get; }
        bool ConsumesStatuses { get; }
        IReactionResult[] Results { get; }
    }

    /// <summary>
    /// Optional opt-in extension of <see cref="IReaction"/> exposing a priority tier.
    /// Reactions implementing this interface fire in the order their <see cref="Priority"/>
    /// dictates (higher first); their consumed statuses are removed before the next-lower
    /// tier re-evaluates, so a high-priority reaction can preempt overlapping low-priority
    /// ones in the same tick. Reactions that do NOT implement this interface are treated
    /// as priority 0 - identical to the v1.0 "fire simultaneously" behaviour.
    /// </summary>
    /// <remarks>
    /// Kept as a separate interface (rather than added to <see cref="IReaction"/>) to
    /// preserve binary compatibility for any v1.0 consumer that implemented
    /// <see cref="IReaction"/> directly.
    /// </remarks>
    public interface IPrioritizedReaction : IReaction
    {
        int Priority { get; }
    }
}

