using Effectio.Effects;
using Effectio.Entities;
using Effectio.Reactions;
using Effectio.Statuses;

namespace Effectio.Core
{
    public interface IEffectioManager
    {
        IEffectsEngine Effects { get; }
        IStatusEngine Statuses { get; }
        IReactionEngine Reactions { get; }

        /// <summary>Registers a new entity. Throws if <paramref name="id"/> is already taken.</summary>
        IEffectioEntity CreateEntity(string id);

        /// <summary>
        /// The entity under <paramref name="id"/>. <b>Throws</b> when there is none - this is
        /// the dictionary-indexer shape, for code that would rather fail than carry a null.
        /// Use <see cref="TryGetEntity"/> where absence is an ordinary answer.
        ///
        /// Note that <see cref="Statuses.IStatusEngine.GetStatusDefinition"/> does the
        /// opposite and returns null. The two are not consistent and cannot be made so
        /// without breaking one of them; a v2 candidate.
        /// </summary>
        IEffectioEntity GetEntity(string id);

        bool TryGetEntity(string id, out IEffectioEntity entity);

        /// <summary>
        /// Removes an entity along with its statuses, immunities and active effects. Safe to
        /// call from inside <see cref="Tick"/> - the entity stops being ticked for the rest of
        /// that frame.
        /// </summary>
        void RemoveEntity(string id);

        void Tick(float deltaTime);
    }
}
