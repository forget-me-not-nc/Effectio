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
    /// reactions registered), OneTrivialReaction (one reaction matching and firing).
    /// </summary>
    /// <remarks>
    /// Originally added to diagnose a 40 B / op allocation in v1.1
    /// ReactionPriorityBenchmark caused by HashSet&lt;T&gt;.ExceptWith(IEnumerable&lt;T&gt;)
    /// boxing its argument's struct enumerator. Fixed by iterating the concrete
    /// HashSet&lt;string&gt; directly. Kept as a permanent regression guard so an
    /// equivalent boxing creep would surface immediately on the next benchmark run.
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

        [Benchmark]
        public void OneTrivialReaction() => _oneReactionManager.Reactions.CheckReactions(_oneReactionEntity);
    }
}
