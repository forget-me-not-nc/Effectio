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
        public IEffect[] OnRefreshEffects { get; }
        public float TickInterval { get; }

        /// <summary>
        /// v1.0 / early-v1.1 constructor (no <c>OnRefreshEffects</c>). Kept as a distinct
        /// 8-parameter overload so pre-built consumers that resolved this exact IL
        /// signature continue to load without a <c>MissingMethodException</c>.
        /// New callers should prefer the 9-parameter overload (or <c>StatusBuilder</c>).
        /// </summary>
        public Status(
            string key,
            string[] tags = null,
            float duration = -1f,
            int maxStacks = 1,
            IEffect[] onApplyEffects = null,
            IEffect[] onTickEffects = null,
            IEffect[] onRemoveEffects = null,
            float tickInterval = 1f)
            : this(key, tags, duration, maxStacks, onApplyEffects, onTickEffects,
                   onRemoveEffects, tickInterval, onRefreshEffects: null)
        {
        }

        /// <summary>v1.1 constructor adding <paramref name="onRefreshEffects"/>.</summary>
        public Status(
            string key,
            string[] tags,
            float duration,
            int maxStacks,
            IEffect[] onApplyEffects,
            IEffect[] onTickEffects,
            IEffect[] onRemoveEffects,
            float tickInterval,
            IEffect[] onRefreshEffects)
        {
            Key = key;
            Tags = tags ?? new string[0];
            Duration = duration;
            MaxStacks = maxStacks;
            OnApplyEffects = onApplyEffects ?? new IEffect[0];
            OnTickEffects = onTickEffects ?? new IEffect[0];
            OnRemoveEffects = onRemoveEffects ?? new IEffect[0];
            OnRefreshEffects = onRefreshEffects ?? new IEffect[0];
            TickInterval = tickInterval;
        }
    }
}
