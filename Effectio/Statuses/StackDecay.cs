namespace Effectio.Statuses
{
    /// <summary>
    /// How a stacked status loses its stacks when its timer runs out.
    ///
    /// The question this answers is not a technical one - both shapes are a few lines - it is
    /// what stacking is supposed to feel like, and the two feel completely different.
    ///
    /// <see cref="All"/> makes stacks a state you hold: keep applying and you keep everything,
    /// stop and the whole pile goes at once. It is the simplest thing to reason about and the
    /// easiest to be surprised by, because five stacks and one stack end at exactly the same
    /// moment - the effort of climbing to five bought nothing that lasts.
    ///
    /// <see cref="One"/> makes them pressure that drains: hard to build, slow to lose. Getting
    /// to five took five applications and losing it takes five durations, so the number on
    /// screen means something over time rather than only right now. This is what most games
    /// with visible stack counts settle on, and it is usually what somebody means when they
    /// say "three stacks should be worth more than one".
    ///
    /// Neither is correct in general. A poison the player is meant to outrun wants
    /// <see cref="All"/>; a bleed they are meant to manage wants <see cref="One"/>.
    /// </summary>
    public enum StackDecay
    {
        /// <summary>
        /// One timer for the whole status; when it runs out the status is removed entirely,
        /// however many stacks it had. The v1.0 behaviour, and the default, so no existing
        /// content changes meaning by upgrading.
        /// </summary>
        All = 0,

        /// <summary>
        /// One timer; when it runs out a single stack is dropped and the timer starts again
        /// at <see cref="IStatus.Duration"/>. The status is removed when the last stack goes.
        ///
        /// Deliberately still one timer rather than one per stack. Per-stack expiry differs
        /// from this only when stacks were applied at uneven intervals, costs storage per
        /// stack, and makes "how long is this status left" a question with several answers -
        /// which an interface then has to pick between. It remains a v2 candidate; this gets
        /// the feel for none of that cost.
        /// </summary>
        One = 1
    }
}
