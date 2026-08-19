using BenchmarkDotNet.Attributes;
using Effectio.Builders;
using Effectio.Core;
using Effectio.Entities;
using Effectio.Statuses;

namespace Effectio.Benchmarks
{
    /// <summary>
    /// v1.1 stack-aware reaction matching cost. Compares three reaction shapes
    /// at varying reaction counts to validate that <c>RequireStacks(key, N)</c>
    /// and <c>ConsumesStacks(key, N)</c> add no measurable overhead vs plain
    /// presence-based reactions at typical scale.
    /// </summary>
    /// <remarks>
    /// Each shape uses the same set of N reactions matching the same keys -
    /// only the predicate / consume kind changes:
    /// <list type="bullet">
    /// <item><description><c>Presence</c>: classic v1.0 <c>RequireStatus("Burning")</c>
    /// + <c>Persists()</c>. Baseline.</description></item>
    /// <item><description><c>StackThreshold</c>: v1.1 <c>RequireStacks("Burning", 1)</c>
    /// + <c>Persists()</c>. Adds one <c>GetStacks</c> call per match check vs
    /// the presence-only path.</description></item>
    /// <item><description><c>StackConsume</c>: v1.1 <c>RequireStacks("Burning", 1)</c>
    /// + <c>ConsumesStacks("Burning", 1)</c>. Adds the consume-buffer path on top
    /// of the threshold check.</description></item>
    /// </list>
    /// Setup pre-stacks the entity to a high count so the consume path never
    /// exhausts during a benchmark iteration (entity with 0 stacks would cause
    /// reactions to stop matching, dropping measured cost to "early exit").
    /// </remarks>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 5)]
    public class StackAwareReactionBenchmark
    {
        [Params(10, 50, 100)]
        public int ReactionCount { get; set; }

        private EffectioManager _presenceManager = null!;
        private IEffectioEntity _presenceEntity = null!;

        private EffectioManager _stackThresholdManager = null!;
        private IEffectioEntity _stackThresholdEntity = null!;

        private EffectioManager _stackConsumeManager = null!;
        private IEffectioEntity _stackConsumeEntity = null!;

        [GlobalSetup]
        public void Setup()
        {
            _presenceManager = BuildManager(stackAware: false, consumes: false);
            _presenceEntity = _presenceManager.GetEntity("e");

            _stackThresholdManager = BuildManager(stackAware: true, consumes: false);
            _stackThresholdEntity = _stackThresholdManager.GetEntity("e");

            _stackConsumeManager = BuildManager(stackAware: true, consumes: true);
            _stackConsumeEntity = _stackConsumeManager.GetEntity("e");
        }

        private EffectioManager BuildManager(bool stackAware, bool consumes)
        {
            var manager = new EffectioManager();
            // Huge MaxStacks so the consume path never empties during a run.
            manager.Statuses.RegisterStatus(new Status("Burning", duration: float.MaxValue, maxStacks: int.MaxValue));

            for (int i = 0; i < ReactionCount; i++)
            {
                var b = ReactionBuilder.Create("r" + i);
                if (stackAware) b.RequireStacks("Burning", 1);
                else            b.RequireStatus("Burning");

                if (consumes)   b.ConsumesStacks("Burning", 0); // 0 = match-only, no decrement
                else            b.Persists();

                manager.Reactions.RegisterReaction(b.Build());
            }

            var entity = manager.CreateEntity("e");
            // Pre-stack to a small fixed count. NONE of the three benchmark methods
            // decrement stacks - Presence and StackThreshold use Persists() (no consume),
            // StackConsume uses ConsumesStacks(_, 0) which early-returns inside
            // RemoveStacks (count <= 0 fast-path). So we only need stacks >= the
            // RequireStacks threshold (1). Setup cost is negligible.
            for (int i = 0; i < 5; i++)
                manager.Statuses.ApplyStatus(entity, "Burning");
            return manager;
        }

        // -------- Steady-state CheckReactions cost --------

        /// <summary>v1.0 baseline: N reactions all matching via RequireStatus("Burning").</summary>
        [Benchmark(Baseline = true)]
        public void Presence() => _presenceManager.Reactions.CheckReactions(_presenceEntity);

        /// <summary>v1.1: N reactions all matching via RequireStacks("Burning", 1) + Persists.</summary>
        [Benchmark]
        public void StackThreshold() => _stackThresholdManager.Reactions.CheckReactions(_stackThresholdEntity);

        /// <summary>
        /// v1.1: N reactions all matching via RequireStacks + ConsumesStacks(_, 0). The 0-count
        /// consume hits the queue + drain path but RemoveStacks's early-return prevents stack
        /// exhaustion, isolating the consume-path overhead vs StackThreshold without the
        /// exhaustion confound.
        /// </summary>
        [Benchmark]
        public void StackConsume() => _stackConsumeManager.Reactions.CheckReactions(_stackConsumeEntity);
    }
}
