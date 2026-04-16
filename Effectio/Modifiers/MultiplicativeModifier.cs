namespace Effectio.Modifiers
{
    /// <summary>Multiplicative contribution: <c>value *= Value</c>.</summary>
    public sealed class MultiplicativeModifier : ModifierBase
    {
        public float Value { get; }
        public override int Priority => ModifierPriority.Multiplicative;

        public MultiplicativeModifier(string key, float value, float duration = -1f, string sourceKey = null)
            : base(key, duration, sourceKey)
        {
            Value = value;
        }

        public override void Apply(ref StatCalculationContext ctx) => ctx.Value *= Value;
    }
}
