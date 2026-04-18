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
        private readonly HashSet<string> _statusSnapshotBuffer = new HashSet<string>();
        // v1.1: queue of per-key stack decrements for the current tier.
        // Drained alongside _toRemoveBuffer post-firing-loop so the next-lower tier
        // sees consistent post-consume state.
        private readonly List<StackConsume> _stackConsumeBuffer = new List<StackConsume>();

        public void RegisterReaction(IReaction reaction)
        {
            // Insert at the end, then bubble up while strictly higher priority than the
            // left neighbour. Strict (>, not >=) inequality keeps the sort *stable* for
            // ties, preserving registration order among equal-priority reactions - which
            // is the v1.0-equivalent "fire simultaneously" behaviour callers rely on.
            // Reactions that do not implement IPrioritizedReaction are treated as
            // priority 0 (identical to v1.0 semantics).
            _reactions.Add(reaction);
            int newPrio = GetPriority(reaction);
            for (int i = _reactions.Count - 1; i > 0; i--)
            {
                if (newPrio > GetPriority(_reactions[i - 1]))
                {
                    var swap = _reactions[i];
                    _reactions[i] = _reactions[i - 1];
                    _reactions[i - 1] = swap;
                }
                else break;
            }
            _logger.Info($"Reaction '{reaction.Key}' registered.");
        }

        public void RemoveReaction(string reactionKey)
        {
            // RemoveAll preserves the relative order of remaining elements, so the sort
            // invariant is maintained without re-sorting.
            _reactions.RemoveAll(r => r.Key == reactionKey);
        }

        public void CheckReactions(IEffectioEntity entity)
        {
            int depth = 0;

            while (depth < MaxChainDepth)
            {
                // Snapshot current statuses (pooled buffer) so we can detect new ones after the pass.
                _prevStatusBuffer.Clear();
                entity.CopyStatusKeysTo(_prevStatusBuffer);

                bool anyTriggeredThisPass = ProcessTiers(entity);

                if (!anyTriggeredThisPass)
                    break;

                // Detect new statuses for chain detection across passes.
                _newStatusBuffer.Clear();
                entity.CopyStatusKeysTo(_newStatusBuffer);
                _newStatusBuffer.ExceptWith(_prevStatusBuffer);

                if (_newStatusBuffer.Count == 0)
                    break;

                depth++;
            }

            if (depth >= MaxChainDepth)
            {
                if (_logger.IsEnabled) _logger.Warning($"Reaction chain depth limit ({MaxChainDepth}) reached on entity '{entity.Id}'.");
            }
        }

        /// <summary>
        /// Walks the priority-sorted <c>_reactions</c> list ONCE, grouping consecutive
        /// equal-priority entries into tiers. Within a tier all matching reactions fire
        /// simultaneously (against the same pre-consume state); between tiers, consumed
        /// statuses are removed so the next-lower tier re-evaluates against the
        /// post-consume state. Total work is O(R) per pass regardless of how many
        /// distinct priorities are in use.
        /// </summary>
        private bool ProcessTiers(IEffectioEntity entity)
        {
            bool anyFired = false;
            int i = 0;
            while (i < _reactions.Count)
            {
                int tier = GetPriority(_reactions[i]);

                _matchBuffer.Clear();
                _toRemoveBuffer.Clear();
                _stackConsumeBuffer.Clear();

                // Collect satisfied reactions for this tier.
                int j = i;
                while (j < _reactions.Count && GetPriority(_reactions[j]) == tier)
                {
                    if (IsReactionSatisfied(entity, _reactions[j]))
                        _matchBuffer.Add(_reactions[j]);
                    j++;
                }

                // Execute all matched reactions in this tier (simultaneously - same
                // pre-consume view of the entity's statuses).
                for (int k = 0; k < _matchBuffer.Count; k++)
                {
                    var reaction = _matchBuffer[k];
                    if (_logger.IsEnabled) _logger.Info($"Reaction '{reaction.Key}' triggered on entity '{entity.Id}'.");

                    var results = reaction.Results;
                    for (int r = 0; r < results.Length; r++)
                        ExecuteResult(entity, results[r]);

                    // Queue per-key stack-decrement consumes (v1.1+, IStackAwareReaction only).
                    StackConsume[] stackConsumes = null;
                    if (reaction is IStackAwareReaction stackAware && stackAware.StackConsumes.Length > 0)
                    {
                        stackConsumes = stackAware.StackConsumes;
                        for (int sc = 0; sc < stackConsumes.Length; sc++)
                            _stackConsumeBuffer.Add(stackConsumes[sc]);
                    }

                    // Queue whole-status consumes (v1.0 ConsumesStatuses flag), EXCLUDING any
                    // keys that the reaction's StackConsumes already targets - per-key
                    // stack consume wins for those keys.
                    if (reaction.ConsumesStatuses)
                    {
                        var keys = reaction.RequiredStatusKeys;
                        for (int k2 = 0; k2 < keys.Length; k2++)
                        {
                            if (stackConsumes != null && KeyHasStackConsume(stackConsumes, keys[k2]))
                                continue;
                            _toRemoveBuffer.Add(keys[k2]);
                        }
                    }

                    OnReactionTriggered?.Invoke(entity, reaction);
                }

                // Apply stack decrements for this tier (only if the status engine
                // supports IStackOperations - external implementations that don't
                // are warn-skipped, so stack-aware reactions degrade to "consume nothing").
                if (_stackConsumeBuffer.Count > 0)
                {
                    if (_statusEngine is IStackOperations ops)
                    {
                        for (int s = 0; s < _stackConsumeBuffer.Count; s++)
                        {
                            var sc = _stackConsumeBuffer[s];
                            ops.RemoveStacks(entity, sc.StatusKey, sc.Count);
                        }
                    }
                    else
                    {
                        if (_logger.IsEnabled) _logger.Warning($"Stack-aware reaction tried to ConsumesStacks but the status engine does not implement IStackOperations.");
                    }
                }

                // Apply whole-status consumes for this tier so the next-lower tier sees post-consume state.
                foreach (var statusKey in _toRemoveBuffer)
                    _statusEngine.RemoveStatus(entity, statusKey);

                if (_matchBuffer.Count > 0) anyFired = true;
                i = j;
            }
            return anyFired;
        }

        /// <summary>
        /// Linear-scan check: does <paramref name="consumes"/> contain an entry
        /// targeting <paramref name="statusKey"/>? Used to suppress whole-status
        /// removal when a per-key stack consume already covers the same key.
        /// Both arrays are typically very small (1-3 entries), so O(N) is fine.
        /// </summary>
        private static bool KeyHasStackConsume(StackConsume[] consumes, string statusKey)
        {
            for (int i = 0; i < consumes.Length; i++)
            {
                if (consumes[i].StatusKey == statusKey) return true;
            }
            return false;
        }

        private bool IsReactionSatisfied(IEffectioEntity entity, IReaction reaction)
        {
            // Pull stack requirements if reaction opted into IStackAwareReaction.
            // Reactions that don't implement it have no stack constraints (v1.0 behaviour).
            StackRequirement[] stackReqs = (reaction is IStackAwareReaction sar)
                ? sar.RequiredStacks
                : null;
            bool hasStackReqs = stackReqs != null && stackReqs.Length > 0;

            // Status keys + stacks are an AND-group (all must be satisfied for this branch
            // to succeed). RequireStacks(key, N) implies presence of `key`, so a caller
            // does not also need RequireStatus(key) - the stack check is sufficient.
            bool hasStatusKeys = reaction.RequiredStatusKeys.Length > 0;
            if (hasStatusKeys || hasStackReqs)
            {
                bool allMatch = true;
                if (hasStatusKeys)
                {
                    var keys = reaction.RequiredStatusKeys;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        if (!entity.HasStatus(keys[i])) { allMatch = false; break; }
                    }
                }
                if (allMatch && hasStackReqs)
                {
                    for (int i = 0; i < stackReqs.Length; i++)
                    {
                        if (_statusEngine.GetStacks(entity, stackReqs[i].StatusKey) < stackReqs[i].MinStacks)
                        {
                            allMatch = false;
                            break;
                        }
                    }
                }
                if (allMatch) return true;
            }

            // Tag fallback (v1.0 OR-alternative when status/stack group is unsatisfied).
            if (reaction.RequiredTags.Length > 0)
            {
                return AreTagsSatisfied(entity, reaction.RequiredTags);
            }

            return false;
        }

        private bool AreTagsSatisfied(IEffectioEntity entity, string[] requiredTags)
        {
            // Each required tag must be present on at least one active status.
            // Uses pooled buffers throughout — zero allocation per call.
            _statusSnapshotBuffer.Clear();
            entity.CopyStatusKeysTo(_statusSnapshotBuffer);

            _entityTagsBuffer.Clear();
            foreach (var statusKey in _statusSnapshotBuffer) // HashSet struct enumerator, no boxing
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

        /// <summary>
        /// Reads a reaction's priority tier without forcing it to implement
        /// <see cref="IPrioritizedReaction"/>. Reactions that do not opt in are
        /// treated as priority 0 (identical to v1.0 behaviour). The pattern match
        /// JITs to a single typecheck; per-call cost is negligible.
        /// </summary>
        private static int GetPriority(IReaction reaction)
            => reaction is IPrioritizedReaction p ? p.Priority : 0;

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
