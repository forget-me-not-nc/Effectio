using BenchmarkDotNet.Attributes;
using Effectio.Builders;
using Effectio.Core;
using Effectio.Statuses;

namespace Effectio.Benchmarks
{
    /// <summary>
    /// Measures <c>ReactionEngine.CheckReactions</c> across priority distributions
    /// to validate that v1.1's tier-based execution does not regress the
    /// priority-default (single-tier) path that all v1.0 consumers use.
    ///
    /// <para>
    /// Three shapes:
    /// <list type="bullet">
    /// <item><b>AllDefault</b> - every reaction at priority 0 (single tier; the
    /// shape every v1.0 codebase has).</item>
    /// <item><b>TwoTiers</b> - half at priority 100, half at priority 0 (typical
    /// v1.1 use: a small number of "ultimate" reactions on top of normal ones).</item>
    /// <item><b>ManyTiers</b> - every reaction at its own unique priority
    /// (worst case for the per-tier scanning in <c>CheckReactions</c>).</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// All reactions persist (no consume) and require the same single status,
    /// so every pass fires every reaction. The chain loop terminates after the
    /// second pass because no new matching-relevant statuses appear.
    /// </para>
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(warmupCount: 3, iterationCount: 5)]
    public class ReactionPriorityBenchmark
    {
        public enum PriorityShape
        {
            AllDefault,
            TwoTiers,
            ManyTiers
        }

        [Params(10, 50, 100)]
        public int ReactionCount { get; set; }

        [Params(PriorityShape.AllDefault, PriorityShape.TwoTiers, PriorityShape.ManyTiers)]
        public PriorityShape Shape { get; set; }

        private EffectioManager _manager = null!;
        private Effectio.Entities.IEffectioEntity _entity = null!;

        [GlobalSetup]
        public void Setup()
        {
            _manager = new EffectioManager();
            _manager.Statuses.RegisterStatus(new Status("Trigger"));

            for (int i = 0; i < ReactionCount; i++)
            {
                _manager.Statuses.RegisterStatus(new Status("out_" + i));

                int prio = Shape switch
                {
                    PriorityShape.AllDefault => 0,
                    PriorityShape.TwoTiers => i < ReactionCount / 2 ? 100 : 0,
                    PriorityShape.ManyTiers => i,
                    _ => 0
                };

                _manager.Reactions.RegisterReaction(ReactionBuilder.Create("r_" + i)
                    .RequireStatus("Trigger")
                    .Persists()
                    .Priority(prio)
                    .ApplyStatus("out_" + i)
                    .Build());
            }

            _entity = _manager.CreateEntity("p");
            _entity.AddStatus("Trigger");
        }

        [Benchmark]
        public void CheckReactions() => _manager.Reactions.CheckReactions(_entity);
    }
}
