using System;
using System.Collections.Generic;
using Effectio.Stats;
using Effectio.Statuses;

namespace Effectio.Entities
{
    public class EffectioEntity : IEffectioEntity
    {
        private readonly Dictionary<string, IStat> _stats = new Dictionary<string, IStat>();

        /// <summary>The same stats as a flat array, for the tick loop.</summary>
        private IStat[] _statArray = System.Array.Empty<IStat>();
        private readonly HashSet<string> _statusKeys = new HashSet<string>();
        private readonly IStatusEngine _stackQueryEngine;

        public string Id { get; }

        public IReadOnlyCollection<string> StatKeys => _stats.Keys;
        public IReadOnlyCollection<string> ActiveStatusKeys => _statusKeys;

        /// <summary>
        /// v1.0-compatible constructor. Entities built this way have no status-engine
        /// reference; <see cref="GetStatusStackCount"/> returns 0 unconditionally.
        /// Use the v1.1 ctor below (or <c>EffectioManager.CreateEntity</c>, which calls it)
        /// for entities that need real stack counts.
        /// </summary>
        public EffectioEntity(string id)
            : this(id, stackQueryEngine: null)
        {
        }

        /// <summary>
        /// v1.1 constructor accepting a <paramref name="stackQueryEngine"/> reference
        /// used to back <see cref="GetStatusStackCount"/>. Pass <c>null</c> to opt out
        /// of that shortcut - <c>GetStatusStackCount</c> will then return 0.
        /// </summary>
        public EffectioEntity(string id, IStatusEngine stackQueryEngine)
        {
            Id = id;
            _stackQueryEngine = stackQueryEngine;
        }

        public void AddStat(IStat stat)
        {
            if (_stats.ContainsKey(stat.Key))
                throw new InvalidOperationException($"Stat '{stat.Key}' already exists on entity '{Id}'.");

            _stats[stat.Key] = stat;

            // Rebuilt here rather than walked from the dictionary every frame. Stats are added
            // when an entity is built and almost never afterwards, so this pays once for a
            // flat array to tick - and the array is what makes adding a stat from inside a
            // tick safe, since a loop already running holds the old one.
            _statArray = new IStat[_stats.Count];
            _stats.Values.CopyTo(_statArray, 0);
        }

        public IStat GetStat(string key)
        {
            if (_stats.TryGetValue(key, out var stat))
                return stat;

            throw new KeyNotFoundException($"Stat '{key}' not found on entity '{Id}'.");
        }

        public bool TryGetStat(string key, out IStat stat)
        {
            return _stats.TryGetValue(key, out stat);
        }

        public bool HasStat(string key) => _stats.ContainsKey(key);

        /// <summary>
        /// Ticks every stat's modifiers. The hottest loop in the library: once per entity,
        /// every frame.
        ///
        /// Over a flat array rather than the dictionary, which is both faster and the reason
        /// this is safe to re-enter. Ticking a modifier can expire it, which recalculates the
        /// stat, which raises <see cref="Stats.IStat.OnValueChanged"/> - somebody else's code,
        /// free to give this entity a stat it did not have. Walking the dictionary, that threw.
        ///
        /// The array reference is taken once into a local, so a stat added part way through
        /// swaps the field without disturbing the loop in flight; the new stat starts on the
        /// next frame, the same way a newly spawned entity does.
        /// </summary>
        public void TickStatModifiers(float deltaTime)
        {
            var stats = _statArray;

            for (int i = 0; i < stats.Length; i++)
                stats[i].TickModifiers(deltaTime);
        }

        public void AddStatus(string statusKey) => _statusKeys.Add(statusKey);
        public void RemoveStatus(string statusKey) => _statusKeys.Remove(statusKey);
        public bool HasStatus(string statusKey) => _statusKeys.Contains(statusKey);

        /// <summary>
        /// Returns the stack count of <paramref name="statusKey"/> on this entity by
        /// delegating to the status engine wired at construction. Entities constructed
        /// without an engine ref (single-arg ctor) return 0 unconditionally.
        /// </summary>
        public int GetStatusStackCount(string statusKey)
            => _stackQueryEngine?.GetStacks(this, statusKey) ?? 0;

        public void CopyStatusKeysTo(ICollection<string> dest)
        {
            // Iterates the concrete HashSet via its struct enumerator — no boxing.
            foreach (var key in _statusKeys)
                dest.Add(key);
        }
    }
}
