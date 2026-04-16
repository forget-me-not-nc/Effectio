using System.Collections.Generic;
using Effectio.Stats;

namespace Effectio.Entities
{
    public interface IEffectioEntity
    {
        string Id { get; }

        // Stats
        IStat GetStat(string key);
        bool TryGetStat(string key, out IStat stat);
        bool HasStat(string key);
        IReadOnlyCollection<string> StatKeys { get; }
        void AddStat(IStat stat);

        // Statuses
        IReadOnlyCollection<string> ActiveStatusKeys { get; }
        void AddStatus(string statusKey);
        void RemoveStatus(string statusKey);
        bool HasStatus(string statusKey);
    }
}
