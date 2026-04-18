using BenchmarkDotNet.Attributes;
using Effectio.Builders;
using Effectio.Core;
using Effectio.Statuses;

namespace Effectio.Benchmarks
{
    /// <summary>
    /// Allocation regression guard for ReactionEngine.CheckReactions.
    /// Three escalating baselines pin down where any future allocation creeps in:
    /// NoOp (BenchmarkDotNet harness floor), EarlyExit (CheckReactions with no
    /// reactions registered), OneTrivialReaction (one reaction matching and firing
    /// against an entity in steady state - see remarks for the exact path measured).
    /// </summary>
    /// <remarks>
    /// Originally added to diagnose a 40 B / op allocation in v1.1
    /// ReactionPriorityBenchmark caused by HashSet&lt;T&gt;.ExceptWith(IEnumerable&lt;T&gt;)
    /// boxing its argument's struct enumerator. Fixed by iterating the concrete
    /// HashSet&lt;string&gt; directly. Kept as a permanent regression guard so an
    /// equivalent boxing creep would surface immediately on the next benchmark run.
    /// <para>
    /// <b>What OneTrivialReaction actually measures.</b> The first invocation runs
    /// the 2-pass chain path (the reaction's <c>ApplyStatus("out")</c> creates a
    /// new status, which triggers chain detection to run again). Every subsequent
    /// invocation against the same persistent <c>_oneReactionEntity</c> runs the
    /// 1-pass steady-state path: the entity already has "out" from the first call,
    /// so the reaction's <c>ApplyStatus</c> hits the existing-status refresh branch,
    /// no new statuses appear, and chain detection breaks after pass 1.
    /// BenchmarkDotNet's warmup absorbs the one-shot 2-pass path; the reported
    /// per-op number reflects the 1-pass steady state.
    /// </para>
    /// <para>
    /// <b>Why we don't reset state per invocation.</b> A reviewer may notice the
    /// path divergence and reach for <c>[IterationSetup]</c> /
    /// <c>[InvocationSetup]</c> to force every invocation through the 2-pass path.
    /// Resist the temptation: this benchmark's job is the allocation number, not
    /// path coverage. The reset itself (RemoveStatus + dictionary mutation) would
    /// add noise to a sub-microsecond measurement and could introduce its own
    /// allocations that mask the very thing we're guarding against. The 2-pass
    /// path is implicitly covered by ReactionPriorityBenchmark - if its
    /// chain-detection allocation regressed, that benchmark would surface it
    /// alongside this one.
    /// </para>
    /// </remarks>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 5)]
    public class ReactionAllocationDiagnostic
    {
        private EffectioManager _emptyManager = null!;
        private Effectio.Entities.IEffectioEntity _emptyEntity = null!;

        private EffectioManager _oneReactionManager = null!;
        private Effectio.Entities.IEffectioEntity _oneReactionEntity = null!;

        [GlobalSetup]
        public void Setup()
        {
            _emptyManager = new EffectioManager();
            _emptyEntity = _emptyManager.CreateEntity("e");

            _oneReactionManager = new EffectioManager();
            _oneReactionManager.Statuses.RegisterStatus(new Status("Trigger"));
            _oneReactionManager.Statuses.RegisterStatus(new Status("out"));
            _oneReactionManager.Reactions.RegisterReaction(ReactionBuilder.Create("r")
                .RequireStatus("Trigger")
                .Persists()
                .ApplyStatus("out")
                .Build());
            _oneReactionEntity = _oneReactionManager.CreateEntity("e");
            _oneReactionEntity.AddStatus("Trigger");
        }

        [Benchmark(Baseline = true)]
        public void NoOp() { }

        [Benchmark]
        public void EarlyExit() => _emptyManager.Reactions.CheckReactions(_emptyEntity);

        // First invocation: 2-pass chain (status "out" gets created in pass 1).
        // Steady state (every subsequent invocation, which is what BDN actually
        // measures after warmup): 1 pass - "out" already present, ApplyStatus
        // hits the refresh branch, chain detection breaks because no new
        // statuses appeared. See class-level remarks for why we deliberately
        // do not reset state per invocation.
        [Benchmark]
        public void OneTrivialReaction() => _oneReactionManager.Reactions.CheckReactions(_oneReactionEntity);
    }
}
