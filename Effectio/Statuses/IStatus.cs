using Effectio.Effects;

namespace Effectio.Statuses
{
    public interface IStatus
    {
        string Key { get; }
        string[] Tags { get; }
        float Duration { get; }
        int MaxStacks { get; }
        IEffect[] OnApplyEffects { get; }
        IEffect[] OnTickEffects { get; }
        IEffect[] OnRemoveEffects { get; }

        /// <summary>
        /// Effects fired every time <see cref="IStatusEngine.ApplyStatus"/> is called
        /// against an entity that already has this status, regardless of whether the
        /// stack counter changes. Fires for both the increment-stacks path AND the
        /// at-<see cref="MaxStacks"/> refresh path (since both paths refresh the
        /// combined <c>RemainingDuration</c>). Does NOT fire on first application
        /// (use <see cref="OnApplyEffects"/>) or on partial
        /// <see cref="IStackOperations.RemoveStacks"/> decrement (which does not
        /// touch the duration). Useful for "stand-in-flame" mechanics that want a
        /// burst of damage on every re-application.
        /// </summary>
        IEffect[] OnRefreshEffects { get; }

        float TickInterval { get; }
    }
}
