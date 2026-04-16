using Effectio.Effects;
using Effectio.Effects.Actions;
using Effectio.Effects.Triggers;
using Effectio.Modifiers;
using System;

namespace Effectio.Builders
{
    /// <summary>
    /// Fluent builder for <see cref="IEffect"/>. Defaults produce an instant stat-adjusting effect.
    /// </summary>
    public class EffectBuilder
    {
        private readonly string _key;
        private EffectType _effectType = EffectType.Instant;
        private EffectActionType _actionType = EffectActionType.AdjustStat;
        private string _targetKey;
        private float _value;
        private float _duration = -1f;
        private float _tickInterval;
        private string _customActionKey;
        private TriggerConditionType _triggerCondition = TriggerConditionType.None;
        private string _triggerKey;
        private float _triggerThreshold;
        private IEffectAction _customAction;
        private ITriggerCondition _customTrigger;

        public EffectBuilder(string key)
        {
            _key = key;
        }

        public static EffectBuilder Create(string key) => new EffectBuilder(key);

        // Effect type shortcuts
        public EffectBuilder Instant() { _effectType = EffectType.Instant; return this; }
        public EffectBuilder Timed(float duration) { _effectType = EffectType.Timed; _duration = duration; return this; }
        public EffectBuilder Periodic(float duration, float tickInterval)
        {
            _effectType = EffectType.Periodic;
            _duration = duration;
            _tickInterval = tickInterval;
            return this;
        }
        public EffectBuilder Aura(float duration = -1f) { _effectType = EffectType.Aura; _duration = duration; return this; }
        public EffectBuilder Triggered(float duration = -1f) { _effectType = EffectType.Triggered; _duration = duration; return this; }

        // Action shortcuts
        public EffectBuilder AdjustStat(string statKey, float value)
        {
            _actionType = EffectActionType.AdjustStat;
            _targetKey = statKey;
            _value = value;
            _customAction = null;
            return this;
        }

        public EffectBuilder ApplyModifier(string statKey, float value)
        {
            _actionType = EffectActionType.ApplyModifier;
            _targetKey = statKey;
            _value = value;
            _customAction = null;
            return this;
        }

        /// <summary>
        /// Apply any <see cref="IModifier"/> kind via a factory invoked per execution.
        /// Use this to attach multiplicative, cap-adjustment, or custom modifiers.
        /// </summary>
        public EffectBuilder ApplyModifier(string statKey, Func<IEffect, IModifier> modifierFactory)
        {
            _customAction = new ApplyModifierAction(statKey, modifierFactory);
            _actionType = EffectActionType.Custom;
            return this;
        }

        public EffectBuilder RemoveModifier(string modifierKey)
        {
            _actionType = EffectActionType.RemoveModifier;
            _targetKey = modifierKey;
            _customAction = null;
            return this;
        }

        public EffectBuilder ApplyStatus(string statusKey)
        {
            _actionType = EffectActionType.ApplyStatus;
            _targetKey = statusKey;
            _customAction = null;
            return this;
        }

        public EffectBuilder RemoveStatus(string statusKey)
        {
            _actionType = EffectActionType.RemoveStatus;
            _targetKey = statusKey;
            _customAction = null;
            return this;
        }

        public EffectBuilder Custom(string customActionKey)
        {
            _actionType = EffectActionType.Custom;
            _customActionKey = customActionKey;
            _customAction = null;
            return this;
        }

        /// <summary>
        /// Use a user-supplied <see cref="IEffectAction"/> - bypasses the built-in action kinds
        /// and is the preferred way to plug in custom gameplay behaviour.
        /// </summary>
        public EffectBuilder WithAction(IEffectAction action)
        {
            _customAction = action;
            _actionType = EffectActionType.Custom;
            return this;
        }

        // Trigger conditions
        public EffectBuilder WhenStatBelow(string statKey, float threshold)
        {
            _triggerCondition = TriggerConditionType.StatBelow;
            _triggerKey = statKey;
            _triggerThreshold = threshold;
            _customTrigger = null;
            return this;
        }

        public EffectBuilder WhenStatAbove(string statKey, float threshold)
        {
            _triggerCondition = TriggerConditionType.StatAbove;
            _triggerKey = statKey;
            _triggerThreshold = threshold;
            _customTrigger = null;
            return this;
        }

        public EffectBuilder WhenHasStatus(string statusKey)
        {
            _triggerCondition = TriggerConditionType.HasStatus;
            _triggerKey = statusKey;
            _customTrigger = null;
            return this;
        }

        public EffectBuilder WhenLacksStatus(string statusKey)
        {
            _triggerCondition = TriggerConditionType.LacksStatus;
            _triggerKey = statusKey;
            _customTrigger = null;
            return this;
        }

        /// <summary>Use a user-supplied <see cref="ITriggerCondition"/> (e.g. <c>AndTrigger</c>, custom predicate).</summary>
        public EffectBuilder When(ITriggerCondition trigger)
        {
            _customTrigger = trigger;
            return this;
        }

        public IEffect Build()
        {
            if (_customAction != null)
            {
                return new Effect(
                    _key,
                    _effectType,
                    _customAction,
                    _duration,
                    _tickInterval,
                    _triggerCondition,
                    _triggerKey,
                    _triggerThreshold,
                    _customTrigger);
            }

            return new Effect(
                _key,
                _effectType,
                _actionType,
                _targetKey,
                _value,
                _duration,
                _tickInterval,
                _customActionKey,
                _triggerCondition,
                _triggerKey,
                _triggerThreshold,
                _customTrigger);
        }
    }
}
