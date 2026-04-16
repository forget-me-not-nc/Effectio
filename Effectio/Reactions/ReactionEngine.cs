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

        // Pooled buffers reused across CheckReactions calls.
        // The engine is single-threaded by design (driven from Tick) and the manager guards
        // against re-entrancy via _isCheckingReactions, so these buffers are safe to reuse.
        private readonly List<IReaction> _matchBuffer = new List<IReaction>();
        private readonly HashSet<string> _toRemoveBuffer = new HashSet<string>();
        private readonly HashSet<string> _prevStatusBuffer = new HashSet<string>();
        private readonly HashSet<string> _newStatusBuffer = new HashSet<string>();
        private readonly HashSet<string> _entityTagsBuffer = new HashSet<string>();

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

                // Snapshot current statuses (pooled buffer) so we can detect new ones after the pass.
                _prevStatusBuffer.Clear();
                foreach (var s in entity.ActiveStatusKeys)
                    _prevStatusBuffer.Add(s);

                _toRemoveBuffer.Clear();

                // Execute ALL matching reactions simultaneously
                for (int i = 0; i < triggered.Count; i++)
                {
                    var reaction = triggered[i];
                    _logger.Info($"Reaction '{reaction.Key}' triggered on entity '{entity.Id}'.");

                    var results = reaction.Results;
                    for (int r = 0; r < results.Length; r++)
                        ExecuteResult(entity, results[r]);

                    if (reaction.ConsumesStatuses)
                    {
                        var keys = reaction.RequiredStatusKeys;
                        for (int k = 0; k < keys.Length; k++)
                            _toRemoveBuffer.Add(keys[k]);
                    }

                    OnReactionTriggered?.Invoke(entity, reaction);
                }

                // Remove consumed statuses after all reactions in this pass
                foreach (var statusKey in _toRemoveBuffer)
                    _statusEngine.RemoveStatus(entity, statusKey);

                // Detect new statuses for chain detection
                _newStatusBuffer.Clear();
                foreach (var s in entity.ActiveStatusKeys)
                    if (!_prevStatusBuffer.Contains(s))
                        _newStatusBuffer.Add(s);

                if (_newStatusBuffer.Count == 0)
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
            _matchBuffer.Clear();
            for (int i = 0; i < _reactions.Count; i++)
            {
                var reaction = _reactions[i];
                if (IsReactionSatisfied(entity, reaction))
                    _matchBuffer.Add(reaction);
            }
            return _matchBuffer;
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
            // Each required tag must be present on at least one active status.
            // Reuses the pooled tag buffer to avoid per-check allocation.
            _entityTagsBuffer.Clear();
            foreach (var statusKey in entity.ActiveStatusKeys)
            {
                var definition = _statusEngine.GetStatusDefinition(statusKey);
                if (definition?.Tags != null)
                {
                    var tags = definition.Tags;
                    for (int i = 0; i < tags.Length; i++)
                        _entityTagsBuffer.Add(tags[i]);
                }
            }

            for (int i = 0; i < requiredTags.Length; i++)
            {
                if (!_entityTagsBuffer.Contains(requiredTags[i]))
                    return false;
            }
            return true;
        }

        private void ExecuteResult(IEffectioEntity entity, IReactionResult result)
        {
            var ctx = new ReactionResultContext
            {
                Entity = entity,
                StatusEngine = _statusEngine,
                AdjustStat = OnAdjustStat,
                ApplyEffect = OnApplyEffect
            };
            result.Execute(in ctx);
        }
    }
}
