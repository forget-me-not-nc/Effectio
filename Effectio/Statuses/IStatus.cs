using Effectio.Effects;

namespace Effectio.Statuses
{
    public interface IStatus
    {
        /// <summary>Unique key the engine and consumers reference this status by.</summary>
        string Key { get; }

        /// <summary>Free-form tags used by tag-based reaction matching.</summary>
        string[] Tags { get; }

        /// <summary>
        /// Lifetime of the status in seconds. <c>-1</c> (the default) means permanent;
        /// the status never expires regardless of stack count.
        /// </summary>
        /// <remarks>
        /// <b>v1.x stack-expiration contract:</b> all stacks of a status share
        /// one combined <c>RemainingDuration</c>. Each successful
        /// <see cref="IStatusEngine.ApplyStatus"/> resets that counter to
        /// <see cref="Duration"/>; <see cref="IStatusEngine.Tick"/> decrements it
        /// uniformly; when it reaches zero the entire status is removed and
        /// <see cref="IStatusEngine.OnStatusExpired"/> fires <em>once</em>
        /// (not once per stack). <see cref="IStackOperations.RemoveStacks"/>
        /// does NOT touch the duration; remaining stacks keep the in-flight
        /// value. A future v2 release may distinguish individual stacks
        /// (each with its own expiration); the v1.x combined-counter
        /// behaviour will remain selectable / opt-in.
        /// </remarks>
        float Duration { get; }

        /// <summary>
        /// Maximum stack count for this status. Additional
        /// <see cref="IStatusEngine.ApplyStatus"/> calls at this cap do NOT
        /// increment the counter (and do NOT fire
        /// <see cref="IStackOperations.OnStatusStacked"/>) but they DO refresh
        /// the combined <c>RemainingDuration</c> to <see cref="Duration"/>.
        /// </summary>
        int MaxStacks { get; }

        /// <summary>Effects fired once when the status is first applied to an entity.</summary>
        IEffect[] OnApplyEffects { get; }

        /// <summary>
        /// Effects fired periodically (every <see cref="TickInterval"/> seconds)
        /// while the status is active. <b>v1.x:</b> these fire once per tick per
        /// status, regardless of stack count. Per-stack tick scaling
        /// (<c>OnTick(...).PerStack()</c>) is a v1.2 candidate (see roadmap).
        /// </summary>
        IEffect[] OnTickEffects { get; }

        /// <summary>Effects fired once when the status is removed (manual remove or expiration).</summary>
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

        /// <summary>Seconds between successive <see cref="OnTickEffects"/> applications.</summary>
        float TickInterval { get; }
    }
}

