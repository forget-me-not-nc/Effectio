using Effectio.Effects.Actions;
using Effectio.Effects.Triggers;

namespace Effectio.Effects
{
    public enum EffectType
    {
        Instant,
        Timed,
        Periodic,
        Aura,
        Triggered
    }

    public enum EffectActionType
    {
        ApplyModifier,
        RemoveModifier,
        AdjustStat,
        ApplyStatus,
        RemoveStatus,
        Custom
    }

    public enum TriggerConditionType
    {
        None,
        StatBelow,
        StatAbove,
        HasStatus,
        LacksStatus
    }

    public interface IEffect
    {
        string Key { get; }
        EffectType EffectType { get; }
        EffectActionType ActionType { get; }
        string TargetKey { get; }
        float Value { get; }
        float Duration { get; }
        float TickInterval { get; }
        string CustomActionKey { get; }

        // Triggered effect support
        TriggerConditionType TriggerCondition { get; }
        string TriggerKey { get; }
        float TriggerThreshold { get; }

        /// <summary>
        /// The polymorphic action executed on apply / tick and reversed on aura removal.
        /// Populated by <see cref="Effect"/>'s constructor from the legacy action-type parameters,
        /// or supplied directly via the custom-action constructor / <c>EffectBuilder.WithAction</c>.
        /// </summary>
        IEffectAction Action { get; }

        /// <summary>
        /// Predicate checked each tick for <see cref="EffectType.Triggered"/> effects.
        /// Never <c>null</c> — defaults to <see cref="NeverTrigger.Instance"/> when no trigger is configured.
        /// </summary>
        ITriggerCondition Trigger { get; }
    }
}

