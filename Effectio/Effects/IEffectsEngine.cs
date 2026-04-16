using System;
using Effectio.Entities;

namespace Effectio.Effects
{
    public interface IEffectsEngine
    {
        void ApplyEffect(IEffectioEntity entity, IEffect effect);
        void RemoveEffect(IEffectioEntity entity, string effectKey);
        void Tick(float deltaTime);

        event Action<IEffectioEntity, IEffect> OnEffectApplied;
        event Action<IEffectioEntity, IEffect> OnEffectRemoved;
        event Action<IEffectioEntity, IEffect> OnEffectTick;
    }
}
