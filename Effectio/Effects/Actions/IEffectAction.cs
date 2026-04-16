using Effectio.Entities;
using Effectio.Statuses;

namespace Effectio.Effects.Actions
{
    /// <summary>
    /// Context passed to <see cref="IEffectAction.Execute"/> / <see cref="IEffectAction.Undo"/>.
    /// Bundles the entity the effect is acting on, the effect itself (for <c>Key</c>/<c>Duration</c>
    /// lookups), and the <see cref="IStatusEngine"/> for status-affecting actions.
    /// </summary>
    public struct EffectActionContext
    {
        public IEffectioEntity Entity;
        public IEffect Effect;
        public IStatusEngine StatusEngine;
    }

    /// <summary>
    /// Polymorphic effect action. Implementations own their own target keys / values
    /// and self-describe how they apply (<see cref="Execute"/>) and reverse (<see cref="Undo"/>).
    /// </summary>
    public interface IEffectAction
    {
        /// <summary>Apply this action's effect on <paramref name="ctx"/>.</summary>
        void Execute(in EffectActionContext ctx);

        /// <summary>Reverse this action's effect. Called by aura expiration/removal. May be a no-op.</summary>
        void Undo(in EffectActionContext ctx);
    }
}
