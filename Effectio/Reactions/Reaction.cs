namespace Effectio.Reactions
{
    /// <summary>
    /// Legacy-shape result storing a <see cref="ReactionResultType"/> tag plus target key/value.
    /// Retains backward-compatible construction; <see cref="Execute"/> dispatches by <see cref="Type"/>.
    /// Prefer the built-in concrete types (<see cref="ApplyStatusResult"/>, <see cref="AdjustStatResult"/>, …)
    /// or your own <see cref="IReactionResult"/> implementation for new code.
    /// </summary>
    public class ReactionResult : IReactionResult
    {
        public ReactionResultType Type { get; }
        public string TargetKey { get; }
        public float Value { get; }

        public ReactionResult(ReactionResultType type, string targetKey, float value = 0f)
        {
            Type = type;
            TargetKey = targetKey;
            Value = value;
        }

        public void Execute(in ReactionResultContext ctx)
        {
            switch (Type)
            {
                case ReactionResultType.ApplyStatus:  ctx.StatusEngine.ApplyStatus(ctx.Entity, TargetKey); break;
                case ReactionResultType.RemoveStatus: ctx.StatusEngine.RemoveStatus(ctx.Entity, TargetKey); break;
                case ReactionResultType.AdjustStat:   ctx.AdjustStat?.Invoke(ctx.Entity, TargetKey, Value); break;
                case ReactionResultType.ApplyEffect:  ctx.ApplyEffect?.Invoke(ctx.Entity, TargetKey); break;
                case ReactionResultType.Custom:       /* handled via OnReactionTriggered event */ break;
            }
        }
    }

    /// <summary>Applies a status via the status engine.</summary>
    public sealed class ApplyStatusResult : IReactionResult
    {
        public ReactionResultType Type => ReactionResultType.ApplyStatus;
        public string TargetKey { get; }
        public float Value => 0f;

        public ApplyStatusResult(string statusKey) { TargetKey = statusKey; }

        public void Execute(in ReactionResultContext ctx) => ctx.StatusEngine.ApplyStatus(ctx.Entity, TargetKey);
    }

    /// <summary>Removes a status via the status engine.</summary>
    public sealed class RemoveStatusResult : IReactionResult
    {
        public ReactionResultType Type => ReactionResultType.RemoveStatus;
        public string TargetKey { get; }
        public float Value => 0f;

        public RemoveStatusResult(string statusKey) { TargetKey = statusKey; }

        public void Execute(in ReactionResultContext ctx) => ctx.StatusEngine.RemoveStatus(ctx.Entity, TargetKey);
    }

    /// <summary>Adjusts a stat's base value through the host manager's wired callback.</summary>
    public sealed class AdjustStatResult : IReactionResult
    {
        public ReactionResultType Type => ReactionResultType.AdjustStat;
        public string TargetKey { get; }
        public float Value { get; }

        public AdjustStatResult(string statKey, float value) { TargetKey = statKey; Value = value; }

        public void Execute(in ReactionResultContext ctx) => ctx.AdjustStat?.Invoke(ctx.Entity, TargetKey, Value);
    }

    /// <summary>Dispatches an effect-apply callback (manager decides how to resolve it).</summary>
    public sealed class ApplyEffectResult : IReactionResult
    {
        public ReactionResultType Type => ReactionResultType.ApplyEffect;
        public string TargetKey { get; }
        public float Value => 0f;

        public ApplyEffectResult(string effectKey) { TargetKey = effectKey; }

        public void Execute(in ReactionResultContext ctx) => ctx.ApplyEffect?.Invoke(ctx.Entity, TargetKey);
    }

    /// <summary>
    /// No-op built-in for custom reaction behavior. Game code reacts via the
    /// <c>ReactionEngine.OnReactionTriggered</c> event and inspects the reaction.
    /// </summary>
    public class CustomReactionResult : IReactionResult
    {
        public ReactionResultType Type => ReactionResultType.Custom;
        public string TargetKey { get; }
        public float Value { get; }

        public CustomReactionResult(string targetKey = null, float value = 0f)
        {
            TargetKey = targetKey;
            Value = value;
        }

        public virtual void Execute(in ReactionResultContext ctx) { }
    }

    public class Reaction : IPrioritizedReaction, IStackAwareReaction
    {
        public string Key { get; }
        public string[] RequiredStatusKeys { get; }
        public string[] RequiredTags { get; }
        public bool ConsumesStatuses { get; }
        public IReactionResult[] Results { get; }
        public int Priority { get; }
        public StackRequirement[] RequiredStacks { get; }
        public StackConsume[] StackConsumes { get; }

        /// <summary>
        /// v1.0-compatible constructor. Kept as a distinct overload (not merged into the
        /// 6-parameter form via an optional argument) so the original IL method signature
        /// still exists; pre-built v1.0 consumers calling this ctor continue to resolve
        /// it without a <c>MissingMethodException</c>. New callers should prefer the
        /// 8-parameter overload (or use <see cref="Effectio.Builders.ReactionBuilder"/>).
        /// </summary>
        public Reaction(
            string key,
            string[] requiredStatusKeys = null,
            string[] requiredTags = null,
            bool consumesStatuses = true,
            IReactionResult[] results = null)
            : this(key, requiredStatusKeys, requiredTags, consumesStatuses, results, priority: 0)
        {
        }

        /// <summary>
        /// v1.1 constructor adding the <paramref name="priority"/> tier. Kept as a distinct
        /// 6-parameter overload so binary consumers compiled against the early-v1.1
        /// preview signature continue to resolve it.
        /// </summary>
        public Reaction(
            string key,
            string[] requiredStatusKeys,
            string[] requiredTags,
            bool consumesStatuses,
            IReactionResult[] results,
            int priority)
            : this(key, requiredStatusKeys, requiredTags, consumesStatuses, results, priority,
                   requiredStacks: null, stackConsumes: null)
        {
        }

        /// <summary>
        /// v1.1 constructor adding stack-aware fields (<paramref name="requiredStacks"/>
        /// and <paramref name="stackConsumes"/>). Reactions whose <c>requiredStacks</c>
        /// is empty match purely on <see cref="RequiredStatusKeys"/> / <see cref="RequiredTags"/>.
        /// Reactions whose <c>stackConsumes</c> is empty fall back to <see cref="ConsumesStatuses"/>
        /// for whole-status removal on fire.
        /// </summary>
        public Reaction(
            string key,
            string[] requiredStatusKeys,
            string[] requiredTags,
            bool consumesStatuses,
            IReactionResult[] results,
            int priority,
            StackRequirement[] requiredStacks,
            StackConsume[] stackConsumes)
        {
            Key = key;
            RequiredStatusKeys = requiredStatusKeys ?? new string[0];
            RequiredTags = requiredTags ?? new string[0];
            ConsumesStatuses = consumesStatuses;
            Results = results ?? new IReactionResult[0];
            Priority = priority;
            RequiredStacks = requiredStacks ?? new StackRequirement[0];
            StackConsumes = stackConsumes ?? new StackConsume[0];
        }
    }
}

