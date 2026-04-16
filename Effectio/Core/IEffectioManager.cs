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

        IEffectioEntity CreateEntity(string id);
        IEffectioEntity GetEntity(string id);
        bool TryGetEntity(string id, out IEffectioEntity entity);
        void RemoveEntity(string id);

        void Tick(float deltaTime);
    }
}
