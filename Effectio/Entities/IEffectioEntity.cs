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
        /// Stack count of <paramref name="statusKey"/> on this entity, or <c>0</c> if absent.
        /// Ergonomic shortcut for <c>manager.Statuses.GetStacks(entity, statusKey)</c>.
        /// </summary>
        /// <remarks>
        /// The built-in <see cref="EffectioEntity"/> queries through a status-engine
        /// reference wired at construction time. <see cref="Core.EffectioManager.CreateEntity"/>
        /// passes that reference automatically. Entities constructed manually with the
        /// single-argument <see cref="EffectioEntity(string)"/> ctor have no engine ref and
        /// return 0 for every key - use the manager-bound construction path for any entity
        /// that should report real stack counts.
        /// </remarks>
        int GetStatusStackCount(string statusKey);

        /// <summary>
        /// Hot-path helper: copies all active status keys into <paramref name="dest"/> without
        /// allocating an <see cref="System.Collections.IEnumerator"/> box, which a plain
        /// <c>foreach</c> over <see cref="ActiveStatusKeys"/> (typed as <see cref="IReadOnlyCollection{T}"/>)
        /// would trigger. Used by <c>ReactionEngine</c> for alloc-free status snapshotting.
        /// </summary>
        void CopyStatusKeysTo(System.Collections.Generic.ICollection<string> dest);
    }
}
