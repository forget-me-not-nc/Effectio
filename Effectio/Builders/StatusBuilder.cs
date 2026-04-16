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

        public IStatus Build() => new Status(
            _key,
            _tags.ToArray(),
            _duration,
            _maxStacks,
            _onApply.ToArray(),
            _onTick.ToArray(),
            _onRemove.ToArray(),
            _tickInterval);
    }
}
