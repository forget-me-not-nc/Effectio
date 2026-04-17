using System;
using System.Collections.Generic;
using Effectio.Stats;

namespace Effectio.Entities
{
    public class EffectioEntity : IEffectioEntity
    {
        private readonly Dictionary<string, IStat> _stats = new Dictionary<string, IStat>();
        private readonly HashSet<string> _statusKeys = new HashSet<string>();

        public string Id { get; }

        public IReadOnlyCollection<string> StatKeys => _stats.Keys;
        public IReadOnlyCollection<string> ActiveStatusKeys => _statusKeys;

        public EffectioEntity(string id)
        {
            Id = id;
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

        public void CopyStatusKeysTo(ICollection<string> dest)
        {
            // Iterates the concrete HashSet via its struct enumerator — no boxing.
            foreach (var key in _statusKeys)
                dest.Add(key);
        }
    }
}
