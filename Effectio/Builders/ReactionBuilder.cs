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

        public ReactionBuilder(string key)
        {
            _key = key;
        }

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

        public IReaction Build() => new Reaction(
            _key,
            _requiredStatusKeys.ToArray(),
            _requiredTags.ToArray(),
            _consumesStatuses,
            _results.ToArray());
    }
}
