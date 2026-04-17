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

        public EffectioManager(IEffectioLogger logger = null)
        {
            _logger = logger ?? VoidLogger.Instance;

            _statusEngine = new StatusEngine(_logger);
            _effectsEngine = new EffectsEngine(_statusEngine, _logger);
            _reactionEngine = new ReactionEngine(_statusEngine, _logger);

            // Wire up: when a status is applied, check reactions
            _statusEngine.OnStatusApplied += OnStatusApplied;

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
        }

        public IEffectioEntity CreateEntity(string id)
        {
            if (_entities.ContainsKey(id))
                throw new InvalidOperationException($"Entity '{id}' already exists.");

            var entity = new EffectioEntity(id);
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
                        foreach (var effect in definition.OnTickEffects)
                        {
                            _effectsEngine.ApplyEffect(entity, effect);
                        }
                    }
                }
            }

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
                            _effectsEngine.ApplyEffect(entity, effect);
                        }
                    }

                    _statusEngine.RaiseStatusExpired(entity, statusKey);
                }
            }
            _statusEngine.PendingExpirations.Clear();
        }

        private void OnStatusApplied(IEffectioEntity entity, string statusKey)
        {
            // Apply on-apply effects from the status definition
            var definition = _statusEngine.GetStatusDefinition(statusKey);
            if (definition?.OnApplyEffects != null)
            {
                foreach (var effect in definition.OnApplyEffects)
                {
                    _effectsEngine.ApplyEffect(entity, effect);
                }
            }

            // Check for reactions whenever a status is applied
            // Guard against re-entrancy: ReactionEngine handles chaining internally
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
