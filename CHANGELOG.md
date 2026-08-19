# Changelog

All notable changes to Effectio are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- **`IEffectioEntity.GetStatusStackCount(string)`** ergonomic shortcut for
  `manager.Statuses.GetStacks(entity, key)`. The built-in `EffectioEntity`
  queries through a status-engine reference wired at construction;
  `EffectioManager.CreateEntity` passes that reference automatically so
  the shortcut "just works" for the 99% path. Manually-constructed
  entities using the v1.0 single-arg ctor return 0 (documented fallback);
  the new 2-arg ctor `EffectioEntity(string id, IStatusEngine engine)`
  opts in to real stack queries. Roadmap task v1.1 #5.
- **Stacking semantics: `StackDecay` and `TicksPerStack`.** The two questions
  a stacked status has to answer, and until now it answered both by accident.

  `IStatus.StackDecay` chooses how the stacks go when the timer runs out.
  `StackDecay.All` is v1.0 behaviour and the default - one timer, everything
  ends together. `StackDecay.One` (`StatusBuilder.DecayOneAtATime()`) drops a
  single stack and winds the timer back up, removing the status when the last
  one goes. The difference is what stacking feels like: `All` makes stacks a
  state you hold, where five and one end at the same moment and the climb to
  five bought nothing lasting; `One` makes them pressure that drains, hard to
  build and slow to lose. Still one timer, not one per stack - per-stack
  expiry differs only when stacks arrived at uneven intervals, costs storage
  per stack, and turns "how long is this left" into a question with several
  answers. It remains a v2 candidate.

  `IStatus.TickScalesWithStacks` (`StatusBuilder.TicksPerStack()`) fires
  `OnTickEffects` once per stack instead of once per tick, so three stacks of
  a bleed cost three times as much. Repetition rather than multiplication: an
  effect's action is arbitrary, so a value can be scaled but "apply that
  status" cannot, and the engine cannot tell which it holds. All-or-nothing
  per status rather than per effect - a status wanting half its ticks scaled
  is describing two statuses. Roadmap task v1.1 #9.

  A status that loses a stack to decay still ticks that second: it lost a
  stack, it did not end, and skipping the tick would be a hole in the numbers
  nobody could see. One stack per `Tick` call whatever the delta, matching the
  one-expiry-per-call rule the engine already follows everywhere else.

- **`EffectActionContext.SourceStatusKey`** - the status an effect is running
  on behalf of, or `null` when it was applied directly. Everything needed to
  scale an action by stack count was already in the context; what was missing
  was which key had caused this particular execution, so an action could only
  ask about a status named in its own constructor - making a
  scale-by-my-stacks action one class per status instead of one class. Carried
  on the active-effect entry rather than passed at execution time, because a
  periodic effect ticks long after whoever applied it has returned. Applied
  through a new `EffectsEngine.ApplyEffect(entity, effect, sourceStatusKey)`
  overload on the class rather than a new member on `IEffectsEngine`.

- **`IRemainingDuration`** (`Effectio.Common`), implemented by both
  `StatusEngine` and `EffectsEngine`. `GetRemainingDuration(entity, key)`
  returns seconds left, `-1` for permanent (matching `IStatus.Duration`'s
  convention) and `0` for absent - unambiguous, because anything reaching
  zero is removed in the same tick, so nothing is ever present with none
  left. Both engines had always counted this and neither would tell anyone:
  a consumer could learn that a status was present and how many stacks it
  had, but not how long it would stay, which is the one number a buff timer
  is made of. The only recourse was to shadow the clock outside the engine,
  and that copy is wrong the moment anything refreshes a duration - which
  `ApplyStatus` does as its normal behaviour. Kept as its own interface for
  the same reason `IStackOperations` is: a v1.0 consumer who implemented
  `IStatusEngine` or `IEffectsEngine` directly keeps compiling, and asks for
  this with a type check.

- **`StatusBuilder.OnRefresh(IEffect)`** + **`IStatus.OnRefreshEffects`** +
  **`IStatusEngine.OnStatusRefreshed`** event. Effects fire every time
  `ApplyStatus` is called against an entity that already has the status,
  whether the stack counter increments or is already at `MaxStacks`.
  Distinct from `IStackOperations.OnStatusStacked` (which fires only on
  counter changes); `OnStatusRefreshed` fires on both stack-increment AND
  at-max refresh paths because the combined `RemainingDuration` refreshes
  in both. Does NOT fire on first application or on `RemoveStacks` partial
  decrement. Useful for "stand-in-flame" patterns where each re-application
  bursts damage. Roadmap task v1.1 #7.
