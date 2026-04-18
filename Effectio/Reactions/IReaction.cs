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

    /// <summary>
    /// Minimum stack count of a particular status that a stack-aware reaction requires
    /// to fire. Used by <see cref="IStackAwareReaction.RequiredStacks"/>.
    /// </summary>
    public readonly struct StackRequirement
    {
        public readonly string StatusKey;
        public readonly int MinStacks;

        public StackRequirement(string statusKey, int minStacks)
        {
            StatusKey = statusKey;
            MinStacks = minStacks;
        }
    }

    /// <summary>
    /// Stack-decrement instruction applied when a stack-aware reaction fires.
    /// <see cref="Count"/> stacks are removed from <see cref="StatusKey"/>; if the
    /// remaining count would reach 0 the status is removed entirely. Per-key
    /// stack consumes take precedence over <see cref="IReaction.ConsumesStatuses"/>
    /// for keys they cover; keys not listed here fall through to the v1.0 flag.
    /// </summary>
    /// <remarks>
    /// v1.1 semantics: decrements the combined stack counter by <see cref="Count"/>.
    /// If a future release distinguishes individual stacks (each with its own
    /// expiration), the default refinement will be "remove the <see cref="Count"/>
    /// oldest stacks". Existing callers will continue to compile; behaviour for
    /// equal-age stacks will become deterministic at that point.
    /// </remarks>
    public readonly struct StackConsume
    {
        public readonly string StatusKey;
        public readonly int Count;

        public StackConsume(string statusKey, int count)
        {
            StatusKey = statusKey;
            Count = count;
        }
    }

    /// <summary>
    /// Optional opt-in extension of <see cref="IReaction"/> for reactions that gate
    /// on minimum stack counts and / or decrement stacks (rather than removing whole
    /// statuses) when they fire. Reactions that do NOT implement this interface
    /// behave exactly as in v1.0 / early v1.1 - their match is decided purely by
    /// <see cref="IReaction.RequiredStatusKeys"/> and <see cref="IReaction.RequiredTags"/>,
    /// and consumption is controlled solely by <see cref="IReaction.ConsumesStatuses"/>.
    /// </summary>
    /// <remarks>
    /// Kept as a separate interface (rather than added to <see cref="IReaction"/>) to
    /// preserve binary compatibility for any v1.0 consumer that implemented
    /// <see cref="IReaction"/> directly.
    /// </remarks>
    public interface IStackAwareReaction : IReaction
    {
        /// <summary>Minimum stack counts required for this reaction to match. Empty array if none.</summary>
        StackRequirement[] RequiredStacks { get; }

        /// <summary>Per-key stack-decrement instructions applied when the reaction fires. Empty array if none.</summary>
        StackConsume[] StackConsumes { get; }
    }
}

