using System;
using System.Collections.Generic;
using System.Linq;
using Effectio.Common.Logging;
using Effectio.Entities;
using Effectio.Modifiers;
using Effectio.Statuses;

namespace Effectio.Effects
{
    public class EffectsEngine : IEffectsEngine
    {
        private readonly IEffectioLogger _logger;
        private readonly IStatusEngine _statusEngine;

        // Tracks active non-instant effects per entity
        private readonly Dictionary<string, List<ActiveEffect>> _activeEffects =
            new Dictionary<string, List<ActiveEffect>>();

        public event Action<IEffectioEntity, IEffect> OnEffectApplied;
        public event Action<IEffectioEntity, IEffect> OnEffectRemoved;
        public event Action<IEffectioEntity, IEffect> OnEffectTick;

        public EffectsEngine(IStatusEngine statusEngine, IEffectioLogger logger = null)
        {
            _statusEngine = statusEngine;
            _logger = logger ?? VoidLogger.Instance;
        }

        public void ApplyEffect(IEffectioEntity entity, IEffect effect)
        {
            if (effect.EffectType == EffectType.Instant)
            {
                ExecuteAction(entity, effect);
                OnEffectApplied?.Invoke(entity, effect);
                return;
            }

            // Timed, Periodic, Aura, Triggered — track as active
            if (!_activeEffects.TryGetValue(entity.Id, out var effects))
            {
                effects = new List<ActiveEffect>();
                _activeEffects[entity.Id] = effects;
            }

            var active = new ActiveEffect(effect);
            effects.Add(active);

            // For non-periodic effects, apply immediately on attach
            if (effect.EffectType != EffectType.Periodic)
            {
                ExecuteAction(entity, effect);
            }

            OnEffectApplied?.Invoke(entity, effect);
            _logger.Info($"Effect '{effect.Key}' applied to entity '{entity.Id}'.");
        }

        public void RemoveEffect(IEffectioEntity entity, string effectKey)
        {
            if (!_activeEffects.TryGetValue(entity.Id, out var effects))
                return;

            var removed = effects.RemoveAll(e => e.Effect.Key == effectKey);
            if (removed > 0)
            {
                OnEffectRemoved?.Invoke(entity, null);
                _logger.Info($"Effect '{effectKey}' removed from entity '{entity.Id}'.");
            }
        }

        public void Tick(float deltaTime)
        {
            var entitiesToClean = new List<string>();

            foreach (var kvp in _activeEffects)
            {
                var entityId = kvp.Key;
                var effects = kvp.Value;
                var toRemove = new List<ActiveEffect>();

                foreach (var active in effects)
                {
                    var effect = active.Effect;

                    // Decrement duration for timed/periodic effects
                    if (effect.Duration >= 0)
                    {
                        active.RemainingDuration -= deltaTime;
                        if (active.RemainingDuration <= 0)
                        {
                            toRemove.Add(active);
                            continue;
                        }
                    }

                    // Process periodic tick
                    if (effect.EffectType == EffectType.Periodic && effect.TickInterval > 0)
                    {
                        active.TimeSinceLastTick += deltaTime;
                        if (active.TimeSinceLastTick >= effect.TickInterval)
                        {
                            active.TimeSinceLastTick -= effect.TickInterval;
                            // We need the entity to execute action — store entityId for lookup
                            active.PendingTick = true;
                        }
                    }
                }

                foreach (var removed in toRemove)
                {
                    effects.Remove(removed);
                    OnEffectRemoved?.Invoke(null, removed.Effect);
                }

                if (effects.Count == 0)
                    entitiesToClean.Add(entityId);
            }

            foreach (var id in entitiesToClean)
                _activeEffects.Remove(id);
        }

        /// <summary>
        /// Process pending periodic ticks for a specific entity. Called by EffectioManager which has entity references.
        /// </summary>
        internal void ProcessPendingTicks(IEffectioEntity entity)
        {
            if (!_activeEffects.TryGetValue(entity.Id, out var effects))
                return;

            foreach (var active in effects)
            {
                if (active.PendingTick)
                {
                    active.PendingTick = false;
                    ExecuteAction(entity, active.Effect);
                    OnEffectTick?.Invoke(entity, active.Effect);
                }
            }
        }

        private void ExecuteAction(IEffectioEntity entity, IEffect effect)
        {
            switch (effect.ActionType)
            {
                case EffectActionType.AdjustStat:
                    if (entity.HasStat(effect.TargetKey))
                    {
                        var stat = entity.GetStat(effect.TargetKey);
                        stat.BaseValue += effect.Value;
                        stat.Recalculate();
                    }
                    break;

                case EffectActionType.ApplyModifier:
                    if (entity.HasStat(effect.TargetKey))
                    {
                        var stat = entity.GetStat(effect.TargetKey);
                        var modifier = new Modifier(
                            effect.Key + "_mod",
                            ModifierType.Additive,
                            effect.Value,
                            effect.Duration,
                            effect.Key
                        );
                        stat.AddModifier(modifier);
                    }
                    break;

                case EffectActionType.RemoveModifier:
                    if (entity.HasStat(effect.TargetKey))
                    {
                        var stat = entity.GetStat(effect.TargetKey);
                        stat.RemoveModifier(effect.TargetKey);
                    }
                    break;

                case EffectActionType.ApplyStatus:
                    _statusEngine.ApplyStatus(entity, effect.TargetKey);
                    break;

                case EffectActionType.RemoveStatus:
                    _statusEngine.RemoveStatus(entity, effect.TargetKey);
                    break;

                case EffectActionType.Custom:
                    // Custom actions are handled via events — game code listens to OnEffectApplied/OnEffectTick
                    break;
            }
        }

        internal bool HasActiveEffects(string entityId)
        {
            return _activeEffects.ContainsKey(entityId) && _activeEffects[entityId].Count > 0;
        }

        internal void RemoveAllEffects(string entityId)
        {
            _activeEffects.Remove(entityId);
        }

        private class ActiveEffect
        {
            public IEffect Effect { get; }
            public float RemainingDuration { get; set; }
            public float TimeSinceLastTick { get; set; }
            public bool PendingTick { get; set; }

            public ActiveEffect(IEffect effect)
            {
                Effect = effect;
                RemainingDuration = effect.Duration;
                TimeSinceLastTick = 0f;
                PendingTick = false;
            }
        }
    }
}
