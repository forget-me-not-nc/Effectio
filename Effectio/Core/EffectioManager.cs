using System;
using System.Collections.Generic;
using Effectio.Common.Logging;
using Effectio.Effects;
using Effectio.Entities;
using Effectio.Reactions;
using Effectio.Statuses;

namespace Effectio.Core
{
    public class EffectioManager : IEffectioManager
    {
        private readonly IEffectioLogger _logger;
        private readonly Dictionary<string, IEffectioEntity> _entities = new Dictionary<string, IEffectioEntity>();

        private readonly StatusEngine _statusEngine;
        private readonly EffectsEngine _effectsEngine;
        private readonly ReactionEngine _reactionEngine;
        private bool _isCheckingReactions;

        public IEffectsEngine Effects => _effectsEngine;
        public IStatusEngine Statuses => _statusEngine;
        public IReactionEngine Reactions => _reactionEngine;

        /// <summary>
        /// Registry of <see cref="IEffect"/> definitions resolvable by key. Reactions
        /// using <c>ReactionBuilder.ApplyEffect(string)</c> resolve through this catalog
        /// at trigger time. Added in v1.1; the underlying engine implements both
        /// <see cref="IEffectsEngine"/> and <see cref="IEffectCatalog"/>.
        /// </summary>
        public IEffectCatalog EffectCatalog => _effectsEngine;

        public EffectioManager(IEffectioLogger logger = null)
        {
            _logger = logger ?? VoidLogger.Instance;

            _statusEngine = new StatusEngine(_logger);
            _effectsEngine = new EffectsEngine(_statusEngine, _logger);
            _reactionEngine = new ReactionEngine(_statusEngine, _logger);

            // Wire up: when a status is applied, check reactions
            _statusEngine.OnStatusApplied += OnStatusApplied;

            // v1.1: and when one is taken off. IStatus.OnRemoveEffects has always been
            // documented as firing on "manual remove or expiration" and only ever fired on
            // expiration, because nothing was listening here. A status dispelled rather than
            // waited out silently skipped its own farewell - so anything an author put there
            // to clean up after itself simply did not happen.
            _statusEngine.OnStatusRemoved += OnStatusRemoved;

            // v1.1: stack count changes also trigger reaction checks so
            // IStackAwareReaction.RequiredStacks thresholds re-evaluate as
            // stacks accumulate. Does NOT replay OnApplyEffects (those fire
            // once on status birth, not on stack changes).
            _statusEngine.OnStatusStacked += OnStatusStacked;

            // v1.1: status refreshes (re-application against existing status,
            // whether stacks incremented or were already at MaxStacks) fire
            // OnRefreshEffects from the status definition.
            _statusEngine.OnStatusRefreshed += OnStatusRefreshed;

            // Wire up reaction engine callbacks for stat adjustments and effect application
            _reactionEngine.OnAdjustStat = (entity, statKey, value) =>
            {
                if (entity.HasStat(statKey))
                {
                    var stat = entity.GetStat(statKey);
                    stat.BaseValue += value;
                    stat.Recalculate();
                }
            };

            // v1.1: resolve ApplyEffect(string) results through the effect catalog.
            // Pre-v1.1 this delegate was unwired, silently dropping reaction-applied effects.
            _reactionEngine.OnApplyEffect = (entity, effectKey) =>
            {
                if (_effectsEngine.TryGetEffect(effectKey, out var effect))
                {
                    _effectsEngine.ApplyEffect(entity, effect);
                }
                else
                {
                    if (_logger.IsEnabled)
                        _logger.Warning($"Reaction tried to apply unknown effect '{effectKey}'. Register it via EffectCatalog.RegisterEffect before the reaction fires.");
                }
            };
        }

        public IEffectioEntity CreateEntity(string id)
        {
            if (_entities.ContainsKey(id))
                throw new InvalidOperationException($"Entity '{id}' already exists.");

            // v1.1: pass the status engine so entity.GetStatusStackCount(...) works
            // without callers having to reach back through manager.Statuses.
            var entity = new EffectioEntity(id, _statusEngine);
            _entities[id] = entity;
            _logger.Info($"Entity '{id}' created.");
            return entity;
        }

        public IEffectioEntity GetEntity(string id)
        {
            if (_entities.TryGetValue(id, out var entity))
                return entity;

            throw new KeyNotFoundException($"Entity '{id}' not found.");
        }

        public bool TryGetEntity(string id, out IEffectioEntity entity)
        {
            return _entities.TryGetValue(id, out entity);
        }

        public void RemoveEntity(string id)
        {
            if (_entities.Remove(id))
            {
                _effectsEngine.RemoveAllEffects(id);
                _statusEngine.CleanupEntity(id);
                _logger.Info($"Entity '{id}' removed.");
            }
        }

