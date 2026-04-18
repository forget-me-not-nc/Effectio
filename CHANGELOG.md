# Changelog

All notable changes to Effectio are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

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

- `ReactionBuilder.ApplyEffect(string)` (and `ReactionResult` of type
  `ApplyEffect`) now actually apply the named effect when the reaction
  fires. Pre-v1.1 the reaction engine's `OnApplyEffect` callback was never
  wired by `EffectioManager`, so this result type was a silent no-op.
  `EffectioManager` now resolves the key through `IEffectCatalog` and applies
  the resulting effect; an unknown key is logged as a warning and skipped
  (other results in the same reaction still execute).

### Backwards compatibility

- v1.0 source and binary surfaces are preserved. `IReaction`, `IEffectsEngine`,
  `IStatusEngine` and `IEffectioManager` are unchanged, so existing
  implementations compile and load against v1.1 unmodified. New surfaces
  (`IPrioritizedReaction`, `IStackAwareReaction`, `IEffectCatalog`,
  `IStackOperations`, `EffectioManager.EffectCatalog`) are additive.
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
