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
        /// <b>The v1.x stack-expiration contract, stated once.</b> Everything else that
        /// touches stacks points here rather than restating it, so there is one description
        /// to keep true.
        ///
        /// <list type="number">
        /// <item><description>All stacks share <em>one</em> combined remaining duration.
        /// There is no per-stack timer.</description></item>
        /// <item><description>Every successful <see cref="IStatusEngine.ApplyStatus"/> resets
        /// that counter to <see cref="Duration"/> - including calls made at
        /// <see cref="MaxStacks"/>, which refresh the timer without changing the
        /// count.</description></item>
        /// <item><description><see cref="IStatusEngine.Tick"/> decrements it uniformly, one
        /// expiry event per call whatever the delta.</description></item>
        /// <item><description>What happens when it reaches zero is
        /// <see cref="StackDecay"/>'s to say: under <see cref="Statuses.StackDecay.All"/> the
        /// whole status is removed and <see cref="IStatusEngine.OnStatusExpired"/> fires
        /// <em>once</em>, not once per stack; under <see cref="Statuses.StackDecay.One"/> a
        /// single stack is dropped, the counter is reset, and the status survives until the
        /// last one goes.</description></item>
        /// <item><description><see cref="IStackOperations.RemoveStacks"/> never touches the
        /// duration. Remaining stacks keep whatever was left.</description></item>
        /// </list>
        ///
        /// A future v2 may give each stack its own expiry; this behaviour stays selectable.
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

        /// <summary>
        /// What happens to the stacks when <see cref="Duration"/> runs out. See
        /// <see cref="Statuses.StackDecay"/> for what the choice means; defaults to
        /// <see cref="Statuses.StackDecay.All"/>, which is the v1.0 behaviour.
        /// </summary>
        StackDecay StackDecay { get; }

        /// <summary>
        /// Whether <see cref="OnTickEffects"/> fire once per stack instead of once per tick.
        ///
        /// The reason to have stacks at all. Three stacks of a bleed that tick for the same
        /// one point as a single stack are three of nothing: the number goes up and the
        /// damage does not, and the player learns that stacking is decoration.
        ///
        /// Scaling is repetition rather than multiplication - the effects run <c>n</c> times,
        /// exactly as if the status had ticked <c>n</c> times in one instant. That is the only
        /// meaning available in general, because an effect's action is arbitrary: a value can
        /// be multiplied, but "apply this status" and "remove that modifier" cannot, and the
        /// engine has no way to tell which it is holding. Anything needing a different shape
        /// writes an <see cref="Effectio.Effects.Actions.IEffectAction"/> that reads the stack
        /// count itself.
        ///
        /// All-or-nothing per status rather than per effect. A status whose ticks scale is a
        /// status; one that wants half its ticks scaled and half not is describing two
        /// statuses, or one custom action.
        /// </summary>
        bool TickScalesWithStacks { get; }
    }
}

