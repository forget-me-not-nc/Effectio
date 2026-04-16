namespace Effectio.Modifiers
{
    public class Modifier : IModifier
    {
        public string Key { get; }
        public ModifierType Type { get; }
        public float Value { get; }
        public float Duration { get; }
        public float RemainingTime { get; set; }
        public bool IsExpired => Duration >= 0 && RemainingTime <= 0;
        public string SourceKey { get; }

        public Modifier(string key, ModifierType type, float value, float duration = -1f, string sourceKey = null)
        {
            Key = key;
            Type = type;
            Value = value;
            Duration = duration;
            RemainingTime = duration;
            SourceKey = sourceKey;
        }
    }
}
