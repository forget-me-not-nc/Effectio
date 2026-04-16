using System;
using Effectio.Entities;

namespace Effectio.Statuses
{
    public interface IStatusEngine
    {
        void RegisterStatus(IStatus status);
        void ApplyStatus(IEffectioEntity entity, string statusKey);
        void RemoveStatus(IEffectioEntity entity, string statusKey);
        bool HasStatus(IEffectioEntity entity, string statusKey);
        int GetStacks(IEffectioEntity entity, string statusKey);
        IStatus GetStatusDefinition(string statusKey);
        void Tick(float deltaTime);

        void GrantImmunity(IEffectioEntity entity, string statusKey);
        void RevokeImmunity(IEffectioEntity entity, string statusKey);
        bool IsImmune(IEffectioEntity entity, string statusKey);

        event Action<IEffectioEntity, string> OnStatusBlocked;

        event Action<IEffectioEntity, string> OnStatusApplied;
        event Action<IEffectioEntity, string> OnStatusRemoved;
        event Action<IEffectioEntity, string> OnStatusExpired;
    }
}
