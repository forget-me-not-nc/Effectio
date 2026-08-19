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

        /// <summary>
        /// The status this effect is running on behalf of, or <c>null</c> when it was applied
        /// directly rather than by a status.
        ///
        /// The piece that was missing for an action to answer "how many of me are there".
        /// Everything else was already here - <see cref="Entity"/> and
        /// <see cref="StatusEngine"/> between them can count stacks of any key - but nothing
        /// said which key had caused this tick, so an action could only ask about a status it
        /// had been told about in its own constructor. That makes a scale-by-my-stacks action
        /// one class per status instead of one class.
        ///
        /// With it, a single action serves every stacking status:
        /// <code>
        /// public void Execute(in EffectActionContext ctx)
        /// {
        ///     var stacks = ctx.SourceStatusKey == null
        ///         ? 1
        ///         : ctx.StatusEngine.GetStacks(ctx.Entity, ctx.SourceStatusKey);
        ///
        ///     var stat = ctx.Entity.GetStat("Health");
        ///     stat.BaseValue -= _perStack * stacks;
        ///     stat.Recalculate();
        /// }
        /// </code>
        ///
        /// Null is a real answer and not a missing one: an effect applied straight through
        /// <c>Effects.ApplyEffect</c> genuinely has no owning status, and an action that
        /// scales by stacks should treat that as one.
        /// </summary>
        public string SourceStatusKey;
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
