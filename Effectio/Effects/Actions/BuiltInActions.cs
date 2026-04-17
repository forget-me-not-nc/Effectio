using System;
using Effectio.Modifiers;
using Effectio.Stats;

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

    /// <summary>
    /// Attaches a modifier to <see cref="TargetStatKey"/>. The modifier is produced by
    /// <see cref="ModifierFactory"/> on each <see cref="Execute"/>, so every application
    /// gets a fresh instance with its own <c>RemainingTime</c>. Works for any
    /// <see cref="IModifier"/> kind — additive, multiplicative, cap-adjusting, or custom.
    /// </summary>
    public sealed class ApplyModifierAction : IEffectAction
    {
        public string TargetStatKey { get; }

        /// <summary>The nominal value for the legacy additive shortcut; <c>0</c> when constructed with a factory.</summary>
        public float Value { get; }

        public Func<IEffect, IModifier> ModifierFactory { get; }

        /// <summary>
        /// Shortcut that attaches a fresh <see cref="AdditiveModifier"/> per apply, using the
        /// effect's <c>Key + "_mod"</c>/<c>Duration</c>/<c>Key</c> for identity/duration/source.
        /// </summary>
        public ApplyModifierAction(string targetStatKey, float value)
        {
            TargetStatKey = targetStatKey;
            Value = value;
            ModifierFactory = e => new AdditiveModifier(e.Key + "_mod", value, e.Duration, e.Key);
        }

        /// <summary>Applies a modifier produced by <paramref name="modifierFactory"/> — any <see cref="IModifier"/> kind.</summary>
        public ApplyModifierAction(string targetStatKey, Func<IEffect, IModifier> modifierFactory)
        {
            if (modifierFactory == null) throw new ArgumentNullException(nameof(modifierFactory));
            TargetStatKey = targetStatKey;
            Value = 0f;
            ModifierFactory = modifierFactory;
        }

        public void Execute(in EffectActionContext ctx)
        {
            if (!ctx.Entity.HasStat(TargetStatKey)) return;
            ctx.Entity.GetStat(TargetStatKey).AddModifier(ModifierFactory(ctx.Effect));
        }

        public void Undo(in EffectActionContext ctx)
        {
            if (!ctx.Entity.HasStat(TargetStatKey)) return;
            ctx.Entity.GetStat(TargetStatKey).RemoveModifiersFromSource(ctx.Effect.Key);
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
