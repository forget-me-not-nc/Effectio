# Roadmap

A living list of work proposed for upcoming releases. Each row in the
tables below becomes a GitHub issue when its milestone is opened. Sizes:
**XS** ~1 h, **S** ~half day, **M** ~1-2 days, **L** ~3+ days.

> **About the IDs.** Task numbers (`#1`, `#2`, ...) reset inside each
> milestone section and are issue-tracker IDs, not release tags. The
> section heading is the release tag (`v1.1.0`, `v1.2.0`, ...). Every
> task in a section ships together under that one tag.

> **About SemVer.** `MAJOR.MINOR.PATCH` - bump MAJOR for breaking API
> changes, MINOR for new backwards-compatible features, PATCH for bug
> fixes only. Effectio's milestones are MINOR releases; a `v2.0.0` will
> only happen when something must break to move forward. Standalone bug
> fixes between milestones may ship as `v1.0.1`, `v1.0.2`, ...

## v1.1.0 - "Stacks, properly"

Closes the design gaps that the v1.0 sample surfaced: stack-aware
reactions, stack-scaled effect actions, and the small core tweaks that
come with them. Ships as one `v1.1.0` tag once every task in this
section lands.

| # | Task | Why | Size |
|---|---|---|---|
| 1 | ~~**Stack-aware reactions.** `RequireStacks(key, min)`, `ConsumesStacks(key, count)`, `ScaleResultsByMinStackCount()`.~~ **Shipped trimmed:** `RequireStacks(key, min)` and `ConsumesStacks(key, count)` only. `ScaleResultsByMinStackCount` deferred into v1.2 task #4 ("Scaling and callbacks design") - the right shape is a richer action / callback vocabulary, not a numeric multiplier on existing results. | Lets "3 stacks of Burning -> Inferno" be expressible declaratively. Stack-scaled damage is achievable today via a custom `IReactionResult` that reads `ctx.StatusEngine.GetStacks(...)`. | M |
| 2 | ~~**Stack-scaled effect actions.** `EffectBuilder.AdjustStat("Health", -5f).PerStackOf("Burning")`.~~ **Deferred to v1.2 #4** (same scaling-and-callbacks rethink). | Removes the need to write a custom `IEffectAction` for `Bleeding x3 = -3 HP/s`. | S |
| 3 | **Reaction priority.** Explicit `.Priority(int)` so registration order does not silently determine which reaction wins. | Today the 5-condition Apocalypse only fires if registered before its 2-condition subsets. Easy to get wrong. | S |
| 4 | **Effect catalog + wire `OnApplyEffect`.** `manager.Effects.RegisterEffect(IEffect)`; `ReactionBuilder.ApplyEffect(string)` resolves through the catalog. | Closes the bug found designing Overload (`ApplyEffect(string)` is currently a no-op from reactions). | M |
| 5 | **`entity.GetStatusStackCount(key)` shortcut.** | Today consumers have to reach into `manager.Statuses.GetStacks(...)`. Pure ergonomics. | XS |
| 6 | **Per-stack expiration semantics: test + docs.** Decide whether stacks expire all-at-once or one-at-a-time, lock with tests, document. | Current behaviour is an implementation detail. | S |
| 7 | ~~**`StatusBuilder.OnRefresh(IEffect)`.** Fire an effect when a status is re-applied while still active.~~ **Shipped:** `StatusBuilder.OnRefresh(IEffect)` + `IStatus.OnRefreshEffects` + `IStatusEngine.OnStatusRefreshed` event. Fires for both stack-increment AND at-MaxStacks paths (whenever `RemainingDuration` refreshes). Distinct from `OnStatusStacked` (which only fires on counter changes). | Useful for "stacking refresh also ticks burst damage" patterns. | S |
| 8 | **`Conditional` effect action.** `.WhenStatBelow(stat, value, then, else)` without writing a custom action. | Sugar for "heal if low HP, otherwise damage". | S |
| 9 | **Stack-aware ticks.** `StatusBuilder.OnTick(effect).PerStack()` so `Bleeding x3` ticks 3 x -1 HP. | Companion to task #2; status-side counterpart. | S |
| 10 | **README: registration-order rule + reaction priority.** Document the gotcha until task #3 ships. | Cheap, prevents foot-guns for early users. | XS |
| 11 | **Comprehensive benchmark matrix.** Cross-product of {10, 50, 100, 500} entities x {10, 50, 100} stats x {10, 50, 100} effects x {10, 50, 100} reactions. Measure CPU time per `Tick`, total managed allocations, peak working set, and per-tick budget headroom (does processing complete inside a 16 ms / 60 Hz frame budget at each scale?). Output baseline numbers so future PRs catch perf regressions. | Today's benchmarks are per-engine micro-benches; we have no end-to-end tick budget data at realistic game scale. | L |

## v1.2.0 - "Authoring"

Optional packages + sugar that make it easy to author content outside
C#. Ships as one `v1.2.0` tag after the v1.1.0 milestone closes.

| # | Task | Why | Size |
|---|---|---|---|
| 1 | **`Effectio.Serialization.Json`** package: DTO records + `System.Text.Json` loader for effects, statuses, reactions. | Lets games author content as JSON without dragging the dependency into the core. | L |
| 2 | **`Effectio.Unity`** package: `EffectioWorldBehaviour`, `CharacterStatsBehaviour`, `ScriptableObject` wrappers mirroring the DTOs. | Pulls the boilerplate out of every Unity consumer's project. | L |
| 3 | **`Effectio.Unity` sample:** convert `samples/UnityDemo` to use the ScriptableObject authoring path. | Validates the authoring story end-to-end. | M |
| 4 | **Scaling and callbacks design.** Replaces deferred v1.1 #1 `ScaleResultsByMinStackCount` and v1.1 #2 `PerStackOf`. Design a coherent extensibility surface for "on satisfied condition, do X" - user callbacks, custom result/action types that can introspect the trigger context (stack counts, matched statuses, etc.), threshold hooks. | Numeric-multiplier scaling is too narrow. The honest shape is a richer action vocabulary, but it needs design work that did not fit in the v1.1 schedule. | L |
| 5 | **Optional per-tick `OnStatusApplied` / `OnStatusStacked` debouncing.** A configurable mode where each event fires at most once per (entity, statusKey) per `Tick`. Off by default to preserve current semantics. | Shields callers from accidental tight-loop application costs (e.g. an aura applying status every Update instead of every Tick). Needs a clean tick-boundary definition. | M |

## Backlog (no milestone yet)

- Comparison vs. Unity GAS / Gameplay Ability System in README + a small port-from-GAS guide.
- Determinism mode (fixed-point arithmetic for lockstep multiplayer).
- Snapshot / replay support: serialise the full simulation state for save-load and rewind.
- Source generators for stat / status / reaction key constants (no more magic strings).
- Optional Unity Editor inspector for `EffectioWorldBehaviour` showing live status / modifier breakdown.
- **Lifecycle polymorphism for `IEffect`** (v2.0 candidate, breaking).
Replaces the `EffectType` enum + multi-place switches in `EffectsEngine`
(`ApplyEffect`, `Tick`, `ProcessPendingTicks`, `RemoveEffect`) with one
`IEffectLifecycle` implementation per variant (Instant, Timed, Periodic,
Aura, Triggered). Adding a new effect kind becomes one new class instead
of editing four switch statements. Breaking because `IEffect` gains a
`Lifecycle` member.


