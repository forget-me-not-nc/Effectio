using System.Collections.Generic;
using Effectio.Effects;
using Effectio.Statuses;

namespace Effectio.Builders
{
    /// <summary>
    /// Fluent builder for <see cref="IStatus"/>.
    /// </summary>
    public class StatusBuilder
    {
        private readonly string _key;
        private readonly List<string> _tags = new List<string>();
        private float _duration = -1f;
        private int _maxStacks = 1;
        private float _tickInterval = 1f;
        private readonly List<IEffect> _onApply = new List<IEffect>();
        private readonly List<IEffect> _onTick = new List<IEffect>();
        private readonly List<IEffect> _onRemove = new List<IEffect>();
        private readonly List<IEffect> _onRefresh = new List<IEffect>();

        public StatusBuilder(string key)
        {
            _key = key;
        }

        public static StatusBuilder Create(string key) => new StatusBuilder(key);

        public StatusBuilder WithTag(string tag) { _tags.Add(tag); return this; }

        public StatusBuilder WithTags(params string[] tags)
        {
            if (tags != null) _tags.AddRange(tags);
            return this;
        }

        public StatusBuilder WithDuration(float duration) { _duration = duration; return this; }
        public StatusBuilder Permanent() { _duration = -1f; return this; }
        public StatusBuilder Stackable(int maxStacks) { _maxStacks = maxStacks; return this; }
        public StatusBuilder WithTickInterval(float interval) { _tickInterval = interval; return this; }

        public StatusBuilder OnApply(IEffect effect) { _onApply.Add(effect); return this; }
        public StatusBuilder OnApply(EffectBuilder effect) { _onApply.Add(effect.Build()); return this; }
        public StatusBuilder OnTick(IEffect effect) { _onTick.Add(effect); return this; }
        public StatusBuilder OnTick(EffectBuilder effect) { _onTick.Add(effect.Build()); return this; }
        public StatusBuilder OnRemove(IEffect effect) { _onRemove.Add(effect); return this; }
        public StatusBuilder OnRemove(EffectBuilder effect) { _onRemove.Add(effect.Build()); return this; }

        /// <summary>
        /// Fire <paramref name="effect"/> every time <c>ApplyStatus</c> is called against
        /// an entity that already has this status (whether stacks increment or are at
        /// <see cref="Stackable(int)"/> max). Useful for "stand-in-flame" patterns:
        /// each re-application bursts a damage tick, regardless of whether the stack
        /// counter actually moved. Does NOT fire on first application (use
        /// <see cref="OnApply(IEffect)"/>) or on partial
        /// <c>IStackOperations.RemoveStacks</c> decrement.
        /// </summary>
        public StatusBuilder OnRefresh(IEffect effect) { _onRefresh.Add(effect); return this; }

        /// <summary>Convenience overload accepting an <see cref="EffectBuilder"/>.</summary>
        public StatusBuilder OnRefresh(EffectBuilder effect) { _onRefresh.Add(effect.Build()); return this; }

        public IStatus Build() => new Status(
            _key,
            _tags.ToArray(),
            _duration,
            _maxStacks,
            _onApply.ToArray(),
            _onTick.ToArray(),
            _onRemove.ToArray(),
            _tickInterval,
            _onRefresh.Count > 0 ? _onRefresh.ToArray() : null);
    }
}
