namespace Effectio.Modifiers
{
    /// <summary>Flat additive contribution: <c>value += Value</c>.</summary>
    public sealed class AdditiveModifier : ModifierBase
    {
        public float Value { get; }
        public override int Priority => ModifierPriority.Additive;

        public AdditiveModifier(string key, float value, float duration = -1f, string sourceKey = null)
            : base(key, duration, sourceKey)
        {
            Value = value;
        }

        public override void Apply(ref StatCalculationContext ctx) => ctx.Value += Value;
    }
}
