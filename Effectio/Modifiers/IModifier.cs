namespace Effectio.Modifiers
{
    public enum ModifierType
    {
        Additive,
        Multiplicative,
        CapAdjustment
    }

    public interface IModifier
    {
        string Key { get; }
        ModifierType Type { get; }
        float Value { get; }
        float Duration { get; }
        float RemainingTime { get; set; }
        bool IsExpired { get; }
        string SourceKey { get; }
    }
}
