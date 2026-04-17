# Changelog

All notable changes to Effectio are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

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

### Backwards compatibility

- v1.0 source and binary surfaces are preserved. `IReaction` is unchanged,
  so existing implementations compile and load against v1.1 unmodified. The
  v1.0 5-parameter `Reaction(...)` constructor is kept as a distinct
  overload (it delegates to the new 6-parameter form with `priority: 0`),
  so pre-built v1.0 consumers do not hit `MissingMethodException`.
  Regression tests cover both paths.

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
