using System;
using Effectio.Effects.Actions;
using Effectio.Effects.Triggers;

namespace Effectio.Effects
{
    public class Effect : IEffect
    {
        public string Key { get; }
        public EffectType EffectType { get; }
        public EffectActionType ActionType { get; }
        public string TargetKey { get; }
        public float Value { get; }
        public float Duration { get; }
        public float TickInterval { get; }
        public string CustomActionKey { get; }
        public TriggerConditionType TriggerCondition { get; }
        public string TriggerKey { get; }
        public float TriggerThreshold { get; }
        public IEffectAction Action { get; }
        public ITriggerCondition Trigger { get; }

        /// <summary>
        /// Legacy-shape constructor. The engine dispatches through <see cref="Action"/>;
        /// the <paramref name="actionType"/>/<paramref name="targetKey"/>/<paramref name="value"/>/<paramref name="customActionKey"/>
        /// parameters are preserved as metadata and used to construct the matching built-in <see cref="IEffectAction"/>.
        /// </summary>
        public Effect(
            string key,
            EffectType effectType,
            EffectActionType actionType,
            string targetKey,
            float value = 0f,
            float duration = -1f,
            float tickInterval = 0f,
            string customActionKey = null,
            TriggerConditionType triggerCondition = TriggerConditionType.None,
            string triggerKey = null,
            float triggerThreshold = 0f,
            ITriggerCondition trigger = null)
        {
            Key = key;
            EffectType = effectType;
            ActionType = actionType;
            TargetKey = targetKey;
            Value = value;
            Duration = duration;
            TickInterval = tickInterval;
            CustomActionKey = customActionKey;
            TriggerCondition = triggerCondition;
            TriggerKey = triggerKey;
            TriggerThreshold = triggerThreshold;
            Action = CreateBuiltInAction(actionType, targetKey, value, customActionKey);
            Trigger = trigger ?? CreateBuiltInTrigger(triggerCondition, triggerKey, triggerThreshold);
        }

        /// <summary>
        /// Constructor for effects driven by a custom <see cref="IEffectAction"/>.
        /// <see cref="ActionType"/> is set to <see cref="EffectActionType.Custom"/>.
        /// </summary>
        public Effect(
            string key,
            EffectType effectType,
            IEffectAction action,
            float duration = -1f,
            float tickInterval = 0f,
            TriggerConditionType triggerCondition = TriggerConditionType.None,
            string triggerKey = null,
            float triggerThreshold = 0f,
            ITriggerCondition trigger = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            Key = key;
            EffectType = effectType;
            ActionType = EffectActionType.Custom;
            TargetKey = null;
            Value = 0f;
            Duration = duration;
            TickInterval = tickInterval;
            CustomActionKey = (action as CustomAction)?.CustomActionKey;
            TriggerCondition = triggerCondition;
            TriggerKey = triggerKey;
            TriggerThreshold = triggerThreshold;
            Action = action;
            Trigger = trigger ?? CreateBuiltInTrigger(triggerCondition, triggerKey, triggerThreshold);
        }

        private static IEffectAction CreateBuiltInAction(
            EffectActionType actionType, string targetKey, float value, string customActionKey)
        {
            switch (actionType)
            {
                case EffectActionType.AdjustStat:     return new AdjustStatAction(targetKey, value);
                case EffectActionType.ApplyModifier:  return new ApplyModifierAction(targetKey, value);
                case EffectActionType.RemoveModifier: return new RemoveModifierAction(targetKey);
                case EffectActionType.ApplyStatus:    return new ApplyStatusAction(targetKey);
                case EffectActionType.RemoveStatus:   return new RemoveStatusAction(targetKey);
                case EffectActionType.Custom:         return new CustomAction(customActionKey);
                default:                              return new CustomAction(customActionKey);
            }
        }

        private static ITriggerCondition CreateBuiltInTrigger(
            TriggerConditionType kind, string triggerKey, float threshold)
        {
            switch (kind)
            {
                case TriggerConditionType.StatBelow:    return new StatBelowTrigger(triggerKey, threshold);
                case TriggerConditionType.StatAbove:    return new StatAboveTrigger(triggerKey, threshold);
                case TriggerConditionType.HasStatus:    return new HasStatusTrigger(triggerKey);
                case TriggerConditionType.LacksStatus:  return new LacksStatusTrigger(triggerKey);
                case TriggerConditionType.None:
                default:                                return NeverTrigger.Instance;
            }
        }
    }
}

