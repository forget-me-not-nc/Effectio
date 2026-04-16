namespace Effectio.Modifiers
{
    /// <summary>
    /// Base class for modifiers. Holds the common identity/duration fields;
    /// subclasses implement <see cref="Apply"/> and declare their <see cref="Priority"/>.
    /// </summary>
    public abstract class ModifierBase : IModifier
    {
        public string Key { get; }
        public string SourceKey { get; }
        public float Duration { get; }
        public float RemainingTime { get; set; }
        public bool IsExpired => Duration >= 0 && RemainingTime <= 0;

        public abstract int Priority { get; }

        protected ModifierBase(string key, float duration = -1f, string sourceKey = null)
        {
            Key = key;
            Duration = duration;
            RemainingTime = duration;
            SourceKey = sourceKey;
        }

        public abstract void Apply(ref StatCalculationContext ctx);
    }
}
