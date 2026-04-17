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

        /// <summary>
        /// Hot-path helper: ticks all stats' modifiers without allocating an enumerator box.
        /// Called by <c>EffectioManager.Tick</c> once per entity per frame.
        /// </summary>
        void TickStatModifiers(float deltaTime);

        // Statuses
        IReadOnlyCollection<string> ActiveStatusKeys { get; }
        void AddStatus(string statusKey);
        void RemoveStatus(string statusKey);
        bool HasStatus(string statusKey);

        /// <summary>
        /// Hot-path helper: copies all active status keys into <paramref name="dest"/> without
        /// allocating an <see cref="System.Collections.IEnumerator"/> box, which a plain
        /// <c>foreach</c> over <see cref="ActiveStatusKeys"/> (typed as <see cref="IReadOnlyCollection{T}"/>)
        /// would trigger. Used by <c>ReactionEngine</c> for alloc-free status snapshotting.
        /// </summary>
        void CopyStatusKeysTo(System.Collections.Generic.ICollection<string> dest);
    }
}
