using Effectio.Effects;

namespace Effectio.Statuses
{
    public class Status : IStatus
    {
        public string Key { get; }
        public string[] Tags { get; }
        public float Duration { get; }
        public int MaxStacks { get; }
        public IEffect[] OnApplyEffects { get; }
        public IEffect[] OnTickEffects { get; }
        public IEffect[] OnRemoveEffects { get; }
        public float TickInterval { get; }

        public Status(
            string key,
            string[] tags = null,
            float duration = -1f,
            int maxStacks = 1,
            IEffect[] onApplyEffects = null,
            IEffect[] onTickEffects = null,
            IEffect[] onRemoveEffects = null,
            float tickInterval = 1f)
        {
            Key = key;
            Tags = tags ?? new string[0];
            Duration = duration;
            MaxStacks = maxStacks;
            OnApplyEffects = onApplyEffects ?? new IEffect[0];
            OnTickEffects = onTickEffects ?? new IEffect[0];
            OnRemoveEffects = onRemoveEffects ?? new IEffect[0];
            TickInterval = tickInterval;
        }
    }
}
