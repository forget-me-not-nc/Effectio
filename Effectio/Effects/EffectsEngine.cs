using System;
using System.Collections.Generic;
using System.Linq;
using Effectio.Common.Logging;
using Effectio.Effects.Actions;
using Effectio.Effects.Triggers;
using Effectio.Common;
using Effectio.Entities;
using Effectio.Statuses;

namespace Effectio.Effects
{
    public class EffectsEngine : IEffectsEngine, IEffectCatalog, IRemainingDuration
    {
        private readonly IEffectioLogger _logger;
        private readonly IStatusEngine _statusEngine;

        // Tracks active non-instant effects per entity
        private readonly Dictionary<string, List<ActiveEffect>> _activeEffects =
            new Dictionary<string, List<ActiveEffect>>();

        // v1.1: catalog of registered effect definitions, looked up by key when a
        // reaction's ApplyEffect(string) result fires.
        private readonly Dictionary<string, IEffect> _catalog = new Dictionary<string, IEffect>();

        public event Action<IEffectioEntity, IEffect> OnEffectApplied;
        public event Action<IEffectioEntity, IEffect> OnEffectRemoved;
        public event Action<IEffectioEntity, IEffect> OnEffectTick;

        public EffectsEngine(IStatusEngine statusEngine, IEffectioLogger logger = null)
        {
            _statusEngine = statusEngine;
            _logger = logger ?? VoidLogger.Instance;
        }

        // -------- IEffectCatalog --------

        public void RegisterEffect(IEffect effect)
        {
            _catalog[effect.Key] = effect;
            if (_logger.IsEnabled) _logger.Info($"Effect '{effect.Key}' registered in catalog.");
        }

        public bool TryGetEffect(string key, out IEffect effect)
        {
            return _catalog.TryGetValue(key, out effect);
        }

        public IReadOnlyCollection<IEffect> RegisteredEffects => _catalog.Values;

        // -------- IEffectsEngine --------

        public void ApplyEffect(IEffectioEntity entity, IEffect effect)
        {
            ApplyEffect(entity, effect, null);
        }

        /// <summary>
        /// Applies an effect on behalf of a status, so its action can find out which status
        /// it is running for through <see cref="Actions.EffectActionContext.SourceStatusKey"/>.
        ///
        /// An overload on the class rather than a member on <see cref="IEffectsEngine"/>: the
        /// caller that needs it is the manager, and growing the interface would break every
        /// external implementation for the sake of a parameter almost nobody passes.
        /// </summary>
        public void ApplyEffect(IEffectioEntity entity, IEffect effect, string sourceStatusKey)
        {
            if (effect.EffectType == EffectType.Instant)
            {
                ExecuteAction(entity, effect, sourceStatusKey);
                OnEffectApplied?.Invoke(entity, effect);
                return;
            }

            // Timed, Periodic, Aura, Triggered — track as active
            if (!_activeEffects.TryGetValue(entity.Id, out var effects))
            {
                effects = new List<ActiveEffect>();
                _activeEffects[entity.Id] = effects;
            }

            // Remembered on the entry rather than passed at execution time, because a
            // periodic effect's ticks happen long after whoever applied it has returned.
            var active = new ActiveEffect(effect) { SourceStatusKey = sourceStatusKey };
            effects.Add(active);

            // Aura: apply immediately (will be undone on removal)
            // Timed: apply immediately
            // Periodic: wait for first tick
            // Triggered: wait for condition
            if (effect.EffectType == EffectType.Aura || effect.EffectType == EffectType.Timed)
            {
                ExecuteAction(entity, effect, sourceStatusKey);
            }

            OnEffectApplied?.Invoke(entity, effect);
            if (_logger.IsEnabled) _logger.Info($"Effect '{effect.Key}' applied to entity '{entity.Id}'.");
        }

        /// <inheritdoc />
        public float GetRemainingDuration(IEffectioEntity entity, string key)
        {
            if (entity == null || !_activeEffects.TryGetValue(entity.Id, out var effects))
                return 0f;

            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].Effect.Key != key)
                    continue;

                // The longest of them, for the same reason a status reports one number: an
                // interface draws one timer per key, and the honest one is when the key stops
                // mattering rather than when its first copy runs out.
                float longest = effects[i].Effect.Duration < 0f ? -1f : effects[i].RemainingDuration;

                for (int j = i + 1; j < effects.Count; j++)
                {
                    if (effects[j].Effect.Key != key)
                        continue;

                    if (effects[j].Effect.Duration < 0f)
                        return -1f;

                    if (longest >= 0f && effects[j].RemainingDuration > longest)
                        longest = effects[j].RemainingDuration;
                }