- **Stack-aware reactions** via the new `IStackAwareReaction` interface and
  the `ReactionBuilder.RequireStacks(string, int)` and
  `ReactionBuilder.ConsumesStacks(string, int)` fluent methods. Reactions
  can now gate on minimum stack counts (e.g. "fire only when Burning has
  at least 3 stacks") and decrement specific stack counts on fire instead
  of removing the whole status. Per-key stack consumes take precedence
  over `ConsumesStatuses(true)`; keys not listed fall back to the v1.0
  flag. Reactions that do not implement `IStackAwareReaction` behave
  exactly as in v1.0. Roadmap task v1.1 #1.
- **`IStackOperations` interface** exposing
  `RemoveStacks(IEffectioEntity, string, int)` and the
  `OnStatusStacked` event. Implemented by `StatusEngine` alongside
  `IStatusEngine` (which is unchanged for binary compatibility).
  `OnStatusStacked` fires whenever a status's stack counter changes
  without the status being newly applied or fully removed - e.g. on
  `ApplyStatus` against an existing status, or on a partial `RemoveStacks`.
  Does NOT fire when `ApplyStatus` is called at `MaxStacks` (no counter change).
- **Reaction-check on stack changes.** `EffectioManager` now subscribes to
  `OnStatusStacked` so stack-aware reactions re-evaluate as stacks
  accumulate. Stack-change notifications do NOT replay a status's
  `OnApplyEffects` (those fire once per status birth, not per refresh).
- **Effect catalog** via the new `IEffectCatalog` interface and
  `EffectioManager.EffectCatalog` property (`RegisterEffect(IEffect)`,
  `TryGetEffect(string, out IEffect)`, `RegisteredEffects`). The built-in
  `EffectsEngine` implements both `IEffectsEngine` (unchanged) and
  `IEffectCatalog`. Roadmap task v1.1 #4.
- **Reaction priority** via the new `IPrioritizedReaction` interface and
  `ReactionBuilder.Priority(int)`. Higher-priority reactions fire first, and
  their consumed statuses are removed before lower-priority reactions
  re-evaluate, so a high-priority reaction can preempt overlapping
  low-priority ones in the same tick. Reactions sharing a priority preserve
  v1.0 "fire simultaneously" semantics. Roadmap task v1.1 #3.
- **`IPrioritizedReaction : IReaction`** as a separate opt-in interface
  exposing `int Priority`. The built-in `Reaction` class implements it
  transparently. Reactions that implement only `IReaction` (including any
  v1.0 external implementations) are treated as priority 0, identical to
  v1.0 behaviour.

### Fixed

- **A tick can now create entities and stats.** Both loops that drive a frame
  were walking a live collection while running consumer code, and the most
  ordinary things that code does are to make something or kill something.

  `EffectioManager.Tick` enumerated the entity dictionary directly, so an
  effect action that spawned - a death that summons, a slime that splits, a
  tick that calls for help - threw `InvalidOperationException` on the next
  step. Removal happened to work, because .NET Core permits `Remove` during
  enumeration and not `Add`, which is worse than either being consistently
  broken: it teaches you the pattern is safe. Entities are now copied into a
  reused list before any of their code runs, so the tick stays
  allocation-free after its first call. An entity removed mid-tick stops being
  ticked for the rest of that frame, and an id re-registered mid-tick does not
  tick the object it replaced. A newly created entity starts on the next tick.

  `EffectioEntity.TickStatModifiers` had the same shape one level down: a
  modifier expiring recalculates its stat, which raises `OnValueChanged`,
  which is consumer code free to call `AddStat`. Stats are now ticked over a
  flat array rebuilt on `AddStat` rather than walked from the dictionary -
  faster on the hottest loop in the library, and a stat added part way through
  swaps the field without disturbing the loop in flight.

- **`IStatus.OnRemoveEffects` now fire when a status is removed deliberately,
  not only when it expires.** The property has always been documented as
  firing on "manual remove or expiration" and only ever did the second:
  `EffectioManager` fired them from its expiration loop and never subscribed
  to `OnStatusRemoved`. A status dispelled, cleansed, or dropped by a reaction
  consuming its last stack skipped its own farewell entirely, so anything an
  author put there to clean up after itself simply did not run. Expiration
  still goes through the tick loop, so there is no path on which both fire.

- **`IStat.BaseValue` is now held inside the stat's own `Min` / `Max`.**
  Previously only `CurrentValue` was clamped, so a base driven past a limit
  kept going: forty ticks of `AdjustStat("Health", -5)` on a stat floored at
  zero left `BaseValue` at `-100` while `CurrentValue` sat correctly at `0`.
  Nothing looked wrong at that moment - but the next heal had a hundred
  points of invisible debt to clear before anything moved, so a player could
  heal for fifty and see no change. The mirror case applied at the ceiling:
  repeated overheal banked surplus that later damage silently spent.

  `BaseValue`, `Min` and `Max` are now backed fields; assigning any of them
  re-clamps the base, and the constructor assigns the bounds before the base
  so it is clamped against the right pair. Bounded by the stat's own limits
  and not by the effective ones a modifier computes - raising a ceiling is
  what `CapAdjustmentModifier` is for, and a temporary floor must not
  permanently heal what it was protecting. Locked with
  `BaseValueBoundsTests` (7 cases, including the failure by the route a game
  actually takes: a periodic effect through `EffectioManager`).

- `ReactionBuilder.ApplyEffect(string)` (and `ReactionResult` of type
  `ApplyEffect`) now actually apply the named effect when the reaction
  fires. Pre-v1.1 the reaction engine's `OnApplyEffect` callback was never
  wired by `EffectioManager`, so this result type was a silent no-op.
  `EffectioManager` now resolves the key through `IEffectCatalog` and applies
  the resulting effect; an unknown key is logged as a warning and skipped
  (other results in the same reaction still execute).

### Backwards compatibility

- v1.0 source and binary surfaces are preserved at the `IReaction` /
  `IEffectsEngine` / `IEffectioManager` level for the v1.0 method shapes.
  Three interfaces do grow a member in v1.1, which is technically a binary
  break for anyone who implemented them externally:

  - `IEffectioEntity.GetStatusStackCount(string)` - external implementers
    return whatever stack source they own, or 0 if they track none. The
    built-in `EffectioEntity` adds it transparently and keeps its single-arg
    ctor as a delegating overload to a new 2-arg ctor taking the status
    engine.
  - `IStatusEngine.OnStatusRefreshed` - engine plumbing rather than
    user-extension surface, so the realistic population of external
    implementers is zero.
  - `IStatus.OnRefreshEffects`, `IStatus.StackDecay` and
    `IStatus.TickScalesWithStacks` - external implementations add the three;
    `null` / `StackDecay.All` / `false` reproduce v1.0 behaviour exactly, so
    saying so costs three lines and changes nothing. `OnRefreshEffects` may be
    `null` or `Array.Empty`, the manager null-guards. The built-in `Status`
    keeps its v1.0 8-parameter ctor as a delegating overload, and its v1.1
    9-parameter one as another.
  Other new surfaces (`IPrioritizedReaction`, `IStackAwareReaction`,
  `IEffectCatalog`, `IStackOperations`, `EffectioManager.EffectCatalog`)
  are pure additions on new opt-in interfaces.
  The v1.0 5-parameter `Reaction(...)` constructor and the v1.1-preview
  6-parameter overload are both kept as distinct ctors (delegating to the
  new 8-parameter form with empty stack arrays / priority 0), so pre-built
  consumers do not hit `MissingMethodException`. Regression tests cover all
  three ctor paths and external `IReaction` implementations.

### Apply-spam contract

- `IStatusEngine.ApplyStatus` is idempotent at `MaxStacks` (the counter
  does not grow further) but still fires `OnStatusApplied` (when the
  status is newly applied) or `OnStatusStacked` (when the counter
  actually increments). It does NOT fire either event when called against
  a status already at `MaxStacks` - the counter does not change in that
  path; only the duration refreshes. **Callers should still debounce
  their own application loops** - typical pattern: aura systems should
  re-apply per `Tick(deltaTime)`, not per Update/frame. Calling
  `ApplyStatus` 1000x against a not-yet-maxed status between ticks costs
  1000x the reaction-check work. A v1.2 candidate adds an opt-in
  per-tick debounce mode (see roadmap).

### Stack-expiration contract

- All stacks of a status share **one combined `RemainingDuration`**.
  Each successful `IStatusEngine.ApplyStatus` resets it to
  `IStatus.Duration` (this is true even at `MaxStacks` - the counter
  does not increment but the duration still refreshes).
  `IStatusEngine.Tick` decrements the combined duration uniformly.
  When the duration reaches zero, the entire status is removed and
  `OnStatusExpired` fires **once**, not once per stack.
- `IStackOperations.RemoveStacks` decrements the stack counter without
  touching `RemainingDuration`; remaining stacks expire on the original
  in-flight timer.
- `IStatus.OnTickEffects` fire **once per tick per status**, regardless
  of stack count. Per-stack tick scaling (`OnTick(...).PerStack()`) is
  a v1.2 candidate.
- `Duration = -1` means permanent. Such statuses never expire regardless
  of stack count.
- This contract is locked down by `StackExpirationTests` (6 tests).
  A future v2 release may distinguish individual stacks (each with its
  own expiration); the v1.x combined-counter behaviour will remain
  selectable / opt-in. Roadmap task v1.1 #6.

### Performance

- `ReactionEngine` now keeps `_reactions` sorted by priority on register (stable
  insertion sort, preserves registration order for ties). `CheckReactions` walks
  the sorted list once per pass, grouping consecutive equal-priority entries
  into tiers. Total work is O(R) per pass regardless of how many distinct
  priorities are in use.
- New `Effectio.Benchmarks.ReactionPriorityBenchmark` covers `AllDefault`,
  `TwoTiers`, and `ManyTiers` priority shapes at 10/50/100 reactions. Reference
  numbers on a Coffee Lake i7-9700K, .NET 8: 100 reactions across 100 distinct
  priorities tick in ~12 us, matching 100 reactions all at default priority
  (i.e. priority is free at typical scale; 0 B allocated per call).
- `ReactionEngine.CheckReactions` no longer allocates 40 B per call from
  `HashSet<T>.ExceptWith(IEnumerable<T>)` boxing the chain-detection diff's
  argument enumerator. Replaced with a manual `foreach` over the concrete
  `HashSet<string>` (struct enumerator, zero box). Reference numbers: the
  smallest realistic `CheckReactions` call (one matching reaction, two chain
  passes) drops from 170 ns / 40 B to 148 ns / 0 B; the priority benchmark
  matrix at 100 reactions also drops to 0 B per op across every shape.
- New `Effectio.Benchmarks.ReactionAllocationDiagnostic` keeps three
  baselines (`NoOp`, `EarlyExit`, `OneTrivialReaction`) as a permanent
  regression guard against allocation creep in the engine's hot path.

## [1.0.0] - 2026-04-17

Initial public release.

### Core simulation

- `EffectioManager` facade with a single `Tick(deltaTime)` entry point.
- Per-entity stats (`IStat` / `Stat`) with priority-ordered modifier pipeline:
  additive (P=100), multiplicative (P=200), cap-adjustment (P=300), clamp.
  Any custom `ModifierBase` subclass plugs in with its own priority.
- Five effect lifecycle kinds: `Instant`, `Timed`, `Periodic`, `Aura`
  (auto-undo on removal / expiration), `Triggered`.
- Polymorphic `IEffectAction` with built-ins for adjust-stat, apply /
  remove modifier (any `IModifier` kind via factory), apply / remove
  status, and user-supplied actions.
- Polymorphic `ITriggerCondition` with `StatBelow`, `StatAbove`,
  `HasStatus`, `LacksStatus`, and composite `And` / `Or` / `Not` built-ins.
- Statuses (`IStatus` / `Status`) with tags, duration, stacking, tick
  interval, on-apply / on-tick / on-remove effects, and immunity support.
- Reactions (`IReaction`) fire when required statuses or tags coexist;
  polymorphic `IReactionResult` kinds; reaction chaining up to
  `ReactionEngine.MaxChainDepth`.

### Authoring

- Fluent builders for every kind: `ModifierBuilder`, `EffectBuilder`,
  `StatusBuilder`, `ReactionBuilder`.

### Performance

- Hot-path buffers pooled on `StatusEngine` and `ReactionEngine`.
- `IEffectioLogger.IsEnabled` gate eliminates interpolated-string
  allocations when logging is disabled.
- `IEffectioEntity.TickStatModifiers` / `CopyStatusKeysTo` sidestep
  `IReadOnlyCollection<T>` enumerator boxing on the hot path.
- Steady-state `EffectioManager.Tick` allocates zero bytes for 1000
  entities with mixed DoT / timed / aura / triggered / status workload.

### Tests & benchmarks

- 78 MSTest cases covering stats, modifiers, each effect type, statuses,
  reactions, triggers, complex multi-system scenarios, and builders.
- `Effectio.Benchmarks` BenchmarkDotNet project measuring
  `Stat.Recalculate`, `Stat.TickModifiers`, `ReactionEngine.CheckReactions`,
  and end-to-end `EffectioManager.Tick`.

### Packaging

- NuGet: `Effectio` (netstandard2.0, zero dependencies).
- UPM: `com.forget-me-not-nc.effectio` published from the `Effectio/`
  subfolder of this repository.
- SourceLink enabled so consumers debug straight into GitHub source.

[1.0.0]: https://github.com/forget-me-not-nc/Effectio/releases/tag/v1.0.0
