using Effectio.Entities;

namespace Effectio.Common
{
    /// <summary>
    /// Asks an engine how long something has left to run.
    ///
    /// Both engines have always counted this - a status and a timed effect each hold a
    /// remaining-duration field they decrement every tick - and neither would tell anyone.
    /// A consumer could learn that a status was present and how many stacks it had, and not
    /// how long it would stay, which is the one number an interface needs to draw it: every
    /// buff bar in every game is a fraction of time remaining.
    ///
    /// Without it the only recourse was to shadow the clock outside the engine, listening for
    /// applied and expired and counting down in parallel. That copy is wrong the moment
    /// anything refreshes a duration, and the engine refreshes durations as its normal
    /// behaviour - so the shadow drifts, silently, exactly when the status is being used most.
    ///
    /// Kept as its own interface rather than added to <see cref="Statuses.IStatusEngine"/> and
    /// <see cref="Effects.IEffectsEngine"/>, for the same reason
    /// <see cref="Statuses.IStackOperations"/> is: a v1.0 consumer who implemented either of
    /// those directly keeps compiling. Ask for it with a type check.
    /// </summary>
    public interface IRemainingDuration
    {
        /// <summary>
        /// Seconds left on <paramref name="key"/> for <paramref name="entity"/>.
        ///
        /// Three answers, and they are distinguishable on purpose:
        /// <list type="bullet">
        /// <item><description>a positive number - that many seconds remain;</description></item>
        /// <item><description><c>-1</c> - present and permanent, matching the convention
        /// <see cref="Statuses.IStatus.Duration"/> already uses;</description></item>
        /// <item><description><c>0</c> - not present at all.</description></item>
        /// </list>
        ///
        /// Zero is unambiguous because a thing whose remaining duration reaches zero is
        /// removed in the same tick: nothing is ever present with none left. A caller drawing
        /// a timer can therefore treat zero as "draw nothing" without a second lookup.
        /// </summary>
        float GetRemainingDuration(IEffectioEntity entity, string key);
    }
}
