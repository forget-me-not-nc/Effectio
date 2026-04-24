using System;
using System.Collections.Generic;
using Effectio.Stats;
using Effectio.Statuses;

namespace Effectio.Entities
{
    public class EffectioEntity : IEffectioEntity
    {
        private readonly Dictionary<string, IStat> _stats = new Dictionary<string, IStat>();
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

        public void TickStatModifiers(float deltaTime)
        {
            // Uses Dictionary<K,V>.Enumerator (struct) — no enumerator boxing on the hot path.
            foreach (var kvp in _stats)
                kvp.Value.TickModifiers(deltaTime);
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
