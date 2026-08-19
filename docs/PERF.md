# Effectio performance reference

Reference numbers, allocation contract, and how to run the benchmarks.

## Allocation contract

Every benchmark in `Effectio.Benchmarks/` should report **0 B / op**
in steady state. This is the v1.1 promise: no per-call allocations on
the hot path. If a future change regresses any benchmark to non-zero
allocation, that's a release-blocker and should be diagnosed via
`ReactionAllocationDiagnostic` (or a sibling diagnostic added for the
affected surface).

The 0-B claim does NOT cover:
- One-time setup work (`[GlobalSetup]` blocks may allocate freely;
  not part of the per-op measurement).
- Effect / status / reaction registration (rare at runtime; happens
  during world load).
- The first-time application of a status to an entity (one
  `Dictionary` entry; amortised over the entity's lifetime, not
  measured by any of the steady-state benchmarks).

## How to run

```sh
cd Effectio.Benchmarks
dotnet run -c Release
```

That runs the entire matrix (~10-15 minutes total). To run a single
benchmark class:

```sh
dotnet run -c Release -- --filter "*StatusEngineBenchmark*"
```

For a quick smoke-test pilot (fewer iterations, less precision but
~30s instead of minutes):

```sh
dotnet run -c Release -- --filter "*Foo*" --warmupCount 2 --iterationCount 3
```

## Reference numbers

All numbers measured on Coffee Lake i7-9700K, .NET 8, Release build.
Sub-microsecond benchmarks have noise around the last 1-2 ns;
treat anything within ~5 ns as "the same".

### StatusEngineBenchmark

| Method | EntityCount | Mean | Allocated |
|---|---|---:|---:|
| `ApplyStatus_RefreshAndStack` | 128 | 36.5 ns | 0 B |
| `ApplyStatus_RefreshAndStack` | 1024 | 41.2 ns | 0 B |
| `ApplyStatus_AtMaxRefresh` | 128 | 37.8 ns | 0 B |
| `ApplyStatus_AtMaxRefresh` | 1024 | 40.8 ns | 0 B |
| `Tick` (entities x 5 statuses) | 128 | 8.4 us | 0 B |
| `Tick` (entities x 5 statuses) | 1024 | 72.7 us | 0 B |

`Tick` cost is linear per (entity, status) cell at ~13-14 ns / cell.
The two `ApplyStatus` paths cost essentially the same; the extra
`OnStatusStacked` event-check on the increment path is below the
measurement noise floor.

### StackAwareReactionBenchmark

To populate. Run:

```sh
dotnet run -c Release -- --filter "*StackAwareReactionBenchmark*"
```

Expected pattern: `Presence` (v1.0 baseline) and `StackThreshold` (v1.1)
should be within a few ns of each other - the extra `GetStacks` call
per match check is one dictionary lookup. `StackConsume` adds the
queue + drain path, expected to add ~10-20 ns at 100 reactions.

### ReactionPriorityBenchmark

100 reactions across 100 distinct priorities tick in ~12 us, matching
100 reactions all at default priority. Priority is effectively free at
typical scale. 0 B / op. (Reference numbers from the v1.1 priority
PR; re-measure if any of the priority-tier code in `ReactionEngine`
changes.)

### ReactionAllocationDiagnostic

| Method | Allocated |
|---|---:|
| `NoOp` | 0 B |
| `EarlyExit` | 0 B |
| `OneTrivialReaction` | 0 B |

All three should stay at 0 B. Any non-zero number here means a
boxing or allocation creep somewhere in `ReactionEngine.CheckReactions`
or its dependencies; diagnose by bisecting the recent changes to that
call path.

### ManagerTickBenchmark / ReactionCheckBenchmark / StatRecalculateBenchmark / StatTickModifiersBenchmark

To populate. Run:

```sh
dotnet run -c Release
```

These existed before v1.1 and have not changed substantively. Any
hardware-specific reference numbers should be filled in here over
time.

## Per-PR perf review checklist

When changing any code on a hot path (anything called from
`EffectioManager.Tick` or `ReactionEngine.CheckReactions`):

1. Run the relevant benchmark before and after the change.
2. Confirm Allocated stays at 0 B.
3. If Mean changes by more than ~10%, mention the delta in the PR
   description with the before / after numbers and a one-line
   explanation.
4. If a new hot-path surface is added, also add a corresponding
   benchmark + a `*AllocationDiagnostic` baseline if appropriate.

## Future work

- A CI integration that runs the benchmarks on every PR and fails on
  allocation regression (currently a manual discipline).
- `StatusOnRefreshBenchmark` to validate that statuses with
  `OnRefreshEffects` do not penalise tight re-apply loops (the
  "stand-in-flame" pattern). Will land in a follow-up after the v1.1
  `OnRefresh` PR (#7) merges into `release/1.1.0`.
- An `EffectsEngine` per-`EffectType` benchmark suite. Marginal value
  over what `ManagerTickBenchmark` covers; add only if a perf concern
  surfaces in that area.
