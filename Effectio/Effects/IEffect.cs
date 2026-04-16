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
    }
}
