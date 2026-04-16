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

        public Effect(
            string key,
            EffectType effectType,
            EffectActionType actionType,
            string targetKey,
            float value = 0f,
            float duration = -1f,
            float tickInterval = 0f,
            string customActionKey = null)
        {
            Key = key;
            EffectType = effectType;
            ActionType = actionType;
            TargetKey = targetKey;
            Value = value;
            Duration = duration;
            TickInterval = tickInterval;
            CustomActionKey = customActionKey;
        }
    }
}
