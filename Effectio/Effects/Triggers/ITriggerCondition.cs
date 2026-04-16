using Effectio.Entities;

namespace Effectio.Effects.Triggers
{
    /// <summary>Context passed to <see cref="ITriggerCondition.IsSatisfied"/>.</summary>
    public struct TriggerContext
    {
        public IEffectioEntity Entity;
    }

    /// <summary>
    /// Predicate that decides whether a <see cref="EffectType.Triggered"/> effect should fire.
    /// Built-in implementations cover stat thresholds and status presence/absence; library users
    /// can implement <see cref="ITriggerCondition"/> or combine the built-ins via
    /// <see cref="AndTrigger"/> / <see cref="OrTrigger"/> / <see cref="NotTrigger"/>.
    /// </summary>
    public interface ITriggerCondition
    {
        bool IsSatisfied(in TriggerContext ctx);
    }

    /// <summary>Never fires — used when an effect has no trigger configured.</summary>
    public sealed class NeverTrigger : ITriggerCondition
    {
        public static readonly NeverTrigger Instance = new NeverTrigger();
        private NeverTrigger() { }
        public bool IsSatisfied(in TriggerContext ctx) => false;
    }

    /// <summary>Fires when <c>Entity.GetStat(StatKey).CurrentValue &lt; Threshold</c>.</summary>
    public sealed class StatBelowTrigger : ITriggerCondition
    {
        public string StatKey { get; }
        public float Threshold { get; }
        public StatBelowTrigger(string statKey, float threshold) { StatKey = statKey; Threshold = threshold; }
        public bool IsSatisfied(in TriggerContext ctx)
            => ctx.Entity.TryGetStat(StatKey, out var s) && s.CurrentValue < Threshold;
    }

    /// <summary>Fires when <c>Entity.GetStat(StatKey).CurrentValue &gt; Threshold</c>.</summary>
    public sealed class StatAboveTrigger : ITriggerCondition
    {
        public string StatKey { get; }
        public float Threshold { get; }
        public StatAboveTrigger(string statKey, float threshold) { StatKey = statKey; Threshold = threshold; }
        public bool IsSatisfied(in TriggerContext ctx)
            => ctx.Entity.TryGetStat(StatKey, out var s) && s.CurrentValue > Threshold;
    }

    /// <summary>Fires when the entity has the status.</summary>
    public sealed class HasStatusTrigger : ITriggerCondition
    {
        public string StatusKey { get; }
        public HasStatusTrigger(string statusKey) { StatusKey = statusKey; }
        public bool IsSatisfied(in TriggerContext ctx) => ctx.Entity.HasStatus(StatusKey);
    }

    /// <summary>Fires when the entity does not have the status.</summary>
    public sealed class LacksStatusTrigger : ITriggerCondition
    {
        public string StatusKey { get; }
        public LacksStatusTrigger(string statusKey) { StatusKey = statusKey; }
        public bool IsSatisfied(in TriggerContext ctx) => !ctx.Entity.HasStatus(StatusKey);
    }

    /// <summary>Composite — all children must be satisfied.</summary>
    public sealed class AndTrigger : ITriggerCondition
    {
        private readonly ITriggerCondition[] _children;
        public AndTrigger(params ITriggerCondition[] children) { _children = children ?? new ITriggerCondition[0]; }
        public bool IsSatisfied(in TriggerContext ctx)
        {
            for (int i = 0; i < _children.Length; i++)
                if (!_children[i].IsSatisfied(in ctx)) return false;
            return _children.Length > 0;
        }
    }

    /// <summary>Composite — at least one child must be satisfied.</summary>
    public sealed class OrTrigger : ITriggerCondition
    {
        private readonly ITriggerCondition[] _children;
        public OrTrigger(params ITriggerCondition[] children) { _children = children ?? new ITriggerCondition[0]; }
        public bool IsSatisfied(in TriggerContext ctx)
        {
            for (int i = 0; i < _children.Length; i++)
                if (_children[i].IsSatisfied(in ctx)) return true;
            return false;
        }
    }

    /// <summary>Inverts a child condition.</summary>
    public sealed class NotTrigger : ITriggerCondition
    {
        private readonly ITriggerCondition _child;
        public NotTrigger(ITriggerCondition child) { _child = child; }
        public bool IsSatisfied(in TriggerContext ctx) => _child != null && !_child.IsSatisfied(in ctx);
    }
}
