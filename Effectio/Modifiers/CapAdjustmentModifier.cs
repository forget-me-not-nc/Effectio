namespace Effectio.Modifiers
{
    /// <summary>Extends <see cref="StatCalculationContext.EffectiveMax"/> by <see cref="Value"/>.</summary>
    public sealed class CapAdjustmentModifier : ModifierBase
    {
        public float Value { get; }
        public override int Priority => ModifierPriority.CapAdjustment;

        public CapAdjustmentModifier(string key, float value, float duration = -1f, string sourceKey = null)
            : base(key, duration, sourceKey)
        {
            Value = value;
        }

        public override void Apply(ref StatCalculationContext ctx) => ctx.EffectiveMax += Value;
    }
}
