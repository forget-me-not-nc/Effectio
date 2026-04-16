using System;
using System.Collections.Generic;
using System.Linq;
using Effectio.Common.Logging;
using Effectio.Entities;
using Effectio.Statuses;

namespace Effectio.Reactions
{
    public class ReactionEngine : IReactionEngine
    {
        private readonly IEffectioLogger _logger;
        private readonly IStatusEngine _statusEngine;
        private readonly List<IReaction> _reactions = new List<IReaction>();

        public int MaxChainDepth { get; set; } = 5;

        public event Action<IEffectioEntity, IReaction> OnReactionTriggered;

        /// <summary>
        /// Called by ReactionEngine when a result needs to adjust a stat directly.
        /// Wired up by EffectioManager.
        /// </summary>
        internal Action<IEffectioEntity, string, float> OnAdjustStat { get; set; }

        /// <summary>
        /// Called by ReactionEngine when a result needs to apply an effect.
        /// Wired up by EffectioManager.
        /// </summary>
        internal Action<IEffectioEntity, string> OnApplyEffect { get; set; }

        public ReactionEngine(IStatusEngine statusEngine, IEffectioLogger logger = null)
        {
            _statusEngine = statusEngine;
            _logger = logger ?? VoidLogger.Instance;
        }

        public void RegisterReaction(IReaction reaction)
        {
            _reactions.Add(reaction);
            _logger.Info($"Reaction '{reaction.Key}' registered.");
        }

        public void RemoveReaction(string reactionKey)
        {
            _reactions.RemoveAll(r => r.Key == reactionKey);
        }

        public void CheckReactions(IEffectioEntity entity)
        {
            int depth = 0;

            while (depth < MaxChainDepth)
            {
                var triggered = FindMatchingReactions(entity);
                if (triggered.Count == 0)
                    break;

                // Collect all statuses to consume (after all reactions in this pass)
                var statusesToRemove = new HashSet<string>();
                // Collect all new statuses to add (for chain detection)
                var previousStatuses = new HashSet<string>(entity.ActiveStatusKeys);

                // Execute ALL matching reactions simultaneously
                foreach (var reaction in triggered)
                {
                    _logger.Info($"Reaction '{reaction.Key}' triggered on entity '{entity.Id}'.");

                    foreach (var result in reaction.Results)
                    {
                        ExecuteResult(entity, result);
                    }

                    if (reaction.ConsumesStatuses)
                    {
                        foreach (var key in reaction.RequiredStatusKeys)
                            statusesToRemove.Add(key);
                    }

                    OnReactionTriggered?.Invoke(entity, reaction);
                }

                // Remove consumed statuses after all reactions in this pass
                foreach (var statusKey in statusesToRemove)
                {
                    _statusEngine.RemoveStatus(entity, statusKey);
                }

                // Check if new statuses were added (for chaining)
                var currentStatuses = new HashSet<string>(entity.ActiveStatusKeys);
                currentStatuses.ExceptWith(previousStatuses);

                if (currentStatuses.Count == 0)
                    break; // No new statuses — no further chaining possible

                depth++;
            }

            if (depth >= MaxChainDepth)
            {
                _logger.Warning($"Reaction chain depth limit ({MaxChainDepth}) reached on entity '{entity.Id}'.");
            }
        }

        private List<IReaction> FindMatchingReactions(IEffectioEntity entity)
        {
            var matches = new List<IReaction>();

            foreach (var reaction in _reactions)
            {
                if (IsReactionSatisfied(entity, reaction))
                    matches.Add(reaction);
            }

            return matches;
        }

        private bool IsReactionSatisfied(IEffectioEntity entity, IReaction reaction)
        {
            // Check by status keys
            if (reaction.RequiredStatusKeys.Length > 0)
            {
                bool allPresent = true;
                foreach (var key in reaction.RequiredStatusKeys)
                {
                    if (!entity.HasStatus(key))
                    {
                        allPresent = false;
                        break;
                    }
                }
                if (allPresent) return true;
            }

            // Check by tags
            if (reaction.RequiredTags.Length > 0)
            {
                return AreTagsSatisfied(entity, reaction.RequiredTags);
            }

            return false;
        }

        private bool AreTagsSatisfied(IEffectioEntity entity, string[] requiredTags)
        {
            // Each required tag must be present on at least one active status
            var entityTags = new HashSet<string>();
            foreach (var statusKey in entity.ActiveStatusKeys)
            {
                var definition = _statusEngine.GetStatusDefinition(statusKey);
                if (definition?.Tags != null)
                {
                    foreach (var tag in definition.Tags)
                        entityTags.Add(tag);
                }
            }

            foreach (var tag in requiredTags)
            {
                if (!entityTags.Contains(tag))
                    return false;
            }
            return true;
        }

        private void ExecuteResult(IEffectioEntity entity, IReactionResult result)
        {
            switch (result.Type)
            {
                case ReactionResultType.ApplyStatus:
                    _statusEngine.ApplyStatus(entity, result.TargetKey);
                    break;

                case ReactionResultType.RemoveStatus:
                    _statusEngine.RemoveStatus(entity, result.TargetKey);
                    break;

                case ReactionResultType.AdjustStat:
                    OnAdjustStat?.Invoke(entity, result.TargetKey, result.Value);
                    break;

                case ReactionResultType.ApplyEffect:
                    OnApplyEffect?.Invoke(entity, result.TargetKey);
                    break;

                case ReactionResultType.Custom:
                    // Custom results handled via OnReactionTriggered event — game code inspects the reaction
                    break;
            }
        }
    }
}