        public void Tick(float deltaTime)
        {
            // 1. Tick effects (decrement durations, mark periodic ticks)
            _effectsEngine.Tick(deltaTime);

            // 2. Tick statuses (decrement durations, mark tick effects)
            _statusEngine.Tick(deltaTime);

            // 3. Process pending operations that require entity references
            foreach (var kvp in _entities)
            {
                var entity = kvp.Value;

                // Tick modifiers on all stats (no enumerator boxing; uses the entity's struct iterator)
                entity.TickStatModifiers(deltaTime);

                // Process pending effect ticks
                _effectsEngine.ProcessPendingTicks(entity);

                // Process pending status tick effects
                var pendingStatusTicks = _statusEngine.GetPendingTicks(entity.Id);
                foreach (var statusKey in pendingStatusTicks)
                {
                    var definition = _statusEngine.GetStatusDefinition(statusKey);
                    if (definition?.OnTickEffects != null)
                    {
                        // Repetition rather than multiplication. An effect's action is
                        // arbitrary - a number can be scaled, "apply that status" cannot -
                        // so the only meaning available in general is that a status with
                        // three stacks ticks as if it had ticked three times at once.
                        var times = definition.TickScalesWithStacks
                            ? _statusEngine.GetStacks(entity, statusKey)
                            : 1;

                        for (var repeat = 0; repeat < times; repeat++)
                        {
                            foreach (var effect in definition.OnTickEffects)
                            {
                                _effectsEngine.ApplyEffect(entity, effect, statusKey);
                            }
                        }
                    }
                }
            }

            // 4a. Announce stacks lost to decay. Before expirations, so a listener sees the
            //     counter fall one at a time and only then sees the status go.
            for (int i = 0; i < _statusEngine.PendingDecays.Count; i++)
            {
                var (entityId, statusKey) = _statusEngine.PendingDecays[i];
                if (_entities.TryGetValue(entityId, out var decayed))
                    _statusEngine.RaiseDecayed(decayed, statusKey);
            }

            _statusEngine.PendingDecays.Clear();

            // 4. Process status expirations
            foreach (var (entityId, statusKey) in _statusEngine.PendingExpirations)
            {
                if (_entities.TryGetValue(entityId, out var entity))
                {
                    entity.RemoveStatus(statusKey);

                    // Apply on-remove effects
                    var definition = _statusEngine.GetStatusDefinition(statusKey);
                    if (definition?.OnRemoveEffects != null)
                    {
                        foreach (var effect in definition.OnRemoveEffects)
                        {
                            _effectsEngine.ApplyEffect(entity, effect, statusKey);
                        }
                    }

                    _statusEngine.RaiseStatusExpired(entity, statusKey);
                }
            }
            _statusEngine.PendingExpirations.Clear();
        }

        /// <summary>
        /// Fires a status's OnRemoveEffects when it is taken off deliberately.
        ///
        /// Expiration does not come through here - the engine raises OnStatusExpired for that
        /// and the tick loop fires the same effects itself - so there is no path on which both
        /// run.
        /// </summary>
        private void OnStatusRemoved(IEffectioEntity entity, string statusKey)
        {
            var definition = _statusEngine.GetStatusDefinition(statusKey);

            if (definition?.OnRemoveEffects == null)
                return;

            foreach (var effect in definition.OnRemoveEffects)
            {
                _effectsEngine.ApplyEffect(entity, effect, statusKey);
            }
        }

        private void OnStatusApplied(IEffectioEntity entity, string statusKey)
        {
            // Apply on-apply effects from the status definition
            var definition = _statusEngine.GetStatusDefinition(statusKey);
            if (definition?.OnApplyEffects != null)
            {
                foreach (var effect in definition.OnApplyEffects)
                {
                    _effectsEngine.ApplyEffect(entity, effect, statusKey);
                }
            }

            // Check for reactions whenever a status is applied
            // Guard against re-entrancy: ReactionEngine handles chaining internally
            CheckReactionsGuarded(entity);
        }

        private void OnStatusStacked(IEffectioEntity entity, string statusKey)
        {
            // Stack increments do NOT replay OnApplyEffects (those are once-per-birth).
            // They DO need a reaction re-check so stack-aware reactions can fire as
            // thresholds get crossed.
            CheckReactionsGuarded(entity);
        }

        private void OnStatusRefreshed(IEffectioEntity entity, string statusKey)
        {
            // Apply the status's OnRefreshEffects (separate list from OnApplyEffects).
            // Mirrors the OnStatusApplied wiring for the once-per-birth OnApplyEffects.
            var definition = _statusEngine.GetStatusDefinition(statusKey);
            if (definition?.OnRefreshEffects != null)
            {
                foreach (var effect in definition.OnRefreshEffects)
                {
                    _effectsEngine.ApplyEffect(entity, effect, statusKey);
                }
            }
            // No reaction re-check here. OnStatusStacked already triggers CheckReactions
            // for stack-counter changes. At-MaxStacks refreshes do not change anything
            // observable to a stack-aware reaction (counter unchanged, no new statuses).
        }

        private void CheckReactionsGuarded(IEffectioEntity entity)
        {
            if (!_isCheckingReactions)
            {
                _isCheckingReactions = true;
                try
                {
                    _reactionEngine.CheckReactions(entity);
                }
                finally
                {
                    _isCheckingReactions = false;
                }
            }
        }
    }
}
