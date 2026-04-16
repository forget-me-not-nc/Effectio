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
}

