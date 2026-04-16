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

            // Aura: apply immediately (will be undone on removal)
            // Timed: apply immediately
            // Periodic: wait for first tick
            // Triggered: wait for condition
            if (effect.EffectType == EffectType.Aura || effect.EffectType == EffectType.Timed)
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

            for (int i = effects.Count - 1; i >= 0; i--)
            {
                if (effects[i].Effect.Key == effectKey)
                {
                    var effect = effects[i].Effect;

                    // Aura effects undo their action on removal
                    if (effect.EffectType == EffectType.Aura)
                        UndoAction(entity, effect);

                    effects.RemoveAt(i);
                    OnEffectRemoved?.Invoke(entity, effect);
                    _logger.Info($"Effect '{effectKey}' removed from entity '{entity.Id}'.");
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
                        UndoAction(entity, active.Effect);

                    effects.RemoveAt(i);
                    OnEffectRemoved?.Invoke(entity, active.Effect);
                    _logger.Info($"Effect '{active.Effect.Key}' expired on entity '{entity.Id}'.");
                    continue;
                }

                // Handle periodic ticks.
                if (active.PendingTick)
                {
                    active.PendingTick = false;
                    ExecuteAction(entity, active.Effect);
                    OnEffectTick?.Invoke(entity, active.Effect);
                }

                // Handle triggered effects — check condition each tick.
                if (active.Effect.EffectType == EffectType.Triggered && !active.HasTriggered)
                {
                    if (CheckTriggerCondition(entity, active.Effect))
                    {
                        active.HasTriggered = true;
                        ExecuteAction(entity, active.Effect);
                        OnEffectTick?.Invoke(entity, active.Effect);
                    }
                }
            }

            if (effects.Count == 0)
                _activeEffects.Remove(entity.Id);
        }

        private bool CheckTriggerCondition(IEffectioEntity entity, IEffect effect)
        {
            switch (effect.TriggerCondition)
            {
                case TriggerConditionType.StatBelow:
                    if (entity.TryGetStat(effect.TriggerKey, out var statBelow))
                        return statBelow.CurrentValue < effect.TriggerThreshold;
                    return false;

                case TriggerConditionType.StatAbove:
                    if (entity.TryGetStat(effect.TriggerKey, out var statAbove))
                        return statAbove.CurrentValue > effect.TriggerThreshold;
                    return false;

                case TriggerConditionType.HasStatus:
                    return entity.HasStatus(effect.TriggerKey);

                case TriggerConditionType.LacksStatus:
                    return !entity.HasStatus(effect.TriggerKey);

                default:
                    return false;
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

        private void UndoAction(IEffectioEntity entity, IEffect effect)
        {
            switch (effect.ActionType)
            {
                case EffectActionType.AdjustStat:
                    if (entity.HasStat(effect.TargetKey))
                    {
                        var stat = entity.GetStat(effect.TargetKey);
                        stat.BaseValue -= effect.Value;
                        stat.Recalculate();
                    }
                    break;

                case EffectActionType.ApplyModifier:
                    if (entity.HasStat(effect.TargetKey))
                    {
                        var stat = entity.GetStat(effect.TargetKey);
                        stat.RemoveModifiersFromSource(effect.Key);
                    }
                    break;

                case EffectActionType.ApplyStatus:
                    _statusEngine.RemoveStatus(entity, effect.TargetKey);
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
