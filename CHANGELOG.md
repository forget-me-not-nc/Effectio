# Changelog

All notable changes to Effectio are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

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
