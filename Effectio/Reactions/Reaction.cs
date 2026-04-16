namespace Effectio.Reactions
{
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
    }

    public class Reaction : IReaction
    {
        public string Key { get; }
        public string[] RequiredStatusKeys { get; }
        public string[] RequiredTags { get; }
        public bool ConsumesStatuses { get; }
        public IReactionResult[] Results { get; }

        public Reaction(
            string key,
            string[] requiredStatusKeys = null,
            string[] requiredTags = null,
            bool consumesStatuses = true,
            IReactionResult[] results = null)
        {
            Key = key;
            RequiredStatusKeys = requiredStatusKeys ?? new string[0];
            RequiredTags = requiredTags ?? new string[0];
            ConsumesStatuses = consumesStatuses;
            Results = results ?? new IReactionResult[0];
        }
    }
}
