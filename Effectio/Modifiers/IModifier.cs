namespace Effectio.Modifiers
{
    /// <summary>
    /// Mutable calculation state threaded through the modifier pipeline.
    /// Passed by <c>ref</c> so modifiers can mutate in-place without allocation.
    /// </summary>
    public struct StatCalculationContext
    {
        /// <summary>The running stat value (starts at <c>BaseValue</c>).</summary>
        public float Value;

        /// <summary>Effective lower clamp (starts at <c>Stat.Min</c>).</summary>
        public float EffectiveMin;

        /// <summary>Effective upper clamp (starts at <c>Stat.Max</c>).</summary>
        public float EffectiveMax;
    }

    /// <summary>
    /// Well-known priority bands. Lower values apply earlier.
    /// Custom modifiers may use any <c>int</c>; these constants are conveniences.
    /// </summary>
    public static class ModifierPriority
    {
        public const int Override       = 50;
        public const int Additive       = 100;
        public const int Multiplicative = 200;
        public const int CapAdjustment  = 300;
    }

    public interface IModifier
    {
        string Key { get; }
        string SourceKey { get; }
        float Duration { get; }
        float RemainingTime { get; set; }
        bool IsExpired { get; }

        /// <summary>Ordering key for the modifier pipeline; lower applies earlier. Stable within equal priorities.</summary>
        int Priority { get; }

        /// <summary>Apply this modifier's contribution to the running <paramref name="ctx"/>.</summary>
        void Apply(ref StatCalculationContext ctx);
    }
}

