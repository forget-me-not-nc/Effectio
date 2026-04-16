using System.Collections.Generic;
using Effectio.Common.Exceptions;
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
                throw new StatException($"Stat '{stat.Key}' already exists on entity '{Id}'.", stat.Key);

            _stats[stat.Key] = stat;
        }

        public IStat GetStat(string key)
        {
            if (_stats.TryGetValue(key, out var stat))
                return stat;

            throw new StatException($"Stat '{key}' not found on entity '{Id}'.", key);
        }

        public bool HasStat(string key) => _stats.ContainsKey(key);

        public void AddStatus(string statusKey) => _statusKeys.Add(statusKey);
        public void RemoveStatus(string statusKey) => _statusKeys.Remove(statusKey);
        public bool HasStatus(string statusKey) => _statusKeys.Contains(statusKey);
    }
}
