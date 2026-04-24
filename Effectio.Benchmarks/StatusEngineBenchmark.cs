using BenchmarkDotNet.Attributes;
using Effectio.Entities;
using Effectio.Statuses;

namespace Effectio.Benchmarks
{
    /// <summary>
    /// Foundational benchmark for the StatusEngine surface: ApplyStatus's two
    /// steady-state existing-status paths (stack-increment-and-refresh vs
    /// at-MaxStacks-refresh-only) and isolated Tick at scale. Complements
    /// ManagerTickBenchmark which exercises the same calls indirectly through
    /// the full per-frame pipeline.
    /// </summary>
    /// <remarks>
    /// <b>What is NOT measured here.</b> Two paths are intentionally absent:
    /// <list type="bullet">
    /// <item><description>The first-time application path (new dictionary entry +
    /// OnStatusApplied fire). Measuring it cleanly requires per-invocation state reset
    /// and the reset itself dominates the measurement at sub-microsecond per-call cost.
    /// The path is well-understood (one dict entry allocation amortised across the
    /// entity's lifetime).</description></item>
    /// <item><description><c>IStackOperations.RemoveStacks</c>. It's a dictionary lookup +
    /// counter decrement + event invocation - cost dominated by the same dict-lookup that
    /// <c>ApplyStatus_RefreshAndStack</c> already characterises. The interesting RemoveStacks
    /// question ("what happens when many reactions consume stacks per tier?") is covered by
    /// <c>StackAwareReactionBenchmark</c> with proper setup that prevents stack exhaustion
    /// during long iterations.</description></item>
    /// </list>
    /// All benchmarks here should report 0 B / op in steady state - the engine
    /// reuses pooled buffers, dictionary entries are pre-allocated after warmup,
    /// and Tick's _expiredBuffer is reused across calls.
    /// <para>
    /// Entity counts are powers of two so the round-robin index can use a
    /// single-cycle bitmask (<c>cursor &amp; (EntityCount - 1)</c>) instead of
    /// a modulo. Round-numbers like 100 / 1000 would inflate the measurement
    /// with a per-invocation integer division.
    /// </para>
    /// </remarks>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 5)]
    public class StatusEngineBenchmark
    {
        [Params(128, 1024)]
        public int EntityCount { get; set; }

        private StatusEngine _engineForRefresh = null!;
        private IEffectioEntity[] _entitiesForRefresh = null!;

        private StatusEngine _engineForAtMax = null!;
        private IEffectioEntity[] _entitiesForAtMax = null!;

        private StatusEngine _engineForTick = null!;

        // Round-robin cursor so successive invocations hit different entities
        // (avoids a single hot cache line skewing the measurement).
        private int _cursor;

        [GlobalSetup]
        public void Setup()
        {
            // -------- ApplyStatus (REFRESH below max) setup --------
            // MaxStacks=100 keeps every benchmark invocation in the increment-and-refresh
            // branch for the duration of any realistic BDN run.
            _engineForRefresh = new StatusEngine();
            _engineForRefresh.RegisterStatus(new Status("Burning", duration: float.MaxValue, maxStacks: 100));
            _entitiesForRefresh = new IEffectioEntity[EntityCount];
            for (int i = 0; i < EntityCount; i++)
            {
                _entitiesForRefresh[i] = new EffectioEntity("refresh_" + i);
                _engineForRefresh.ApplyStatus(_entitiesForRefresh[i], "Burning"); // seed at stacks=1
            }

            // -------- ApplyStatus (AT MAX) setup --------
            // MaxStacks=1, seeded at the cap. Every benchmark invocation hits the
            // at-max refresh-only branch (counter unchanged, only RemainingDuration refreshes,
            // no OnStatusStacked fires, OnStatusRefreshed fires).
            _engineForAtMax = new StatusEngine();
            _engineForAtMax.RegisterStatus(new Status("Burning", duration: float.MaxValue, maxStacks: 1));
            _entitiesForAtMax = new IEffectioEntity[EntityCount];
            for (int i = 0; i < EntityCount; i++)
            {
                _entitiesForAtMax[i] = new EffectioEntity("atmax_" + i);
                _engineForAtMax.ApplyStatus(_entitiesForAtMax[i], "Burning");
            }

            // -------- Tick setup: every entity has 5 long-duration statuses --------
            _engineForTick = new StatusEngine();
            for (int s = 0; s < 5; s++)
                _engineForTick.RegisterStatus(new Status("S" + s, duration: float.MaxValue, maxStacks: 3, tickInterval: float.MaxValue));
            for (int i = 0; i < EntityCount; i++)
            {
                var e = new EffectioEntity("tick_" + i);
                for (int s = 0; s < 5; s++) _engineForTick.ApplyStatus(e, "S" + s);
            }

            _cursor = 0;
        }

        /// <summary>
        /// Existing-status path that increments stacks (below MaxStacks). Steady-state
        /// measurement: dictionary lookup, counter increment, duration write,
        /// OnStatusStacked + OnStatusRefreshed event invocations (no subscribers in this
        /// isolated harness so they short-circuit to the null check).
        /// </summary>
        [Benchmark]
        public void ApplyStatus_RefreshAndStack()
        {
            int idx = (_cursor++) & (EntityCount - 1);
            _engineForRefresh.ApplyStatus(_entitiesForRefresh[idx], "Burning");
        }

        /// <summary>
        /// Existing-status path at MaxStacks: counter unchanged, only RemainingDuration
        /// refreshes. Cheapest existing-status path (one dict lookup + one float write).
        /// </summary>
        [Benchmark]
        public void ApplyStatus_AtMaxRefresh()
        {
            int idx = (_cursor++) & (EntityCount - 1);
            _engineForAtMax.ApplyStatus(_entitiesForAtMax[idx], "Burning");
        }

        /// <summary>
        /// Engine-only Tick at <see cref="EntityCount"/> entities x 5 statuses each.
        /// With huge durations no expirations happen; this measures pure
        /// dictionary-of-dictionary iteration cost plus float arithmetic per cell.
        /// </summary>
        [Benchmark]
        public void Tick()
        {
            _engineForTick.Tick(0.001f);
        }
    }
}
