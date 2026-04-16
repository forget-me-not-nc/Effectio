using Effectio.Modifiers;

namespace Effectio.Effects.Actions
{
    /// <summary>Adjusts <see cref="IStat.BaseValue"/> by <see cref="Value"/>.</summary>
    public sealed class AdjustStatAction : IEffectAction
    {
        public string TargetStatKey { get; }
        public float Value { get; }

        public AdjustStatAction(string targetStatKey, float value)
        {
            TargetStatKey = targetStatKey;
            Value = value;
        }

        public void Execute(in EffectActionContext ctx)
        {
            if (!ctx.Entity.HasStat(TargetStatKey)) return;
            var stat = ctx.Entity.GetStat(TargetStatKey);
            stat.BaseValue += Value;
            stat.Recalculate();
        }

        public void Undo(in EffectActionContext ctx)
        {
            if (!ctx.Entity.HasStat(TargetStatKey)) return;
            var stat = ctx.Entity.GetStat(TargetStatKey);
            stat.BaseValue -= Value;
            stat.Recalculate();
        }
    }

    /// <summary>Attaches an <see cref="AdditiveModifier"/> with the effect's key/duration/source.</summary>
    public sealed class ApplyModifierAction : IEffectAction
    {
        public string TargetStatKey { get; }
        public float Value { get; }

        public ApplyModifierAction(string targetStatKey, float value)
        {
            TargetStatKey = targetStatKey;
            Value = value;
        }

        public void Execute(in EffectActionContext ctx)
        {
            if (!ctx.Entity.HasStat(TargetStatKey)) return;
            var stat = ctx.Entity.GetStat(TargetStatKey);
            stat.AddModifier(new AdditiveModifier(
                ctx.Effect.Key + "_mod",
                Value,
                ctx.Effect.Duration,
                ctx.Effect.Key));
        }

        public void Undo(in EffectActionContext ctx)
        {
            if (!ctx.Entity.HasStat(TargetStatKey)) return;
            var stat = ctx.Entity.GetStat(TargetStatKey);
            stat.RemoveModifiersFromSource(ctx.Effect.Key);
        }
    }

    /// <summary>
    /// Removes modifiers with <see cref="ModifierKey"/> from the stat that shares the same key.
    /// Note: historical behavior — the key doubles as both stat-key lookup and modifier-key match.
    /// </summary>
    public sealed class RemoveModifierAction : IEffectAction
    {
        public string ModifierKey { get; }

        public RemoveModifierAction(string modifierKey)
        {
            ModifierKey = modifierKey;
        }

        public void Execute(in EffectActionContext ctx)
        {
            if (!ctx.Entity.HasStat(ModifierKey)) return;
            ctx.Entity.GetStat(ModifierKey).RemoveModifier(ModifierKey);
        }

        public void Undo(in EffectActionContext ctx) { /* removal is not reversible */ }
    }

    /// <summary>Applies a registered status through the status engine.</summary>
    public sealed class ApplyStatusAction : IEffectAction
    {
        public string StatusKey { get; }

        public ApplyStatusAction(string statusKey)
        {
            StatusKey = statusKey;
        }

        public void Execute(in EffectActionContext ctx) => ctx.StatusEngine.ApplyStatus(ctx.Entity, StatusKey);

        public void Undo(in EffectActionContext ctx) => ctx.StatusEngine.RemoveStatus(ctx.Entity, StatusKey);
    }

    /// <summary>Removes a status through the status engine.</summary>
    public sealed class RemoveStatusAction : IEffectAction
    {
        public string StatusKey { get; }

        public RemoveStatusAction(string statusKey)
        {
            StatusKey = statusKey;
        }

        public void Execute(in EffectActionContext ctx) => ctx.StatusEngine.RemoveStatus(ctx.Entity, StatusKey);

        public void Undo(in EffectActionContext ctx) { /* removal is not reversible */ }
    }

    /// <summary>
    /// Placeholder action for user-defined behavior. Game code listens to
    /// <c>EffectsEngine.OnEffectApplied</c> / <c>OnEffectTick</c> and dispatches on
    /// <see cref="CustomActionKey"/>. Default <see cref="Execute"/>/<see cref="Undo"/> are no-ops.
    /// </summary>
    public class CustomAction : IEffectAction
    {
        public string CustomActionKey { get; }

        public CustomAction(string customActionKey)
        {
            CustomActionKey = customActionKey;
        }

        public virtual void Execute(in EffectActionContext ctx) { }
        public virtual void Undo(in EffectActionContext ctx) { }
    }
}