                return longest;
            }

            return 0f;
        }

        public void RemoveEffect(IEffectioEntity entity, string effectKey)
        {
            if (!_activeEffects.TryGetValue(entity.Id, out var effects))
                return;

            for (int i = effects.Count - 1; i >= 0; i--)
            {
                if (effects[i].Effect.Key == effectKey)
                {
                    var effect = effects[i].Effect;

                    // Aura effects undo their action on removal
                    if (effect.EffectType == EffectType.Aura)
                        UndoAction(entity, effect, effects[i].SourceStatusKey);

                    effects.RemoveAt(i);
                    OnEffectRemoved?.Invoke(entity, effect);
                    if (_logger.IsEnabled) _logger.Info($"Effect '{effectKey}' removed from entity '{entity.Id}'.");
                }
            }
        }

        public void Tick(float deltaTime)
        {
            // Only decrement durations and mark pending work here.
            // Actual action execution and removal is deferred to ProcessPendingTicks
            // where the entity reference is available (needed to undo Aura effects
            // and invoke OnEffectRemoved with a valid entity).
            foreach (var kvp in _activeEffects)
            {
                foreach (var active in kvp.Value)
                {
                    var effect = active.Effect;

                    if (effect.Duration >= 0)
                    {
                        active.RemainingDuration -= deltaTime;
                        if (active.RemainingDuration <= 0)
                        {
                            active.PendingRemoval = true;
                            continue;
                        }
                    }

                    if (effect.EffectType == EffectType.Periodic && effect.TickInterval > 0)
                    {
                        active.TimeSinceLastTick += deltaTime;
                        if (active.TimeSinceLastTick >= effect.TickInterval)
                        {
                            active.TimeSinceLastTick -= effect.TickInterval;
                            active.PendingTick = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Process pending periodic ticks, trigger checks, and removals for a specific entity.
        /// Called by EffectioManager which has entity references.
        /// </summary>
        internal void ProcessPendingTicks(IEffectioEntity entity)
        {
            if (!_activeEffects.TryGetValue(entity.Id, out var effects))
                return;

            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var active = effects[i];

                // Handle expirations (Aura effects must undo their action).
                if (active.PendingRemoval)
                {
                    if (active.Effect.EffectType == EffectType.Aura)
                        UndoAction(entity, active.Effect, active.SourceStatusKey);

                    effects.RemoveAt(i);
                    OnEffectRemoved?.Invoke(entity, active.Effect);
                    if (_logger.IsEnabled) _logger.Info($"Effect '{active.Effect.Key}' expired on entity '{entity.Id}'.");
                    continue;
                }

                // Handle periodic ticks.
                if (active.PendingTick)
                {
                    active.PendingTick = false;
                    ExecuteAction(entity, active.Effect, active.SourceStatusKey);
                    OnEffectTick?.Invoke(entity, active.Effect);
                }

                // Handle triggered effects — check condition each tick.
                if (active.Effect.EffectType == EffectType.Triggered && !active.HasTriggered)
                {
                    if (CheckTriggerCondition(entity, active.Effect))
                    {
                        active.HasTriggered = true;
                        ExecuteAction(entity, active.Effect, active.SourceStatusKey);
                        OnEffectTick?.Invoke(entity, active.Effect);
                    }
                }
            }

            if (effects.Count == 0)
                _activeEffects.Remove(entity.Id);
        }

        private bool CheckTriggerCondition(IEffectioEntity entity, IEffect effect)
        {
            var ctx = new TriggerContext { Entity = entity };
            return effect.Trigger.IsSatisfied(in ctx);
        }

        private void ExecuteAction(IEffectioEntity entity, IEffect effect, string sourceStatusKey = null)
        {
            var ctx = new EffectActionContext
            {
                Entity = entity,
                Effect = effect,
                StatusEngine = _statusEngine,
                SourceStatusKey = sourceStatusKey
            };
            effect.Action.Execute(in ctx);
        }

        private void UndoAction(IEffectioEntity entity, IEffect effect, string sourceStatusKey = null)
        {
            var ctx = new EffectActionContext
            {
                Entity = entity,
                Effect = effect,
                StatusEngine = _statusEngine,
                SourceStatusKey = sourceStatusKey
            };
            effect.Action.Undo(in ctx);
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

            /// <summary>Which status applied this, or null for a direct application.</summary>
            public string SourceStatusKey { get; set; }
            public float TimeSinceLastTick { get; set; }
            public bool PendingTick { get; set; }
            public bool PendingRemoval { get; set; }
            public bool HasTriggered { get; set; }

            public ActiveEffect(IEffect effect)
            {
                Effect = effect;
                RemainingDuration = effect.Duration;
                TimeSinceLastTick = 0f;
                PendingTick = false;
                PendingRemoval = false;
                HasTriggered = false;
            }
        }
    }
}
