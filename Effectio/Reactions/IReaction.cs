namespace Effectio.Reactions
{
    public enum ReactionResultType
    {
        RemoveStatus,
        ApplyStatus,
        ApplyEffect,
        AdjustStat,
        Custom
    }

    public interface IReactionResult
    {
        ReactionResultType Type { get; }
        string TargetKey { get; }
        float Value { get; }
    }

    public interface IReaction
    {
        string Key { get; }
        string[] RequiredStatusKeys { get; }
        string[] RequiredTags { get; }
        bool ConsumesStatuses { get; }
        IReactionResult[] Results { get; }
    }
}
