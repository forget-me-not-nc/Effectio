using System;
using Effectio.Entities;

namespace Effectio.Reactions
{
    public interface IReactionEngine
    {
        int MaxChainDepth { get; set; }

        void RegisterReaction(IReaction reaction);
        void RemoveReaction(string reactionKey);
        void CheckReactions(IEffectioEntity entity);

        event Action<IEffectioEntity, IReaction> OnReactionTriggered;
    }
}
