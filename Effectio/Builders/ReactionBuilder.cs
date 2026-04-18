using System.Collections.Generic;
using Effectio.Reactions;

namespace Effectio.Builders
{
    /// <summary>
    /// Fluent builder for <see cref="IReaction"/>.
    /// </summary>
    public class ReactionBuilder
    {
        private readonly string _key;
        private readonly List<string> _requiredStatusKeys = new List<string>();
        private readonly List<string> _requiredTags = new List<string>();
        private bool _consumesStatuses = true;
        private readonly List<IReactionResult> _results = new List<IReactionResult>();
        private int _priority = 0;
        private readonly List<StackRequirement> _requiredStacks = new List<StackRequirement>();
        private readonly List<StackConsume> _stackConsumes = new List<StackConsume>();

        public ReactionBuilder(string key)
        {
            _key = key;
        }

        /// <summary>
        /// Sets the priority tier. Higher tiers fire first and their consumed statuses
        /// are removed before lower tiers re-evaluate, so a high-priority reaction can
        /// preempt lower-priority reactions whose required statuses overlap. Reactions
        /// sharing a priority fire simultaneously. Default is 0.
        /// </summary>
        public ReactionBuilder Priority(int priority) { _priority = priority; return this; }

        public static ReactionBuilder Create(string key) => new ReactionBuilder(key);

        public ReactionBuilder RequireStatus(string statusKey) { _requiredStatusKeys.Add(statusKey); return this; }
        public ReactionBuilder RequireStatuses(params string[] statusKeys)
        {
            if (statusKeys != null) _requiredStatusKeys.AddRange(statusKeys);
            return this;
        }

        public ReactionBuilder RequireTag(string tag) { _requiredTags.Add(tag); return this; }
        public ReactionBuilder RequireTags(params string[] tags)
        {
            if (tags != null) _requiredTags.AddRange(tags);
            return this;
        }

        public ReactionBuilder ConsumesStatuses(bool value = true) { _consumesStatuses = value; return this; }
        public ReactionBuilder Persists() { _consumesStatuses = false; return this; }

        /// <summary>
        /// Requires the entity to have at least <paramref name="minStacks"/> stacks of
        /// <paramref name="statusKey"/> for this reaction to match. Combines with
        /// <see cref="RequireStatus(string)"/> calls into an AND-group: every
        /// required status key must be present and every required stack threshold
        /// must be met. <see cref="RequireTag(string)"/> calls remain an OR-fallback
        /// (matched only if the status/stack AND-group does not succeed) - this is the
        /// v1.0 matching shape, preserved for compatibility. Default behaviour without
        /// any <c>RequireStacks</c> call is "any stack count is fine" (presence-only
        /// check via <see cref="RequireStatus(string)"/>).
        /// </summary>
        /// <remarks>
        /// <c>RequireStacks(key, N)</c> implies presence of <paramref name="statusKey"/>
        /// (any count >= 1 means the status is present), so callers do not also need to
        /// add <c>RequireStatus(key)</c> for the same key.
        /// </remarks>
        public ReactionBuilder RequireStacks(string statusKey, int minStacks)
        {
            _requiredStacks.Add(new StackRequirement(statusKey, minStacks));
            return this;
        }

        /// <summary>
        /// On fire, decrement <paramref name="statusKey"/>'s stacks by
        /// <paramref name="count"/> instead of removing the whole status.
        /// If the resulting count would be &lt;= 0 the status is removed entirely.
        /// Per-key stack consumes take precedence over <see cref="ConsumesStatuses(bool)"/>
        /// for the keys they cover; keys not listed here fall back to that flag.
        /// </summary>
        /// <example>
        /// <code>
        /// // Burning + Wet -> Inferno reaction. Requires only 1 Burning stack to fire,
        /// // consumes 1 stack per fire, persists the rest of its required statuses.
        /// // Result: if entity has Burning x5 + Wet, the reaction fires up to
        /// // MaxChainDepth times in one CheckReactions call (5 -> 4 -> 3 -> 2 -> 1),
        /// // applying Inferno each time, until either Burning is exhausted or the
        /// // chain depth limit is reached.
        /// ReactionBuilder.Create("Inferno")
        ///     .RequireStacks("Burning", 1)
        ///     .RequireStatus("Wet")
        ///     .ConsumesStacks("Burning", 1)
        ///     .Persists() // do NOT remove Wet whole-status; chain depends on it surviving
        ///     .ApplyStatus("Inferno")
        ///     .Build();
        /// </code>
        /// </example>
        public ReactionBuilder ConsumesStacks(string statusKey, int count)
        {
            _stackConsumes.Add(new StackConsume(statusKey, count));
            return this;
        }

        public ReactionBuilder ApplyStatus(string statusKey)
        {
            _results.Add(new ReactionResult(ReactionResultType.ApplyStatus, statusKey));
            return this;
        }

        public ReactionBuilder RemoveStatus(string statusKey)
        {
            _results.Add(new ReactionResult(ReactionResultType.RemoveStatus, statusKey));
            return this;
        }

        public ReactionBuilder AdjustStat(string statKey, float value)
        {
            _results.Add(new ReactionResult(ReactionResultType.AdjustStat, statKey, value));
            return this;
        }

        public ReactionBuilder ApplyEffect(string effectKey)
        {
            _results.Add(new ReactionResult(ReactionResultType.ApplyEffect, effectKey));
            return this;
        }

        public ReactionBuilder Custom(string customKey, float value = 0f)
        {
            _results.Add(new ReactionResult(ReactionResultType.Custom, customKey, value));
            return this;
        }

        /// <summary>Adds a user-supplied <see cref="IReactionResult"/>.</summary>
        public ReactionBuilder WithResult(IReactionResult result)
        {
            if (result != null) _results.Add(result);
            return this;
        }

        public IReaction Build() => new Reaction(
            _key,
            _requiredStatusKeys.ToArray(),
            _requiredTags.ToArray(),
            _consumesStatuses,
            _results.ToArray(),
            _priority,
            _requiredStacks.Count > 0 ? _requiredStacks.ToArray() : null,
            _stackConsumes.Count > 0 ? _stackConsumes.ToArray() : null);
    }
}
